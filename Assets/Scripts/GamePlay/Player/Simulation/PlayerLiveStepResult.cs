using System;
using System.Collections.Generic;
using GamePlay.HoldSystem;
using UnityEngine;

namespace GamePlay.Player.Simulation
{
    /// <summary>玩家实时输入门面完成一拍后的事实；不参与运动回滚或包含网络元数据。</summary>
    public readonly struct PlayerLiveStepResult
    {
        public PlayerSimState State { get; }
        public bool Fired { get; }
        /// <summary>仅在 Fired 为 true 时有效。</summary>
        public Vector3 FireOrigin { get; }
        /// <summary>仅在 Fired 为 true 时有效。</summary>
        public Vector3 FireDirection { get; }
        /// <summary>仅在 Fired 为 true 时有效，记录本次实际发射所用的栏位。</summary>
        public WeaponSlot FireWeaponSlot { get; }
        /// <summary>本拍输入完成后实际切换到的栏位，未发生有效切换时为空。</summary>
        public WeaponSlot? SwitchedWeaponSlot { get; }
        public IReadOnlyList<PlayerWeaponAction> WeaponActions { get; }

        public PlayerLiveStepResult(in PlayerSimState state, bool fired, Vector3 fireOrigin, Vector3 fireDirection,
            WeaponSlot fireWeaponSlot = WeaponSlot.Rifle, WeaponSlot? switchedWeaponSlot = null,
            IReadOnlyList<PlayerWeaponAction> weaponActions = null)
        {
            State = state;
            Fired = fired;
            FireOrigin = fireOrigin;
            FireDirection = fireDirection;
            FireWeaponSlot = fireWeaponSlot;
            SwitchedWeaponSlot = switchedWeaponSlot;
            WeaponActions = weaponActions ?? Array.Empty<PlayerWeaponAction>();
        }
    }
}
