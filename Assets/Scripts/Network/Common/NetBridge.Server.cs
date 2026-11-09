using System;
using Mirror;
using Network.Common.Messages;
using UnityEngine;

namespace Network.Common
{
    public sealed partial class NetBridge : MonoBehaviour
    {
        public event Action<NetworkConnectionToClient, PlayerInputMessage> PlayerInputReceived;
        public event Action<NetworkConnectionToClient, HelloRequest> ServerHelloReceived;
        public event Action<NetworkConnectionToClient, PredictionResyncRequest> PredictionResyncRequested;

        public void StartServer()
        {
            if (_serverHandlersRegistered)
                return;

            NetworkServer.RegisterHandler<HelloRequest>(HandleServerHello);
            NetworkServer.RegisterHandler<PlayerInputMessage>(HandlePlayerInput);
            NetworkServer.RegisterHandler<PredictionResyncRequest>(HandlePredictionResync);
            _serverHandlersRegistered = true;
        }

        public void StopServer()
        {
            if (!_serverHandlersRegistered)
                return;

            NetworkServer.UnregisterHandler<HelloRequest>();
            NetworkServer.UnregisterHandler<PlayerInputMessage>();
            NetworkServer.UnregisterHandler<PredictionResyncRequest>();
            _serverHandlersRegistered = false;
        }

        public bool SendHelloToClient(NetworkConnectionToClient connection, HelloResponse response)
        {
            if (connection == null || !NetworkServer.active)
            {
                Debug.LogWarning("[NetBridge] 服务器或目标连接不可用，握手响应未发送。");
                return false;
            }

            connection.Send(response);
            return true;
        }

        public void SendPlayerSnapshot(ref PlayerSnapshotMessage message)
        {
            NetworkServer.SendToAll(message, Channels.Unreliable);
        }

        /// <summary>将服务器确认的瞬时事件可靠、有序地发送给所有 Ready 客户端。</summary>
        public bool BroadcastEvent(NetworkEventMessage message)
        {
            if (!NetworkServer.active || message == null)
                return false;

            NetworkServer.SendToAll(new NetworkEventEnvelope { payload = message },
                Channels.Reliable, sendToReadyOnly: true);
            return true;
        }

        public bool SendPredictionBaseline(NetworkConnectionToClient connection, PredictionBaselineMessage message)
        {
            if (connection == null || !NetworkServer.active)
                return false;

            connection.Send(message, Channels.Reliable);
            return true;
        }

        void HandleServerHello(NetworkConnectionToClient connection, HelloRequest request)
        {
            ServerHelloReceived?.Invoke(connection, request);
        }

        void HandlePlayerInput(NetworkConnectionToClient connection, PlayerInputMessage message)
        {
            PlayerInputReceived?.Invoke(connection, message);
        }

        void HandlePredictionResync(NetworkConnectionToClient connection, PredictionResyncRequest request)
        {
            PredictionResyncRequested?.Invoke(connection, request);
        }
    }
}
