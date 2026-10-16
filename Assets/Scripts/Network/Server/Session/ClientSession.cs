using System;
using Mirror;

namespace Network.Server.Session
{
    /// <summary>
    /// 服务端对单个客户端连接的会话记录。
    /// </summary>
    /// <remarks>
    /// NetworkConnectionToClient 只是传输连接；ClientSession 在它之上补充协议阶段、
    /// 客户端版本和玩家实体绑定。以后接入账号认证时，AccountId 也应该放在会话层，
    /// 而不是塞进玩家 GameObject。
    /// </remarks>
    public sealed class ClientSession
    {
        internal ClientSession(NetworkConnectionToClient connection)
        {
            Connection = connection ?? throw new ArgumentNullException(nameof(connection));
            ConnectedAtUtc = DateTimeOffset.UtcNow;
            State = ClientSessionState.Connected;
            ClientVersion = "unknown";
        }

        /// <summary>Mirror 服务端连接对象。它只应在网络/会话层使用。</summary>
        public NetworkConnectionToClient Connection { get; }

        /// <summary>由 Transport 分配、在本次服务器进程中唯一的连接编号。</summary>
        public int ConnectionId => Connection.connectionId;

        /// <summary>服务器首次登记这条连接的 UTC 时间。</summary>
        public DateTimeOffset ConnectedAtUtc { get; }

        /// <summary>客户端上报的构建版本，仅用于诊断。</summary>
        public string ClientVersion { get; private set; }

        /// <summary>当前会话阶段。</summary>
        public ClientSessionState State { get; private set; }

        /// <summary>当前连接绑定的玩家网络实体；生成玩家前为 null。</summary>
        public NetworkIdentity PlayerIdentity { get; private set; }

        internal bool VerifyProtocol(string clientVersion)
        {
            if (State != ClientSessionState.Connected)
            {
                return false;
            }

            ClientVersion = string.IsNullOrWhiteSpace(clientVersion)
                ? "unknown"
                : clientVersion;
            State = ClientSessionState.ProtocolVerified;
            return true;
        }

        internal bool MarkReady()
        {
            if (State != ClientSessionState.ProtocolVerified)
            {
                return false;
            }

            State = ClientSessionState.Ready;
            return true;
        }

        internal bool AttachPlayer(NetworkIdentity playerIdentity)
        {
            if (State != ClientSessionState.Ready || playerIdentity == null)
            {
                return false;
            }

            PlayerIdentity = playerIdentity;
            State = ClientSessionState.Playing;
            return true;
        }

        internal void BeginDisconnect()
        {
            State = ClientSessionState.Disconnecting;
        }

        public override string ToString()
        {
            return $"session(connId={ConnectionId}, state={State}, client={ClientVersion})";
        }
    }
}
