using System;
using Mirror;
using Network.Common.Messages;
using UnityEngine;

namespace Network.Common
{
    public sealed partial class NetBridge : MonoBehaviour
    {

        /// <summary>客户端收到服务器握手响应时触发。</summary>
        public event Action<HelloResponse> ClientHelloReceived;

        /// <summary>
        /// 注册客户端消息入口。Host 模式会同时注册客户端和服务端入口，这是正常行为。
        /// </summary>
        public void StartClient()
        {
            if (_clientHandlersRegistered)
            {
                return;
            }
           
            NetworkClient.RegisterHandler<HelloResponse>(HandleClientHello);
            NetworkClient.RegisterHandler<PlayerSnapshotMessage>(HandlePlayerSnapshot);
            _clientHandlersRegistered = true;
        }

        

        /// <summary>注销由本组件注册的客户端消息入口。</summary>
        public void StopClient()
        {
            if (!_clientHandlersRegistered)
            {
                return;
            }

            NetworkClient.UnregisterHandler<HelloResponse>();
            _clientHandlersRegistered = false;
        }

        


        private static string SanitizeClientVersion(string clientVersion)
        {
            if (string.IsNullOrWhiteSpace(clientVersion))
            {
                return "unknown";
            }

            string trimmed = clientVersion.Trim();
            return trimmed.Length <= NetworkProtocol.MaxClientVersionLength
                ? trimmed
                : trimmed.Substring(0, NetworkProtocol.MaxClientVersionLength);
        }


        #region CommandSender
        /// <summary> 客户端向当前服务器发送协议握手。</summary>
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

        /// <summary> 本地玩家把一个采样窗口内的操作命令发给服务器,走不可靠通道 </summary>
        public bool SendPlayerCommand(PlayerCommandMessage command)
        {
            if (!NetworkClient.isConnected)
            {
                return false;
            }

            NetworkClient.Send(command, Channels.Unreliable);
            return true;
        }
        #endregion

        
        #region CommandHandler
        private void HandleClientHello(HelloResponse response)
        {
            ClientHelloReceived?.Invoke(response);
        }


        private void HandlePlayerSnapshot(PlayerSnapshotMessage message)
        {
            
        }
        #endregion
    }
}
