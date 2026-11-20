using System;
using System.Collections.Generic;
using Network.Common;
using Network.Common.Messages;
using UnityEngine;

namespace Network.Server
{
    /// <summary>将服务器玩法事实转换为网络事件，并维护本次服务器运行的全局事件序号。</summary>
    public sealed class ServerEventPublisher
    {
        private readonly NetBridge _bridge;

        public ulong LastEventId { get; private set; }

        public ServerEventPublisher(NetBridge bridge)
        {
            _bridge = bridge ?? throw new ArgumentNullException(nameof(bridge));
        }

        public void Publish(IReadOnlyList<ServerGameEvent> events)
        {
            if (events == null)
                throw new ArgumentNullException(nameof(events));

            foreach (ServerGameEvent gameEvent in events)
            {
                NetworkEventMessage message = CreateMessage(gameEvent);
                _bridge.BroadcastEvent(message);
#if UNITY_EDITOR
                if (message is FireEventMessage fire)
                    Debug.DrawRay(fire.origin, fire.direction * 100f, Color.red, 3f, false);
#endif
            }
        }

        private NetworkEventMessage CreateMessage(ServerGameEvent gameEvent)
        {
            if (gameEvent == null)
                throw new ArgumentNullException(nameof(gameEvent));
            NetworkEventMessage message = gameEvent switch
            {
                ShotFiredEvent shot => new FireEventMessage
                {
                    predictionEpoch = shot.PredictionEpoch,
                    inputTick = shot.InputTick,
                    weaponSlot = shot.WeaponSlot,
                    position = shot.Position,
                    origin = shot.Origin,
                    direction = shot.Direction
                },
                WeaponSwitchedEvent weaponSwitch => new WeaponSwitchEventMessage
                {
                    predictionEpoch = weaponSwitch.PredictionEpoch,
                    inputTick = weaponSwitch.InputTick,
                    weaponSlot = weaponSwitch.WeaponSlot,
                    serverInitiated = weaponSwitch.ServerInitiated
                },
                KnifeAttackStartedEvent attack => new KnifeAttackEventMessage
                {
                    predictionEpoch = attack.PredictionEpoch,
                    inputTick = attack.InputTick,
                    weaponSlot = attack.WeaponSlot,
                    attackKind = attack.AttackKind
                },
                PrepareStartedEvent prepare => new PrepareEventMessage
                {
                    predictionEpoch = prepare.PredictionEpoch,
                    inputTick = prepare.InputTick,
                    weaponSlot = prepare.WeaponSlot,
                    serverInitiated = prepare.ServerInitiated
                },
                ReloadStartedEvent reload => new ReloadEventMessage
                {
                    predictionEpoch = reload.PredictionEpoch,
                    inputTick = reload.InputTick,
                    weaponSlot = reload.WeaponSlot,
                    serverInitiated = reload.ServerInitiated
                },
                WeaponActionCanceledEvent canceled => new WeaponActionCanceledEventMessage
                {
                    predictionEpoch = canceled.PredictionEpoch,
                    inputTick = canceled.InputTick,
                    weaponSlot = canceled.WeaponSlot,
                    canceledState = canceled.CanceledState,
                    serverInitiated = canceled.ServerInitiated
                },
                _ => throw new ArgumentException($"不支持的服务器玩法事件类型 {gameEvent.GetType().FullName}。", nameof(gameEvent))
            };
            message.eventId = ++LastEventId;
            message.serverTick = gameEvent.ServerTick;
            message.netId = gameEvent.PlayerId;
            return message;
        }
    }
}
