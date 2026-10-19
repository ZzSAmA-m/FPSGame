using System;
using Core.Utility;
using Core.ZLog;
using Mirror;
using Network.Common;
using UnityEngine;

namespace Network.Client
{
    public sealed partial class ClientManager : Singleton<ClientManager>
    {
        public int ClientId{get;private set;}
        private NetBridge _bridge;

        public void Initialize(NetBridge bridge)
        {

            _bridge = bridge ?? throw new ArgumentNullException(nameof(bridge));


            _bridge.ClientHelloReceived += HandleHelloResponse;
            _bridge.GetClientIdResultReceived += HandleGetClientId;
            _bridge.PlayerSnapshotReceived+=HandlePlayerSnapshot;
            ZLog.Log<ClientManager>("客户端应用层已初始化");
        }
        public void ShutDown()
        {
            _bridge.ClientHelloReceived -= HandleHelloResponse;
            _bridge.GetClientIdResultReceived -= HandleGetClientId;
            _bridge.PlayerSnapshotReceived-=HandlePlayerSnapshot;
        }
    }
}
