using System;
using System.Collections.Generic;
using Core.ZLog;
using Mirror;
using Network.Common;
using Network.Common.Messages;
using Network.Server.Session;
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

        /// <summary>当前服务器进程中的会话注册表。</summary>
        public SessionRegistry Sessions { get; } = new();

        /// <summary>当前连接数量。</summary>
        public int ClientCount => Sessions.Count;

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
            _initialized = true;

            ZLog.Log<ServerManager>("服务器应用层已初始化");
        }

        /// <summary>
        /// 登记 Mirror 新连接。重复 connectionId 会被拒绝。
        /// </summary>
        public bool RegisterConnection(NetworkConnectionToClient connection)
        {
            EnsureInitialized();

            if (!Sessions.TryAdd(connection, out ClientSession session))
            {
                Debug.LogError($"[ServerManager] 无法登记重复或无效连接：{connection}");
                return false;
            }

            Debug.Log($"[ServerManager] 已建立 {session}。");
            return true;
        }

        /// <summary>
        /// 将会话推进到 Ready。没有完成协议握手的连接不能进入游戏世界。
        /// </summary>
        public bool MarkClientReady(NetworkConnectionToClient connection)
        {
            EnsureInitialized();

            if (!Sessions.TryGet(connection, out ClientSession session) || !session.MarkReady())
            {
                Debug.LogWarning($"[ServerManager] 拒绝 Ready:连接尚未完成协议握手。connection={connection}");
                return false;
            }

            Debug.Log($"[ServerManager] 客户端已 Ready:{session}");
            return true;
        }

        /// <summary>
        /// 把 Mirror 生成的玩家实体绑定到会话。
        /// </summary>
        public bool AttachPlayer(NetworkConnectionToClient connection, NetworkIdentity playerIdentity)
        {
            EnsureInitialized();

            if (!Sessions.TryGet(connection, out ClientSession session) ||
                !session.AttachPlayer(playerIdentity))
            {
                ZLog.LogError<ServerManager>($" 玩家绑定失败:会话阶段不正确或实体为空。connection={connection}");
                return false;
            }

            ZLog.Log<ServerManager>($" 玩家实体已绑定：{session}, netId={playerIdentity.netId}");
            return true;
        }

        /// <summary>
        /// 在 Mirror 销毁玩家对象之前移除会话索引。
        /// </summary>
        public void UnregisterConnection(NetworkConnectionToClient connection)
        {
            if (!_initialized)
            {
                return;
            }

            if (Sessions.TryRemove(connection, out ClientSession session))
            {
                _lastCommandSequence.Remove(connection.connectionId);
                Debug.Log($"[ServerManager] 已移除客户端会话：{session}");
            }
        }

        /// <summary>
        /// 停止接收协议消息并清理所有进程内会话。
        /// </summary>
        public void Shutdown()
        {
            if (!_initialized)
            {
                return;
            }

            _bridge.ServerHelloReceived -= HandleHelloRequest;
            _bridge.PlayerCommandReceived -= HandlePlayerCommand;
            _lastCommandSequence.Clear();
            Sessions.Clear();
            _bridge = null;
            _initialized = false;
            Debug.Log("[ServerManager] 服务器应用层已关闭。");
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