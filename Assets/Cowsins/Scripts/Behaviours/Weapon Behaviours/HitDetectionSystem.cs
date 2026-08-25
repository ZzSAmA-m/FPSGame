// HitDetectionSystem.cs：命中检测系统。
// 处理子弹命中后的伤害结算与命中特效触发：根据命中部位标签（Critical 暴击/BodyShot 身体）或
// IDamageable 接口施加伤害，并考虑距离伤害衰减；同时在构造时注册命中特效的物体池。
using UnityEngine;

namespace cowsins
{
    /// <summary>
    /// 命中检测系统：接收命中信息，结算伤害（含暴击与距离衰减）并触发命中特效与事件。
    /// </summary>
    public class HitDetectionSystem
    {
        // 武器上下文，聚合依赖项与设置。
        private WeaponContext context;
        // 输入管理器（本系统内暂未直接使用，由上下文中其它模块消费）。
        private InputManager inputManager;
        // 武器引用提供者，提供当前武器数据。
        private IWeaponReferenceProvider weaponReference;
        // 武器事件提供者，用于触发命中相关事件。
        private IWeaponEventsProvider weaponEvents;
        // 玩家移动状态提供者（本系统内暂未直接使用）。
        private IPlayerMovementStateProvider playerMovement;

        // 当前武器数据（Weapon_SO）快捷属性。
        private Weapon_SO weapon => weaponReference.Weapon;
        // 当前武器实例标识快捷属性。
        private WeaponIdentification id => weaponReference.Id;
        // 主相机快捷属性。
        private Camera mainCamera => weaponReference.MainCamera;
        // 武器挂载点（持枪父节点）快捷属性。
        private Transform weaponHolder => context.WeaponHolder;

        // 武器控制器设置，包含命中特效等配置。
        private WeaponControllerSettings settings;

        /// <summary>
        /// 构造函数：缓存依赖引用，为命中特效（按层与默认）注册物体池，并订阅 OnHit 事件以执行命中结算。
        /// </summary>
        /// <param name="context">武器上下文。</param>
        /// <param name="settings">武器控制器设置，含命中特效配置。</param>
        public HitDetectionSystem(WeaponContext context, WeaponControllerSettings settings)
        {
            this.settings = settings;

            // Register Pool
            foreach (var entry in this.settings.impactEffects.impacts)
            {
                if (entry.impact != null)
                    PoolManager.Instance.RegisterPool(entry.impact, PoolManager.Instance.WeaponEffectsSize);
            }
            if (this.settings.impactEffects.defaultImpact != null)
                PoolManager.Instance.RegisterPool(this.settings.impactEffects.defaultImpact, PoolManager.Instance.WeaponEffectsSize);

            this.context = context;
            this.inputManager = context.InputManager;
            this.weaponReference = context.Dependencies.WeaponReference;
            this.weaponEvents = context.Dependencies.WeaponEvents;
            this.playerMovement = context.Dependencies.PlayerMovementState;

            context.Dependencies.WeaponEvents.Events.OnHit.AddListener(Hit);
        }

        // 命中结算入口：触发命中特效，并根据命中部位（暴击/身体/通用）施加伤害；伤害会先乘以距离衰减系数。
        /// <summary>
        /// Handles hit detection, applies damage, and triggers effects.
        /// </summary>
        /// <param name="layer">命中物体的层，用于选择对应的命中特效。</param>
        /// <param name="damage">基础伤害值。</param>
        /// <param name="h">命中信息（含碰撞体、法线、命中点等）。</param>
        /// <param name="damageTarget">是否对目标造成伤害（false 时仅触发命中特效）。</param>
        public void Hit(int layer, float damage, RaycastHit h, bool damageTarget)
        {
            if (weapon == null || h.collider == null) return;

            settings.userEvents.OnHit?.Invoke();
            weaponEvents.Events.OnInstantiateBulletHoleImpact?.Invoke(layer, h);

            if (!damageTarget) return;

            var hitTransform = h.collider.transform;
            float finalDamage = damage * GetDistanceDamageReduction(hitTransform);

            // Determine hit type and apply damage accordingly
            if (hitTransform.CompareTag("Critical"))
            {
                settings.userEvents.OnCriticalHit?.Invoke();
                var damageable = CowsinsUtilities.GatherDamageableParent(hitTransform);
                damageable?.Damage(finalDamage * weapon.criticalDamageMultiplier, true);
            }
            else if (hitTransform.CompareTag("BodyShot"))
            {
                var damageable = CowsinsUtilities.GatherDamageableParent(hitTransform);
                damageable?.Damage(finalDamage, false);
            }
            else
            {
                var damageable = h.collider.GetComponent<IDamageable>();
                damageable?.Damage(finalDamage, false);
            }
        }

        // 计算距离伤害衰减系数：若武器未启用距离衰减则返回 1（不衰减）；
        // 当目标距离超过最小衰减距离时，按“最小距离/实际距离 * 衰减倍率”计算衰减系数。
        private float GetDistanceDamageReduction(Transform target)
        {
            if (!weapon.applyDamageReductionBasedOnDistance) return 1;
            if (Vector3.Distance(target.position, context.Transform.position) > weapon.minimumDistanceToApplyDamageReduction)
                return (weapon.minimumDistanceToApplyDamageReduction / Vector3.Distance(target.position, context.Transform.position)) * weapon.damageReductionMultiplier;
            else return 1;
        }
    }

}