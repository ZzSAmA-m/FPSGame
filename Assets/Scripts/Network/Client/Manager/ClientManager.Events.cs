using System;
using GamePlay.Player.Combat;
using GamePlay.Player.Simulation.Reconciliation;
using Mirror;
using Network.Common.Messages;

namespace Network.Client
{
    public sealed partial class ClientManager
    {
        /// <summary>
        /// 每个服务器事件最多通知一次，包含本地玩家的开火确认。
        /// 远端表现由事件处理器先行分发，观察者可通过 netId 区分本地确认和远端事件。
        /// </summary>
        public event Action<NetworkEventMessage> EventReceived;
        /// <summary>每次有效的服务器命中广播通知一次。</summary>
        public event Action<HitConfirmMessage> HitReceived;
        /// <summary>射手为本地玩家时的命中确认。</summary>
        public event Action<HitConfirmMessage> HitConfirmed;
        private ulong _lastHitConfirmShotId;

        void HandleNetworkEvent(NetworkEventMessage message)
        {
            if (_eventMessageHandler == null || !_eventMessageHandler.Handle(message))
                return;

            EventReceived?.Invoke(message);
        }

        void HandleHitConfirm(HitConfirmMessage message)
        {
            if (message.shotId <= _lastHitConfirmShotId || message.shooterNetId == 0 || message.targetNetId == 0 ||
                message.shooterNetId == message.targetNetId ||
                (message.region != HitRegion.Head && message.region != HitRegion.Body) ||
                !PlayerStateValidation.IsFinite(message.point))
                return;

            _lastHitConfirmShotId = message.shotId;
            bool isLocalShooter = NetworkClient.localPlayer != null &&
                NetworkClient.localPlayer.netId == message.shooterNetId;
            HitReceived?.Invoke(message);
            if (isLocalShooter)
                HitConfirmed?.Invoke(message);
        }
    }
}
