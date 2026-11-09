using GamePlay.HoldSystem;
using NUnit.Framework.Constraints;
using UnityEngine;
namespace GamePlay.HoldSystem
{
    public sealed class PickableWeapon : MonoBehaviour,IPickable
    {
        public WeaponSO WeaponSO=>_weaponSO;

        [SerializeField]private WeaponSO _weaponSO; 
        
    }

}
