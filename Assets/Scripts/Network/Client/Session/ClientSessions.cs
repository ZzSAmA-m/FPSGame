using System.Collections.Generic;
using System.Text;
using Mirror;

namespace Network.Client
{
    public sealed class ClientSessions
    {
        readonly Dictionary<int, ClientSession> _clients = new();

        public int Count => _clients.Count;
        public IEnumerable<KeyValuePair<int, ClientSession>> Clients => _clients;

        public void Add(NetworkConnectionToClient connection)
        {
            _clients.Add(connection.connectionId, new ClientSession(connection.connectionId, connection));
        }

        public void Remove(NetworkConnectionToClient connection)
        {
            if (connection == null)
                return;
            _clients.Remove(connection.connectionId);
        }

        public bool TryGet(int connectionId, out ClientSession session)
        {
            return _clients.TryGetValue(connectionId, out session);
        }

        public override string ToString()
        {
            var text = new StringBuilder();
            text.Append("客户端数量: ").Append(_clients.Count);
            foreach (KeyValuePair<int, ClientSession> pair in _clients)
                text.Append('\n').Append(pair.Value);
            return text.ToString();
        }
    }
}
