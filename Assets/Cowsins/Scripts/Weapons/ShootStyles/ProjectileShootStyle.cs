// ProjectileShootStyle.cs
// 投射物射击样式(策略模式实现之一)：当武器被设置为 Projectile 射击样式时，
// WeaponIdentification 的 shootBehaviour 会被赋值为本类实例。
// 射击时从主摄像机计算目标落点，在枪口点实例化弹丸(IBullet)并配置速度、伤害、爆炸等属性，
// 射速与弹丸间隔由协程门控(canShoot 标志 + AllowShootAfterDelay)进行控制。
using System;
using System.Collections;
using UnityEngine;

namespace cowsins
{
    /// <summary>
    /// When a weapon is set to Projectile Shoot Style, its shootBehaviour in WeaponIdentification is assigned to this ProjectileShootStyle.
    /// This is done when the weapon is unholstered.
    /// </summary>
    public class ProjectileShootStyle : IShootStyle
    {
        // 玩家依赖容器，用于获取武器引用、启动协程等
        private PlayerDependencies playerDependencies;
        // 武器引用提供者，用于获取主摄像机、武器数据等
        private IWeaponReferenceProvider weaponReference;
        // 玩家倍率提供者，用于获取伤害倍率
        private IPlayerMultipliers multipliers;
        // 武器事件提供者，用于广播射击等事件
        private IWeaponEventsProvider weaponEvents; 
        // 枪口点数组，弹丸从这些位置实例化
        private Transform[] firePoint;
        // 当前武器的数据(ScriptableObject)
        private Weapon_SO weapon;
        // 当前武器的身份标识(含伤害、射速等运行时数据)
        private WeaponIdentification id;
        // 玩家主摄像机，用于计算弹丸目标点
        private Camera mainCamera;

        // 射击门控标志：为 false 时禁止射击
        private bool canShoot = true;
        // 射击协程与射速冷却协程的引用，便于停止/管理
        public Coroutine shootingCoroutine, allowShootCoroutine;

        // Calls events in Weapon Controller when shooting or hitting an enemy ( ProjectileShootStyle does not handle hits so onHit is not used here,
        // but it is still required by IShootStyle.
        public event Action onShoot;
#pragma warning disable CS0067
        public event Action<int, float, RaycastHit, bool> onHit;
#pragma warning restore CS0067

        /// <summary>构造投射物射击样式实例：缓存依赖引用、枪口点与主摄像机。</summary>
        /// <param name="playerDependencies">玩家依赖容器。</param>
        public ProjectileShootStyle(PlayerDependencies playerDependencies)
        {
            this.playerDependencies = playerDependencies;
            this.weaponReference = playerDependencies.WeaponReference;
            this.weapon = weaponReference.Weapon;
            this.id = weaponReference.Id;
            this.multipliers = playerDependencies.PlayerMultipliers;
            this.weaponEvents = playerDependencies.WeaponEvents;
            this.mainCamera = weaponReference.MainCamera;
            this.firePoint = id.FirePoint;
        }

        /// <summary>执行一次投射物射击：若处于可射击状态则进入投射物生成流程。</summary>
        /// <param name="spread">散布角度，用于随机偏移弹丸目标方向。</param>
        /// <param name="damageMultiplier">伤害倍率(本样式未直接使用，实际伤害倍率通过 multipliers 获取)。</param>
        /// <param name="shakeMultiplier">相机抖动倍率(本样式未直接使用)。</param>
        public void Shoot(float spread, float damageMultiplier, float shakeMultiplier)
        {
            if (canShoot)
                HandleProjectileShot(spread);
        }
        // 处理一次投射物射击：
        // 1) 置 canShoot 为 false 禁止连发；
        // 2) 启动射击协程 HandleShooting 实际生成弹丸；
        // 3) 若存在上一个冷却协程则先停止它，再启动新的射速冷却协程；
        // 4) 广播 OnShootSpawnEffects 事件以生成枪口特效。
        private void HandleProjectileShot(float spread)
        {
            if (weapon == null) return;

            canShoot = false; // since you have already shot, you will have to wait in order to being able to shoot again

            shootingCoroutine = playerDependencies.StartCoroutine(HandleShooting(spread));
            if (allowShootCoroutine != null)
            {
                playerDependencies.StopCoroutine(allowShootCoroutine);
                allowShootCoroutine = null;
            }
            playerDependencies.StartCoroutine(AllowShootAfterDelay(id.fireRate));

            weaponEvents.Events.OnShootSpawnEffects?.Invoke();
        }

