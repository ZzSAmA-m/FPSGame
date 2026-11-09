using UnityEngine;

namespace GamePlay.HoldSystem
{
    /// <summary>刀具的配置和模型入口，每名角色获得独立逻辑实例。</summary>
    [CreateAssetMenu(fileName = "KnifeSO", menuName = "Scriptable Objects/Weapon/KnifeSO")]
    public sealed class KnifeSO : WeaponSO
    {
        public override WeaponSlot Slot => WeaponSlot.Knife;
        public override WeaponBase CreateWeapon() => new Knife();
    }
}
