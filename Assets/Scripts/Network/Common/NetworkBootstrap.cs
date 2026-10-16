using Core.Utility;
using Sirenix.OdinInspector;
using UnityEngine;
namespace Network.Common
{
    [RequireComponent(typeof(FPSNetworkManager))]
    public sealed class NetworkBootstrap : MonoBehaviour
    {
        private FPSNetworkManager _manager;
        void Awake()
        {
            _manager=GetComponent<FPSNetworkManager>();
        }
        /// <summary> 以客户端模式启动 </summary>
        [Button]
        public void StartClient()
        {
            _manager.StartClient();
        }
        /// <summary> 以服务器模式启动 </summary>
        [Button]
        public void StartServer()
        {
            _manager.StartServer();
        }
        /// <summary> 以Host模式启动,仅仅调试,会出现大量错误!</summary>
        [Button]
        public void UnsafeStartHost()
        {
            _manager.StartHost();
        }
        /// <summary> 关闭客户端 </summary>
        [Button]
        public void StopClient()
        {
            _manager.StopClient();
        }
        /// <summary> 关闭服务端 </summary>
        [Button]
        public void StopServer()
        {
            _manager.StopServer();
        }

        /// <summary> 关闭Host </summary>
        [Button]
        public void StopHost()
        {
            _manager.StopHost();
        }

        void OnGUI()
        {
            // GUILayout.Button("Client");
            // GUILayout.Button("Server");
            if (GUILayout.Button("Client"))
            {
                StartClient();
            }
            if (GUILayout.Button("Server"))
            {
                StartServer();
            }
        }
    }
}
