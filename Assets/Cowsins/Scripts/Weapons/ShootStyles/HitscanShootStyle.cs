// HitscanShootStyle.cs
// 即时命中(Hitscan)射击样式(策略模式实现之一)：当武器被设置为 Hitscan 射击样式时，
// WeaponIdentification 的 shootBehaviour 会被赋值为本类实例。
// 射击时从主摄像机发射射线，按散布角度偏移方向；命中后计算伤害、处理穿透并生成弹道拖尾，
// 射速与弹丸间隔由协程(canShoot 门控 + AllowShootAfterDelay)进行控制。
using System;
using UnityEngine;
using System.Collections;

namespace cowsins
{
    /// <summary>
    /// When a weapon is set to Hitscan Shoot Style, its shootBehaviour in WeaponIdentification is assigned to this HitscanShootStyle.
    /// This is done when the weapon is unholstered.
    /// </summary>
    public class HitscanShootStyle : IShootStyle
    {
        // 当前武器的数据(ScriptableObject)
        private Weapon_SO weapon;
        // 当前武器的身份标识(含伤害、射速、枪口点等运行时数据)
        private WeaponIdentification id;
        // 玩家依赖容器，用于获取武器引用、启动协程等
        private PlayerDependencies playerDependencies;
        // 武器引用提供者，用于获取主摄像机、武器数据等
        private IWeaponReferenceProvider weaponReference;
        // 武器事件提供者，用于广播射击、命中等事件
        private IWeaponEventsProvider weaponEvents;
        // 玩家倍率提供者，用于获取伤害倍率
        private IPlayerMultipliers multipliers;
        // 枪口点数组，弹道拖尾从这些位置生成
        private Transform[] firePoint;
        // 玩家主摄像机，射线起点与方向基准
        private Camera mainCamera;
        // 射击射线可命中的层掩码
        private LayerMask hitLayer;

        // 射击门控标志：为 false 时禁止射击
        private bool canShoot = true;
        // 射击协程与射速冷却协程的引用，便于停止/管理
        public Coroutine shootingCoroutine, allowShootCoroutine;

        // Calls events in Weapon Controller when shooting or hitting an enemy
        public event Action onShoot;

        /// <summary>构造即时命中射击样式实例：缓存依赖引用、枪口点、主摄像机与命中层。</summary>
        /// <param name="playerDependencies">玩家依赖容器。</param>
        /// <param name="hitLayer">射击射线可命中的层掩码。</param>
        public HitscanShootStyle(PlayerDependencies playerDependencies, LayerMask hitLayer)
        {
            this.playerDependencies = playerDependencies;
            this.weaponReference = playerDependencies.WeaponReference;
            this.weaponEvents = playerDependencies.WeaponEvents;
            this.weapon = weaponReference.Weapon;
            this.id = weaponReference.Id;  
            this.multipliers = playerDependencies.PlayerMultipliers;
            this.firePoint = id.FirePoint;
            this.mainCamera = weaponReference.MainCamera;
            this.hitLayer = hitLayer;
        }

        /// <summary>执行一次即时命中射击：若处于可射击状态则进入命中射线处理流程。</summary>
        /// <param name="spread">散布角度，用于随机偏移射线方向。</param>
        /// <param name="damageMultiplier">伤害倍率(本样式未直接使用，实际伤害倍率通过 multipliers 获取)。</param>
        /// <param name="shakeMultiplier">相机抖动倍率(本样式未直接使用)。</param>
        public void Shoot(float spread, float damageMultiplier, float shakeMultiplier)
        {
            if (canShoot) 
                HandleHitscanProjectileShot(spread); 
        }

