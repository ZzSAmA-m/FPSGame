using GamePlay.HoldSystem;
using GamePlay.Player.Simulation;
using UnityEngine;

namespace Network.Server
{
    /// <summary>服务器玩法处理完成后产生的事实及其来源标识。</summary>
    public abstract class ServerGameEvent
    {
        public uint ServerTick { get; }
        public uint PlayerId { get; }
        public uint PredictionEpoch { get; }
        public uint InputTick { get; }

        protected ServerGameEvent(uint serverTick, uint playerId, uint predictionEpoch, uint inputTick)
        {
            ServerTick = serverTick;
            PlayerId = playerId;
            PredictionEpoch = predictionEpoch;
            InputTick = inputTick;
        }
    }

    /// <summary>服务器枪械已成功扣弹发射后生成的开火事实。</summary>
    public sealed class ShotFiredEvent : ServerGameEvent
    {
        public Vector3 Position { get; }
        public Vector3 Origin { get; }
        public Vector3 Direction { get; }
        public WeaponSlot WeaponSlot { get; }

        public ShotFiredEvent(uint serverTick, in ServerPlayerInput input, in PlayerLiveStepResult result,
            WeaponSlot? weaponSlot = null, Vector3? origin = null, Vector3? direction = null)
            : base(serverTick, input.PlayerId, input.PredictionEpoch, input.Frame.tick)
        {
            Position = result.State.position;
            Origin = origin ?? result.FireOrigin;
            Direction = direction ?? result.FireDirection;
            WeaponSlot = weaponSlot ?? result.FireWeaponSlot;
        }
    }

    /// <summary>权威刀具进入轻击或重击状态。</summary>
    public sealed class KnifeAttackStartedEvent : ServerGameEvent
    {
        public WeaponSlot WeaponSlot { get; }
        public KnifeAttackKind AttackKind { get; }

        public KnifeAttackStartedEvent(uint serverTick, in ServerPlayerInput input, WeaponSlot slot, KnifeAttackKind attackKind)
            : base(serverTick, input.PlayerId, input.PredictionEpoch, input.Frame.tick)
        {
            WeaponSlot = slot;
            AttackKind = attackKind;
        }
    }

    /// <summary>服务器输入已成功切换当前装备栏位后生成的事实。</summary>
    public sealed class WeaponSwitchedEvent : ServerGameEvent
    {
        public WeaponSlot WeaponSlot { get; }
        public bool ServerInitiated { get; }

        public WeaponSwitchedEvent(uint serverTick, in ServerPlayerInput input, WeaponSlot weaponSlot,
            bool serverInitiated = false)
            : base(serverTick, input.PlayerId, input.PredictionEpoch, input.Frame.tick)
        {
            WeaponSlot = weaponSlot;
            ServerInitiated = serverInitiated;
        }
    }

    /// <summary>权威武器开始装备准备。</summary>
    public sealed class PrepareStartedEvent : ServerGameEvent
    {
        public WeaponSlot WeaponSlot { get; }
        public bool ServerInitiated { get; }

        public PrepareStartedEvent(uint serverTick, in ServerPlayerInput input, WeaponSlot weaponSlot,
            bool serverInitiated = false)
            : base(serverTick, input.PlayerId, input.PredictionEpoch, input.Frame.tick)
        {
            WeaponSlot = weaponSlot;
            ServerInitiated = serverInitiated;
        }
    }

    /// <summary>服务器枪械已成功开始换弹后产生的事实。</summary>
    public sealed class ReloadStartedEvent : ServerGameEvent
    {
        public WeaponSlot WeaponSlot { get; }
        public bool ServerInitiated { get; }

        public ReloadStartedEvent(uint serverTick, in ServerPlayerInput input, WeaponSlot weaponSlot,
            bool serverInitiated = false)
            : base(serverTick, input.PlayerId, input.PredictionEpoch, input.Frame.tick)
        {
            WeaponSlot = weaponSlot;
            ServerInitiated = serverInitiated;
        }
    }

    /// <summary>服务器枪械动作提前退出后产生的取消事实。</summary>
    public sealed class WeaponActionCanceledEvent : ServerGameEvent
    {
        public WeaponSlot WeaponSlot { get; }
        public GunState CanceledState { get; }
        public bool ServerInitiated { get; }

        public WeaponActionCanceledEvent(uint serverTick, in ServerPlayerInput input,
            WeaponSlot weaponSlot, GunState canceledState, bool serverInitiated = false)
            : base(serverTick, input.PlayerId, input.PredictionEpoch, input.Frame.tick)
        {
            WeaponSlot = weaponSlot;
            CanceledState = canceledState;
            ServerInitiated = serverInitiated;
        }
    }
}
