// WeaponWeightSystem.cs：武器重量系统。
// 将当前武器的重量系数（weightMultiplier，含附件加成）应用到玩家的重量乘数（WeightMultiplier），
// 从而影响移动速度等受重量影响的参数；在武器掏出/释放/取消选中时刷新该系数。
using UnityEngine;

namespace cowsins
{
    /// <summary>
    /// 武器重量系统：根据当前武器（含附件加成）的重量系数更新玩家的重量乘数，进而影响移动等表现。
    /// </summary>
    public class WeaponWeightSystem
    {
        // 武器上下文，聚合依赖项与设置。
        private WeaponContext context;
        // 武器引用提供者，提供当前武器及其重量系数。
        private IWeaponReferenceProvider weaponReference;
        // 武器事件提供者，用于订阅武器掏出/释放等事件。
        private IWeaponEventsProvider weaponEvents;
        // 玩家乘数集合，其中的重量乘数会受当前武器重量影响。
        private IPlayerMultipliers playerMultipliers;

        // 是否启用了“Inventory Pro Manager”附加包；启用时重量由背包系统计算，本系统不干预。
        private bool isAddonAvailable;

        // 当前武器数据（Weapon_SO）快捷属性。
        private Weapon_SO weapon => weaponReference.Weapon;

        /// <summary>
        /// 构造函数：缓存依赖引用，订阅武器掏出/释放/取消选中事件以刷新重量系数，并立即执行一次刷新。
        /// </summary>
        /// <param name="context">武器上下文，提供依赖项与设置。</param>
        public WeaponWeightSystem(WeaponContext context)
        {
            this.context = context;
            this.weaponReference = context.Dependencies.WeaponReference;
            this.weaponEvents = context.Dependencies.WeaponEvents;
            this.playerMultipliers = context.Dependencies.PlayerMultipliers;

            weaponEvents.Events.OnUnholster.AddListener(GetWeaponWeightModifierOnUnholster);
            weaponEvents.Events.OnReleaseWeapon.AddListener(GetWeaponWeightModifier);
            weaponEvents.Events.OnUnselectingWeapon.AddListener(GetWeaponWeightModifier);

            GetWeaponWeightModifier();
        }

        // 掏出武器时回调：直接刷新重量系数（两个参数仅为匹配事件签名，未使用）。
        private void GetWeaponWeightModifierOnUnholster(bool prop, bool prop2) => GetWeaponWeightModifier();
        /// <summary>
        /// 刷新玩家的重量乘数：优先读取 weaponReference.Id 的重量系数（含附件加成），
        /// 若没有已装备武器则回退到 Weapon_SO 的重量系数，仍无则设为默认值 1。启用背包附加包时跳过。
        /// </summary>
        public void GetWeaponWeightModifier()
        {
            // Only apply the weight modification if the Inventory Pro Manager add-on is not available. If it is available, the weight of the player is calculated by the inventory
            if (isAddonAvailable) return;

            // Read from weaponReference.Id to ensure attachment modifiers are included
            if (weaponReference.Id != null)
                playerMultipliers.WeightMultiplier.BaseValue = weaponReference.Id.weightMultiplier;
            else
                playerMultipliers.WeightMultiplier.BaseValue = weapon != null ? weapon.weightMultiplier : 1;

        }
    }

}
