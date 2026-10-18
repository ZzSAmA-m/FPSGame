using Core.ZLog;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Network.Server
{
    [RequireComponent(typeof(ServerManager))]
    public class ServerManagerDebugger : MonoBehaviour
    {
        private ServerManager _manager;
        void Awake()
        {
            _manager=GetComponent<ServerManager>();
        }
        [Button]
        public void LogClientSessions()
        {
            ZLog.Log<ServerManagerDebugger>(_manager.Sessions.ToString());
        }
    }
}
  