        // 处理一次即时命中射击：
        // 1) 置 canShoot 为 false 禁止连发；
        // 2) 启动射击协程 HandleShooting 实际发射弹丸；
        // 3) 若存在上一个冷却协程则先停止它，再启动新的射速冷却协程；
        // 4) 广播 OnShootSpawnEffects 事件以生成枪口特效。
        private void HandleHitscanProjectileShot(float spread)
        {
            if (weapon == null) return;

            canShoot = false; // since you have already shot, you will have to wait in order to being able to shoot again

            shootingCoroutine = playerDependencies.StartCoroutine(HandleShooting(spread));
            if(allowShootCoroutine != null)
            {
                playerDependencies.StopCoroutine(allowShootCoroutine);
                allowShootCoroutine = null;
            }
            playerDependencies.StartCoroutine(AllowShootAfterDelay(id.fireRate));

            weaponEvents.Events.OnShootSpawnEffects?.Invoke();
        }

        // 射击协程：先按需等待 shootDelay(仅 shootStyle 枚举值为 1 时等待)，
        // 再按 bulletsPerFire 发射一发或多发子弹，多发射击间以 timeBetweenShots 间隔；
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
                HitscanShot(spread);
            }
            else
            {
                int i = 0;
                while (i < weapon.bulletsPerFire)
                {
                    if (weapon == null) yield break;

                    HitscanShot(spread);
                    yield return new WaitForSeconds(weapon.timeBetweenShots);
                    i++;
                }
            }

            yield break;
        }

        // 发射一发即时命中射线：按散布角度计算方向，从主摄像机发射射线；
        // 命中后计算伤害并广播 OnHit 事件，随后处理穿透与弹道拖尾。
        private void HitscanShot(float spread)
        {
            onShoot?.Invoke();
        
            RaycastHit hit;
            Transform hitObj;

            //This defines the first hit on the object
            Vector3 dir = CowsinsUtilities.GetSpreadDirection(spread, mainCamera);
            Ray ray = new Ray(mainCamera.transform.position, dir);

            // 发射射线检测命中；命中后按 武器伤害×玩家伤害倍率 计算基础伤害并广播 OnHit 事件
            if (Physics.Raycast(ray, out hit, weapon.bulletRange, hitLayer))
            {
                float dmg = id.damage * multipliers.DamageMultiplier.Value;
                weaponEvents.Events.OnHit?.Invoke(hit.collider.gameObject.layer, dmg, hit, true);
                hitObj = hit.collider.transform;

                // 穿透处理：从首个命中点沿原方向再发一条短射线，检测穿透后的第二个目标
                //Handle Penetration
                Ray newRay = new Ray(hit.point, ray.direction);
                RaycastHit newHit;

                if (Physics.Raycast(newRay, out newHit, id.penetrationAmount, hitLayer))
                {
                    if (hitObj != newHit.collider.transform)
                    {
                        float dmg_ = id.damage * multipliers.DamageMultiplier.Value * weapon.penetrationDamageReduction;
                        weaponEvents.Events.OnHit?.Invoke(newHit.collider.gameObject.layer, dmg_, newHit, true);
                    }
                }

                // 弹道拖尾：从每个枪口点取出一个拖尾渲染器，并启动协程使其从枪口移动到命中点
                // Handle Bullet Trails
                if (weapon.bulletTrail == null) return;

                foreach (var p in firePoint)
                {
                    if(p == null) continue;

                    TrailRenderer trail = PoolManager.Instance.GetFromPool(weapon.bulletTrail.gameObject, p.position, Quaternion.identity).GetComponent<TrailRenderer>();

                    playerDependencies.StartCoroutine(SpawnTrail(trail, hit));
                }
            }
        }
        // 弹道拖尾协程：让拖尾渲染器从枪口位置沿直线移动到命中点，到达后归还对象池。
        private IEnumerator SpawnTrail(TrailRenderer trail, RaycastHit hit)
        {
            float time = 0;
            Vector3 startPos = trail.transform.position;
            GameObject prefab = weapon.bulletTrail != null ? weapon.bulletTrail.gameObject : null;
            while (time < 1)
            {
                trail.transform.position = Vector3.Lerp(startPos, hit.point, time);
                time += Time.deltaTime / trail.time;

                yield return null;
            }

            trail.transform.position = hit.point;
            PoolManager.Instance.ReturnToPool(trail.gameObject, prefab);
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
