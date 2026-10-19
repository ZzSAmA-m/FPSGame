using System.Collections.Generic;
using System.Text;
using Core.ZLog;
using Mirror;
using UnityEngine;

namespace Network.Client
{
    public class ClientSessions
    {
        private Dictionary<int,ClientSession> _clients=new();
        public int Count=>_clients.Count;
        public IEnumerable<KeyValuePair<int,ClientSession>> Clients=>_clients;
        public void AddClientSession(NetworkConnectionToClient conn)
        {
            ClientSession session = new(conn.connectionId,conn);
            _clients.Add(conn.connectionId,session);
        }
        public void RemoveClientSession(NetworkConnectionToClient conn)
        {
            _clients.Remove(conn.connectionId);
        }

        public ClientSession GetClientSession(int id)
        {
            if (_clients.TryGetValue(id,out var session))
            {
                return session;
            }
            else
            {
                ZLog.LogError<ClientSessions>("找不到这个客户端Session : id = "+id);
                throw new System.Exception("找不到这个客户端Session : id = "+id);
            }
        }

        public override string ToString()
        {
            StringBuilder sb=new($"客户端信息:\n客户端数量: {_clients.Count}\n");
            foreach(var cs in _clients)
            {
                sb.AppendLine($"id={cs.Key}");
            }
            return sb.ToString();
        }

    }
}