        // 射击协程：先按需等待 shootDelay(仅 shootStyle 枚举值为 1 时等待)，
        // 再按 bulletsPerFire 生成一发或多发弹丸，多发射击间以 timeBetweenShots 间隔；
        // 若 timeBetweenShots 为 0(即一次性发射)，则先广播 OnReduceAmmo 事件扣减弹药。
        private IEnumerator HandleShooting(float spread)
        {
            if ((int)weapon.shootStyle == 1)
            {
                yield return new WaitForSeconds(weapon.shootDelay);
            }

            if (weapon.timeBetweenShots == 0)
            {
                // Rest the bullets that have just been shot
                weaponEvents.Events.OnReduceAmmo?.Invoke();
            }

            // Avoid calling the while loop if we only want to shoot one bullet
            if (weapon.bulletsPerFire == 1)
            {
                if (weapon == null) yield break;
                ProjectileShoot(spread);
            }
            else
            {
                int i = 0;
                while (i < weapon.bulletsPerFire)
                {
                    if (weapon == null) yield break;

                    ProjectileShoot(spread);
                    yield return new WaitForSeconds(weapon.timeBetweenShots);
                    i++;
                }
            }

            yield break;
        }

        // 生成一发投射物弹丸：计算目标落点，在枪口点实例化弹丸并配置
        // 伤害、速度、爆炸、暴击倍率、存活时长等属性，再按需设置刚体的运动学状态。
        private void ProjectileShoot(float spread)
        {
            onShoot?.Invoke();

            RaycastHit hit;
            Vector3 dir = CowsinsUtilities.GetSpreadDirection(spread, mainCamera);
            Ray ray = new Ray(mainCamera.transform.position, dir);
            // 计算弹丸目标点：射线命中非玩家物体则取其命中点，否则取射线前方 50 米处
            Vector3 destination = (Physics.Raycast(ray, out hit) && !hit.transform.CompareTag("Player")) ? hit.point : ray.GetPoint(50f);

            // 若未配置弹丸预制体或弹丸未实现 IBullet 接口，则警告并跳过本次发射
            if (weapon.projectile == null || weapon.projectile?.GetComponent<IBullet>() == null)
            {
                Debug.LogWarning($"Projectile is not set for {weapon._name}");
                return;
            }

            // 在每个枪口点实例化一枚弹丸，并写入爆炸、伤害、速度等配置
            foreach (var p in firePoint)
            {
                // 实例化弹丸并获取其 IBullet 接口
                GameObject bulletGO = GameObject.Instantiate(weapon.projectile.gameObject, p.position, p.transform.rotation);
                IBullet bullet = bulletGO.GetComponent<IBullet>();
                // 若武器开启命中爆炸，则把爆炸特效预制体赋给弹丸
                if (weapon.explosionOnHit) bullet.ExplosionVFX = weapon.explosionVFX;

                bullet.HurtsPlayer = weapon.hurtsPlayer;
                bullet.ExplosionOnHit = weapon.explosionOnHit;
                bullet.ExplosionRadius = weapon.explosionRadius;
                bullet.ExplosionForce = weapon.explosionForce;

                bullet.CriticalMultiplier = weapon.criticalDamageMultiplier;
                bullet.Destination = destination;
                bullet.Player = playerDependencies.transform;
                bullet.Speed = weapon.speed;
                if (bulletGO.TryGetComponent<Rigidbody>(out Rigidbody rb)) rb.isKinematic = !weapon.projectileUsesGravity;
                bullet.Damage = id.damage * multipliers.DamageMultiplier.Value;
                bullet.Duration = weapon.bulletDuration;
            }
        }

        // 射速冷却协程：等待 delay 秒后将 canShoot 置回 true，重新允许射击。
        private IEnumerator AllowShootAfterDelay(float delay)
        {
            yield return new WaitForSeconds(delay);
            canShoot = true;
        }

        /// <summary>设置射击事件回调，供 WeaponController 订阅。</summary>
        /// <param name="event">要订阅的射击事件。</param>
        public void SetOnShootEvent(Action @event) => onShoot = @event;
    }
}
