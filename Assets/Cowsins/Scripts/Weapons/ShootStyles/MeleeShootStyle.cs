// MeleeShootStyle.cs
// 近战射击样式(策略模式实现之一)：当武器被设置为 Melee 射击样式时，
// WeaponIdentification 的 shootBehaviour 会被赋值为本类实例。
// 攻击时播放近战动画，等待 hitDelay 后对玩家前方球形范围内的敌人造成伤害，
// 攻击频率由协程门控(canShoot 标志 + AllowShootAfterDelay)进行控制。
using UnityEngine;
using System.Collections;
using System;

namespace cowsins
{
    /// <summary>
    /// When a weapon is set to Melee Shoot Style, its shootBehaviour in WeaponIdentification is assigned to this MeleeShootStyle.
    /// This is done when the weapon is unholstered.
    /// </summary>
    public class MeleeShootStyle : IShootStyle
    {
        // 当前武器的数据(ScriptableObject)
        private Weapon_SO weapon;
        // 当前武器的身份标识(含动画、音效、枪口点等运行时数据)
        private WeaponIdentification id;
        // 玩家依赖容器，用于启动协程等
        private PlayerDependencies playerDependencies;
        // 武器事件提供者，用于广播命中等事件
        private IWeaponEventsProvider weaponEvents; 
        // 枪口点数组
        private Transform[] firePoint;
        // 玩家主摄像机
        private Camera mainCamera;
        // 近战可命中的层掩码
        private LayerMask hitLayer;

        // 攻击门控标志：为 false 时禁止攻击
        private bool canShoot = true;

        // Calls events in Weapon Controller when shooting or hitting an enemy
        public event Action onShoot;

        /// <summary>构造近战射击样式实例：缓存依赖引用、枪口点、主摄像机与命中层。</summary>
        /// <param name="playerDependencies">玩家依赖容器。</param>
        /// <param name="hitLayer">近战可命中的层掩码。</param>
        public MeleeShootStyle(PlayerDependencies playerDependencies, LayerMask hitLayer)
        {
            this.playerDependencies = playerDependencies;
            IWeaponReferenceProvider weaponReference = playerDependencies.WeaponReference;
            this.weaponEvents = playerDependencies.WeaponEvents;
            this.weapon = weaponReference.Weapon;
            this.id = weaponReference.Id;
            this.firePoint = id.FirePoint;
            this.mainCamera = weaponReference.MainCamera;
            this.hitLayer = hitLayer;
        }

        /// <summary>执行一次近战攻击：若处于可攻击状态则启动近战攻击协程。</summary>
        /// <param name="spread">散布角度(本样式未使用该参数)。</param>
        /// <param name="damageMultiplier">伤害倍率，用于计算近战伤害。</param>
        /// <param name="shakeMultiplier">相机抖动倍率(本样式未直接使用该参数)。</param>
        public void Shoot(float spread, float damageMultiplier, float shakeMultiplier)
        {
            if (canShoot)
                playerDependencies.StartCoroutine(HandleMeleeShotCoroutine(damageMultiplier, shakeMultiplier));
        }

        // 近战攻击协程：
        // 1) 置 canShoot 为 false 禁止连击；
        // 2) 播放随机近战动画与近战音效；
        // 3) 等待 hitDelay(命中延迟)后执行 Melee 造成伤害；
        // 4) 启动攻击冷却协程，并广播 onShoot 事件。
        private IEnumerator HandleMeleeShotCoroutine(float damageMultiplier, float shakeMultiplier)
        {
            canShoot = false;

            // Get the Animator component from the current weapon
            Animator animator = id.Animator;
            // Play the selected random animation
            CowsinsUtilities.ForcePlayAnim(id.GetCurrentShotAnimation(), animator);

            SoundManager.Instance.PlaySound(id.GetFireSFX(), 0, weapon.pitchVariationFiringSFX, true);

            if (weapon == null) yield break;

            // 默认使用武器配置的命中延迟；若配置了每击独立延迟，则按当前动画索引取对应值
            float delay = weapon.hitDelay;
            if (weapon.hitDelays != null && weapon.hitDelays.Length > id.CurrentShotIndex)
                delay = weapon.hitDelays[id.CurrentShotIndex];

            yield return new WaitForSeconds(delay);

            // 等待命中延迟后执行近战伤害判定，并启动攻击冷却协程
            Melee(weapon, 0, damageMultiplier);
            playerDependencies.StartCoroutine(AllowShootAfterDelay(weapon.attackRate));

            // OnShoot() inside WeaponController is subscribed to this onShoot method.
            // When onShoot is called, OnShoot() inside WeaponController is called. Mainly used for Camera Effects.
            onShoot?.Invoke();
        }

        // 执行近战判定：以玩家前方为球心做球形范围检测(OverlapSphere)，
        // 对命中的第一个可伤害目标(优先暴击/身体部位)造成伤害，并广播 OnHit 事件用于命中特效。
        private void Melee(Weapon_SO weapon, float spread, float damageMultiplier)
        {
            RaycastHit hit;
            Vector3 basePosition = id != null ? id.transform.position : playerDependencies.transform.position;
            // 以玩家前方(半个攻击距离处)为球心做球形范围检测，获取攻击范围内的所有碰撞体
            Collider[] col = Physics.OverlapSphere(basePosition + mainCamera.transform.parent.forward * weapon.attackRange / 2, weapon.attackRange, hitLayer);

            // 计算近战伤害：每击伤害 × 伤害倍率
            float dmg = weapon.damagePerHit * damageMultiplier;

            // 遍历命中碰撞体：优先命中暴击/身体部位，否则命中首个可伤害对象后立即跳出
            foreach (var c in col)
            {
                if (c.CompareTag("Critical") || c.CompareTag("BodyShot"))
                {
                    CowsinsUtilities.GatherDamageableParent(c.transform)?.Damage(dmg, false);
                    break;
                }

                IDamageable damageable = c.GetComponent<IDamageable>();
                if (damageable != null)
                {
                    damageable.Damage(dmg, false);
                    break;
                }
            }

            //VISUALS
            Ray ray = new Ray(mainCamera.transform.position, mainCamera.transform.forward);
            if (Physics.Raycast(ray, out hit, weapon.attackRange, hitLayer))
            {
                // Hit() inside WeaponController is subscribed to this onHit method.
                // When onHit is called, Hit() inside WeaponController is called.
                weaponEvents.Events.OnHit?.Invoke(hit.collider.gameObject.layer, 0f, hit, false);
            }
        }

        // 攻击冷却协程：等待 delay 秒后将 canShoot 置回 true，重新允许攻击。
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