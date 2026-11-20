using System;
using Mirror;
using Network.Common.Messages;
using UnityEngine;

namespace Network.Common
{
    public sealed partial class NetBridge : MonoBehaviour
    {
        public event Action<HelloResponse> ClientHelloReceived;
        public event Action<PlayerSnapshotMessage> PlayerSnapshotReceived;
        public event Action<PredictionBaselineMessage> PredictionBaselineReceived;
        public event Action<NetworkEventMessage> NetworkEventReceived;
        public event Action<PlayerStatsStateMessage> PlayerStatsStateReceived;
        public event Action<PlayerLifeStateMessage> PlayerLifeStateReceived;
        public event Action<HitConfirmMessage> HitConfirmReceived;

        public void StartClient()
        {
            if (_clientHandlersRegistered)
                return;

            NetworkClient.RegisterHandler<HelloResponse>(HandleClientHello);
            NetworkClient.RegisterHandler<PlayerSnapshotMessage>(HandlePlayerSnapshot);
            NetworkClient.RegisterHandler<PredictionBaselineMessage>(HandlePredictionBaseline);
            NetworkClient.RegisterHandler<NetworkEventEnvelope>(HandleNetworkEvent);
            NetworkClient.RegisterHandler<PlayerStatsStateMessage>(HandlePlayerStatsState);
            NetworkClient.RegisterHandler<PlayerLifeStateMessage>(HandlePlayerLifeState);
            NetworkClient.RegisterHandler<HitConfirmMessage>(HandleHitConfirm);
            _clientHandlersRegistered = true;
        }

        public void StopClient()
        {
            if (!_clientHandlersRegistered)
                return;

            NetworkClient.UnregisterHandler<HelloResponse>();
            NetworkClient.UnregisterHandler<PlayerSnapshotMessage>();
            NetworkClient.UnregisterHandler<PredictionBaselineMessage>();
            NetworkClient.UnregisterHandler<NetworkEventEnvelope>();
            NetworkClient.UnregisterHandler<PlayerStatsStateMessage>();
            NetworkClient.UnregisterHandler<PlayerLifeStateMessage>();
            NetworkClient.UnregisterHandler<HitConfirmMessage>();
            _clientHandlersRegistered = false;
        }

        public bool SendHelloToServer(string clientVersion)
        {
            if (!NetworkClient.isConnected)
            {
                Debug.LogWarning("[NetBridge] 客户端尚未连接，握手消息未发送。");
                return false;
            }

            NetworkClient.Send(new HelloRequest
            {
                protocolVersion = NetworkProtocol.Version,
                clientVersion = SanitizeClientVersion(clientVersion)
            });
            return true;
        }

        public bool SendPlayerInput(ref PlayerInputMessage message)
        {
            if (!NetworkClient.isConnected)
                return false;

            NetworkClient.Send(message, Channels.Unreliable);
            return true;
        }

        public bool SendPredictionResyncRequest(PredictionResyncRequest request)
        {
            if (!NetworkClient.isConnected)
                return false;

            NetworkClient.Send(request, Channels.Reliable);
            return true;
        }

        static string SanitizeClientVersion(string clientVersion)
        {
            if (string.IsNullOrWhiteSpace(clientVersion))
                return "unknown";

            string trimmed = clientVersion.Trim();
            return trimmed.Length <= NetworkProtocol.MaxClientVersionLength
                ? trimmed
                : trimmed.Substring(0, NetworkProtocol.MaxClientVersionLength);
        }

        void HandleClientHello(HelloResponse response)
        {
            ClientHelloReceived?.Invoke(response);
        }

        void HandlePlayerSnapshot(PlayerSnapshotMessage message)
        {
            PlayerSnapshotReceived?.Invoke(message);
        }

        void HandlePredictionBaseline(PredictionBaselineMessage message)
        {
            PredictionBaselineReceived?.Invoke(message);
        }

        void HandleNetworkEvent(NetworkEventEnvelope message)
        {
            NetworkEventReceived?.Invoke(message.payload);
        }

        void HandlePlayerStatsState(PlayerStatsStateMessage message)
        {
            PlayerStatsStateReceived?.Invoke(message);
        }

        void HandlePlayerLifeState(PlayerLifeStateMessage message)
        {
            PlayerLifeStateReceived?.Invoke(message);
        }

        void HandleHitConfirm(HitConfirmMessage message)
        {
            HitConfirmReceived?.Invoke(message);
        }
    }
}
