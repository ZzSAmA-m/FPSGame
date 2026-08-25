// SpreadSystem.cs：武器子弹散布系统。
// 管理当前武器的子弹散布（spread）值，根据玩家是否瞄准在基础散布与瞄准散布之间切换，
// 并通过订阅武器事件（掏出武器、瞄准开始/结束）动态更新散布值。
using UnityEngine;

namespace cowsins
{
    /// <summary>
    /// 子弹散布系统：维护并对外提供当前散布值，随瞄准状态在基础散布与瞄准散布间切换。
    /// </summary>
    public class SpreadSystem
    {
        // 当前生效的散布值：玩家瞄准时为 baseAimSpread，否则为 baseSpread。
        private float spread; // Current spread in use. Changes to baseSpread or baseAimSpread depending on whether the player is aiming or not.

        // 对外暴露当前散布值的只读属性。
        public float Spread => spread;

        // 武器上下文，聚合依赖项与设置。
        private WeaponContext context;
        // 武器引用提供者，提供当前武器及其散布配置。
        private IWeaponReferenceProvider weaponReference;
        // 武器行为提供者，提供瞄准等武器状态。
        private IWeaponBehaviourProvider weaponBehaviour;

        /// <summary>
        /// 构造函数：缓存上下文与依赖引用，并订阅武器事件，在掏出武器、开始/结束瞄准时更新散布值。
        /// </summary>
        /// <param name="context">武器上下文，提供依赖项与设置。</param>
        public SpreadSystem(WeaponContext context)
        {
            this.context = context;
            this.weaponReference = context.Dependencies.WeaponReference;
            this.weaponBehaviour = context.Dependencies.WeaponBehaviour;

            var weaponEvents = context.Dependencies.WeaponEvents;
            weaponEvents.Events.OnUnholster.AddListener(UpdateSpreadOnUnholster);
            weaponEvents.Events.OnAiming.AddListener(UpdateSpreadOnAiming);
            weaponEvents.Events.OnAimStop.AddListener(UpdateSpreadOnAimStop);

            weaponEvents.Events.OnGetSpread += GetCurrentSpread;
        }

        /// <summary>掏出武器时回调：按当前是否瞄准状态更新散布（前两个参数仅为匹配事件签名，未使用）。</summary>
        public void UpdateSpreadOnUnholster(bool autoReloadProp, bool prop2) => UpdateSpread(weaponBehaviour.IsAiming);
        /// <summary>开始瞄准时回调：切换到瞄准散布（baseAimSpread）。</summary>
        public void UpdateSpreadOnAiming() => UpdateSpread(true);
        /// <summary>停止瞄准时回调：切换回基础散布（baseSpread）。</summary>
        public void UpdateSpreadOnAimStop() => UpdateSpread(false);
        /// <summary>
        /// 根据瞄准状态选择对应的散布基准值：瞄准时使用 baseAimSpread，否则使用 baseSpread。
        /// </summary>
        /// <param name="isAiming">当前是否正在瞄准。</param>
        public void UpdateSpread(bool isAiming)
        {
            float newSpread = isAiming ? weaponReference.Id.baseAimSpread : weaponReference.Id.baseSpread;
            UpdateSpread(newSpread);
        }
        /// <summary>
        /// 写入新的散布值：仅当当前武器不为空且启用了子弹散布（applyBulletSpread）时才生效。
        /// </summary>
        /// <param name="spread">要写入的散布值。</param>
        public void UpdateSpread(float spread)
        {
            if (weaponReference.Weapon != null && weaponReference.Weapon.applyBulletSpread)
                this.spread = spread;
        }

        /// <summary>返回当前生效的散布值。</summary>
        public float GetCurrentSpread() => spread;
    }

}