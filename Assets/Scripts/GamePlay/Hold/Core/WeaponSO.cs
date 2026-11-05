using Sirenix.OdinInspector;
using UnityEngine;
namespace GamePlay.HoldSystem
{
    public class WeaponSO : HoldableSO
    {
        public WeaponBase Weapon{get;protected set;}
        public override IHoldable Item { get => Weapon;}
    }

}
