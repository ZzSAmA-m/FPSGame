using Mirror;
using UnityEngine;

namespace Network.Client
{
    public class ClientSession
    {
        public int Id{get;}
        public NetworkConnectionToClient Conn{get;}
        
        
        public ClientSession(int id,NetworkConnectionToClient conn)
        {
            Id=id;
            Conn=conn;
        }
    }
}
