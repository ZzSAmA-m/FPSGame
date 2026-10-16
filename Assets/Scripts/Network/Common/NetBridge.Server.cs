using Mirror;
using Network.Common.Messages;
using UnityEngine;

namespace Network.Common
{
    public sealed partial class NetBridge : MonoBehaviour
    {
        /// <summary>
        /// 注册服务端消息入口。重复调用是安全的，不会重复覆盖 Handler。
        /// </summary>
        public void StartServer()
        {
            if (_serverHandlersRegistered)
            {
                return;
            }

            // 普通游戏消息默认要求连接已经通过 Mirror 认证。
            // 当前项目没有自定义 Authenticator，Mirror 会在连接阶段自动完成基础认证。
            NetworkServer.RegisterHandler<HelloRequest>(HandleServerHello);
            NetworkServer.RegisterHandler<PlayerCommandMessage>(HandlePlayerCommand);
            _serverHandlersRegistered = true;
        }

        /// <summary>注销由本组件注册的服务端消息入口。</summary>
        public void StopServer()
        {
            if (!_serverHandlersRegistered)
            {
                return;
            }

            NetworkServer.UnregisterHandler<HelloRequest>();
            NetworkServer.UnregisterHandler<PlayerCommandMessage>();
            _serverHandlersRegistered = false;
        }

        /// <summary>
        /// 向指定客户端发送协议握手结果。
        /// </summary>
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

        private void HandleServerHello(NetworkConnectionToClient connection, HelloRequest request)
        {
            ServerHelloReceived?.Invoke(connection, request);
        }

        private void HandlePlayerCommand(NetworkConnectionToClient connection, PlayerCommandMessage command)
        {
            PlayerCommandReceived?.Invoke(connection, command);
        }
    }
}
