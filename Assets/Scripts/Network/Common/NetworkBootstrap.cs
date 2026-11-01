using System;
using Core.ZLog;
using kcp2k;
using Sirenix.OdinInspector;
using UnityEngine;
namespace Network.Common
{
    [RequireComponent(typeof(FPSNetworkManager),typeof(KcpTransport))]
    public sealed class NetworkBootstrap : MonoBehaviour
    {
        private string _txt_ip="127.0.0.1";
        private string _txt_port="7777";
        private bool _clicked=false;
        private FPSNetworkManager _manager;
        void Awake()
        {
            _manager=GetComponent<FPSNetworkManager>();
        }
        /// <summary> 以客户端模式启动 </summary>
        [Button]
        public void StartClient(string ip,string port)
        {
            try
            {
                _manager.networkAddress=ip;
                GetComponent<KcpTransport>().port=ushort.Parse(port);
                _manager.StartClient();
            }
            catch (Exception e)
            {
                ZLog.LogError<NetworkBootstrap>(e.Message);
            }

        }
        /// <summary> 以服务器模式启动 </summary>
        [Button]
        public void StartServer()
        {
            _manager.StartServer();
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

        void OnGUI()
        {
            if(_clicked)return;
            _txt_ip=GUILayout.TextField(_txt_ip);
            _txt_port=GUILayout.TextField(_txt_port);
            if (GUILayout.Button("Client"))
            {
                _clicked=true;

                StartClient(_txt_ip,_txt_port);
            }
            if (GUILayout.Button("Server"))
            {
                _clicked=true;
                StartServer();
            }
        }
    }
}
