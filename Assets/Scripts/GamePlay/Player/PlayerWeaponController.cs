using System;
using Core.Utility;
using GamePlay.Player.Simulation;
using UnityEngine;

namespace GamePlay.Player
{
    public sealed class PlayerWeaponController : MonoBehaviour
    {
        public event Action<WeaponInputAction> OnWeaponInvoked;

        public void Present(in PlayerStepResult result)
        {
            if (result.fired)
                OnWeaponInvoked?.Invoke(WeaponInputAction.MLeft);
            if (result.key1)
                OnWeaponInvoked?.Invoke(WeaponInputAction.Key1);
            if (result.key2)
                OnWeaponInvoked?.Invoke(WeaponInputAction.Key2);
            if (result.key3)
                OnWeaponInvoked?.Invoke(WeaponInputAction.Key3);
            if (result.jumped)
                OnWeaponInvoked?.Invoke(WeaponInputAction.Space);
        }
    }
}
