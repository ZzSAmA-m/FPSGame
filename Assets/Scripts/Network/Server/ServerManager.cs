using System;
using System.Collections.Generic;
using Core.ZLog;
using Mirror;
using Network.Client;
using Network.Common;
using Network.Common.Messages;
using UnityEngine;

namespace Network.Server
{
    /// <summary>
    /// 比赛服务器的应用层入口，负责编排连接会话与协议握手。
    /// </summary>
    /// <remarks>
    /// 这个类不再保存玩家 NetworkBehaviour 列表，也不通过全局 EventCenter 接收网络事件。
    /// FPSNetworkManager 会直接把 Mirror 生命周期回调转交到这里，使调用顺序清晰、可追踪。
    /// </remarks>
    public sealed partial class ServerManager : MonoBehaviour
    {
        private NetBridge _bridge;
        private bool _initialized;
        private readonly Dictionary<int, uint> _lastCommandSequence = new();


        public ClientSessions Sessions{get;private set;}=new();


        /// <summary>
        /// 注入网络边界并开始接收协议消息。
        /// </summary>
        public void Initialize(NetBridge bridge)
        {
            if (_initialized)
            {
                throw new InvalidOperationException("[ServerManager] 不能重复初始化服务器运行时。");
            }

            _bridge = bridge ??throw new ArgumentNullException(nameof(bridge));
            _bridge.ServerHelloReceived += HandleHelloRequest;
            _bridge.PlayerCommandReceived += HandlePlayerCommand;
            _bridge.GetClientIdMsgReceived += HandleGetClientIdMsg;
            _initialized = true;

            ZLog.Log<ServerManager>("服务器应用层已初始化");
        }

        
        public void Shutdown()
        {
            if (!_initialized)
            {
                return;
            }

            _bridge.ServerHelloReceived -= HandleHelloRequest;
            _bridge.PlayerCommandReceived -= HandlePlayerCommand;
            
            _lastCommandSequence.Clear();
            _bridge = null;
            _initialized = false;
            Debug.Log("[ServerManager] 服务器应用层已关闭。");
        }

        public void AddClient(NetworkConnectionToClient conn)
        {
            Sessions.AddClientSession(conn);
        }

        

        private void RejectAndDisconnect(NetworkConnectionToClient connection, string reason)
        {
            Debug.LogWarning($"[ServerManager] 拒绝连接 {connection}:{reason}");

            _bridge.SendHelloToClient(connection, new HelloResponse
            {
                protocolVersion = NetworkProtocol.Version,
                accepted = false,
                message = reason
            });

            // 不兼容客户端继续发送消息只会产生更多错误，因此在给出原因后立即断开。
            connection?.Disconnect();
        }

        private static string SanitizeClientVersion(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "unknown";
            }

            string trimmed = value.Trim();
            return trimmed.Length <= NetworkProtocol.MaxClientVersionLength
                ? trimmed
                : trimmed.Substring(0, NetworkProtocol.MaxClientVersionLength);
        }

        private void EnsureInitialized()
        {
            if (!_initialized)
            {
                throw new InvalidOperationException("[ServerManager] 尚未通过 Initialize 完成初始化。");
            }
        }

        private void OnDestroy()
        {
            // 既覆盖正常 StopServer，也覆盖场景被意外卸载的情况。
            Shutdown();
        }
    }
}