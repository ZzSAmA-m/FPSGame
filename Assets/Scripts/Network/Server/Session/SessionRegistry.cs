using System.Collections.Generic;
using Mirror;

namespace Network.Server.Session
{
    /// <summary>
    /// 当前服务器进程内所有客户端会话的唯一注册表。
    /// </summary>
    /// <remarks>
    /// 集中维护连接索引可以避免多个 Manager 各自保存一份玩家列表，
    /// 也让断线清理只需要经过一个入口。注册表不负责网络发送和玩法规则。
    /// </remarks>
    public sealed class SessionRegistry
    {
        private readonly Dictionary<int, ClientSession> _sessions = new();

        /// <summary>当前登记的会话数量。</summary>
        public int Count => _sessions.Count;

        /// <summary>只读枚举当前会话；调用方不能替换注册表内容。</summary>
        public IEnumerable<ClientSession> Sessions => _sessions.Values;

        /// <summary>
        /// 登记一条新连接。相同 connectionId 重复登记会失败。
        /// </summary>
        public bool TryAdd(NetworkConnectionToClient connection, out ClientSession session)
        {
            session = null;
            if (connection == null || _sessions.ContainsKey(connection.connectionId))
            {
                return false;
            }

            session = new ClientSession(connection);
            _sessions.Add(connection.connectionId, session);
            return true;
        }

        /// <summary>按 Mirror 连接查找会话。</summary>
        public bool TryGet(NetworkConnectionToClient connection, out ClientSession session)
        {
            if (connection == null)
            {
                session = null;
                return false;
            }

            return _sessions.TryGetValue(connection.connectionId, out session);
        }

        /// <summary>按 connectionId 查找会话，便于诊断和管理工具使用。</summary>
        public bool TryGet(int connectionId, out ClientSession session)
        {
            return _sessions.TryGetValue(connectionId, out session);
        }

        /// <summary>
        /// 移除连接对应的会话。该操作是幂等的，重复清理不会抛异常。
        /// </summary>
        public bool TryRemove(NetworkConnectionToClient connection, out ClientSession session)
        {
            session = null;
            if (connection == null || !_sessions.TryGetValue(connection.connectionId, out session))
            {
                return false;
            }

            session.BeginDisconnect();
            return _sessions.Remove(connection.connectionId);
        }

        /// <summary>服务器停止时清空全部进程内会话。</summary>
        public void Clear()
        {
            foreach (ClientSession session in _sessions.Values)
            {
                session.BeginDisconnect();
            }

            _sessions.Clear();
        }
    }
}
