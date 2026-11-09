using UnityEngine;

namespace GamePlay.HoldSystem
{
    /// <summary>配置 AK47 参数并创建独立枪械。</summary>
    [CreateAssetMenu(fileName = "AK47SO", menuName = "Scriptable Objects/Weapon/Gun/AK47SO")]
    public class AK47SO : GunSO
    {
        public override WeaponSlot Slot => WeaponSlot.Rifle;
        public override WeaponBase CreateWeapon() => new AK47(CreateConfig());
                
    }
}
