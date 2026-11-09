using System;
using GamePlay.HoldSystem;

namespace GamePlay.Player
{
    /// <summary>装备栏拥有的武器；切换手持物时保留该实例的弹药和运行状态。</summary>
    public sealed class WeaponEntry
    {
        public WeaponSO Definition { get; }
        public WeaponBase Weapon { get; }
        public WeaponSlot Slot { get; }
        public IWeaponPresentationResources PresentationResources => Definition.PresentationResources;

        internal WeaponEntry(WeaponSO definition, WeaponBase weapon)
        {
            if (definition == null)
                throw new ArgumentNullException(nameof(definition));
            Definition = definition;
            Weapon = weapon ?? throw new ArgumentNullException(nameof(weapon));
            // 进入装备栏时确定分类，避免定义的后续变化破坏栏位约束。
            Slot = definition.Slot;
        }
    }
}
