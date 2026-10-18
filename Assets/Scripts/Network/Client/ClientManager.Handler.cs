using System;
using Core.ZLog;
using Network.Common;
using Network.Common.Messages;
using UnityEngine;

namespace Network.Client
{
    public sealed partial class ClientManager : MonoBehaviour
    {
        private void HandleHelloResponse(HelloResponse response)
        {
            if (!response.accepted)
            {
                Debug.LogError(
                    $"[FPSNetworkManager] 服务器拒绝协议握手：{response.message} " +
                    $"(serverProtocol={response.protocolVersion})");
                return;
            }

            Debug.Log(
                $"[FPSNetworkManager] 协议握手完成：{response.message} " +
                $"(protocol={response.protocolVersion})");
        }
    }
}
