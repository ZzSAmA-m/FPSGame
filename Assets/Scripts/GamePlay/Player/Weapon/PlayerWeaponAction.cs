using GamePlay.HoldSystem;
using UnityEngine;

namespace GamePlay.Player
{
    public enum PlayerWeaponActionKind
    {
        Switched,
        PrepareStarted,
        ReloadStarted,
        Canceled,
        Shot,
        KnifeAttack
    }

    /// <summary>按实际产生顺序保存的权威武器事实，栏位在产生时捕获。</summary>
    public readonly struct PlayerWeaponAction
    {
        public readonly PlayerWeaponActionKind Kind;
        public readonly WeaponSlot Slot;
        public readonly GunState CanceledState;
        public readonly Vector3 FireOrigin;
        public readonly Vector3 FireDirection;
        public readonly bool ServerInitiated;
        public readonly KnifeAttackKind KnifeAttackKind;

        public PlayerWeaponAction(PlayerWeaponActionKind kind, WeaponSlot slot, GunState canceledState = GunState.None,
            Vector3 fireOrigin = default, Vector3 fireDirection = default, bool serverInitiated = false,
            KnifeAttackKind knifeAttackKind = default)
        {
            Kind = kind;
            Slot = slot;
            CanceledState = canceledState;
            FireOrigin = fireOrigin;
            FireDirection = fireDirection;
            ServerInitiated = serverInitiated;
            KnifeAttackKind = knifeAttackKind;
        }
    }

    public readonly struct PlayerWeaponStepResult
    {
        public bool Fired { get; }
        public bool ReloadStarted { get; }

        public PlayerWeaponStepResult(bool fired, bool reloadStarted)
        {
            Fired = fired;
            ReloadStarted = reloadStarted;
        }
    }
}
