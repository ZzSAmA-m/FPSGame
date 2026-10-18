using System;
using Core.ZLog;
using Network.Common;
using UnityEngine;

namespace Network.Client
{
    public sealed partial class ClientManager : MonoBehaviour
    {
        private NetBridge _bridge;

        public void Initialize(NetBridge bridge)
        {

            _bridge = bridge ?? throw new ArgumentNullException(nameof(bridge));

            _bridge.ClientHelloReceived += HandleHelloResponse;

            ZLog.Log<ClientManager>("客户端应用层已初始化");
        }
        public void ShutDown()
        {
            _bridge.ClientHelloReceived -= HandleHelloResponse;
        }
    }
}
