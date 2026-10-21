using System;
using Core.Utility;
using Core.ZLog;
using Mirror;
using Network.Client;
using Network.Common.Messages;
using Network.Server;
using UnityEngine;

namespace Network.Common
{
    /// <summary>
    /// 项目的 Mirror 生命周期适配器和网络模块组合根。
    /// </summary>
    /// <remarks>
    /// 这里负责把 Mirror 的回调转交给 NetBridge 与 ServerManager，
    /// 但不直接实现比赛规则。这样网络启动顺序集中可见，同时避免依赖全局事件总线。
    /// </remarks>
    [RequireComponent(typeof(NetBridge))]
    public sealed class FPSNetworkManager : NetworkManager
    {
        [SerializeField, Tooltip("承载 ServerManager 的服务器运行时预制体")]
        private GameObject _server;
        [SerializeField, Tooltip("承载 ClientManager 的客户端运行时预制体")]
        private GameObject _client;
        private NetBridge _bridge;

        /// <summary>当前进程的网络角色。Host 会同时具备 Server 与 Client。</summary>
        [Sirenix.OdinInspector.ReadOnly]
        public NetState NetState { get; private set; } = NetState.None;

        /// <summary>当前 NetworkManager 对象上的消息网关。</summary>
        public NetBridge Bridge => _bridge;

        /// <summary>仅在服务器运行期间存在的应用层实例。</summary>
        public ServerManager ServerManagerInst { get; private set; }
        public ClientManager ClientManagerInst { get; private set; }

        public override void Awake()
        {

            base.Awake();
            _bridge = GetComponent<NetBridge>();

        }
        /// <summary>服务器侧启动时创建应用层，并注册服务端协议入口。</summary>
        public override void OnStartServer()
        {
            base.OnStartServer();

            CreateServerRuntime();
            _bridge.StartServer();
            RefreshNetState();

            ZLog.Log<FPSNetworkManager>($" 服务端已启动，模式={NetState}");
        }
        /// <summary>客户端侧启动时注册响应入口；Host 会同时执行双端启动。</summary>
        public override void OnStartClient()
        {
            base.OnStartClient();
            
            _bridge.StartClient();
            RefreshNetState();
            CreateClientRuntime();

            ZLog.Log<FPSNetworkManager>($" 客户端已启动，模式={NetState}");
        }
        public override void OnServerConnect(NetworkConnectionToClient conn)
        {
            base.OnServerConnect(conn);
            ServerManagerInst.AddClient(conn);
        }
        /// <summary>生成完整玩家预制体，并将 NetworkIdentity 绑定到对应会话。</summary>
        public override void OnServerAddPlayer(NetworkConnectionToClient connection)
        {
            base.OnServerAddPlayer(connection);
            if (connection.identity != null)
            {
                connection.identity.gameObject.name =
                    $"{playerPrefab.name} [connId={connection.connectionId}]";
            }

            ZLog.Log<FPSNetworkManager>(
                $"已生成联网玩家:connId={connection.connectionId}, " +
                $"netId={connection.identity.netId}");
        }


        /// <summary>
        /// 客户端连接完成后先发送项目协议握手，再让 Mirror 进入 Ready/AddPlayer 流程。
        /// </summary>
        public override void OnClientConnect()
        {
            // HelloRequest 与随后由 base 发送的 Ready/AddPlayer 都走可靠通道，
            // 因而服务器会先验证协议，再允许会话进入 Ready。
            _bridge.SendHelloToServer(Application.version);
            
            base.OnClientConnect();
        }



        /// <summary>客户端停止时注销客户端 Handler 和事件订阅。</summary>
        public override void OnStopClient()
        {
            
            _bridge.StopClient();
            base.OnStopClient();
            if (ClientManagerInst != null)
            {
                ClientManagerInst.ShutDown();
                Destroy(ClientManagerInst.gameObject);
                ClientManagerInst=null;
            }
            // Mirror 的 Stop 回调发生在静态 active 标记完全落下之前，
            // 因此这里按“客户端即将停止”的事实计算，而不是读取尚未刷新的 active。
            NetState = NetworkServer.active ? NetState.Server : NetState.None;

            ZLog.Log<FPSNetworkManager>($" 客户端已停止，模式={NetState}。");
        }

        /// <summary>服务器停止时逆序关闭消息入口和应用层运行时。</summary>
        public override void OnStopServer()
        {
            _bridge.StopServer();

            if (ServerManagerInst != null)
            {
                ServerManagerInst.Shutdown();
                Destroy(ServerManagerInst.gameObject);
                ServerManagerInst = null;
            }

            base.OnStopServer();

            // 同理，回调期间 NetworkServer.active 仍可能为 true。
            NetState = NetworkClient.active ? NetState.Client : NetState.None;

            ZLog.Log<FPSNetworkManager>($" 服务端已停止，模式={NetState}。");
        }

        private void CreateServerRuntime()
        {

            GameObject serverObject = Instantiate(_server);
            serverObject.name = _server.name;

            ServerManagerInst = serverObject.GetComponent<ServerManager>();

            
            ServerManagerInst.Initialize(_bridge);
        }
        private void CreateClientRuntime()
        {

            GameObject clientObject = Instantiate(_client);
            clientObject.name = _client.name;

            ClientManagerInst = clientObject.GetComponent<ClientManager>();

            
            ClientManagerInst.Initialize(_bridge);
        }
        

        

        private void RefreshNetState()
        {
            bool serverActive = NetworkServer.active;
            bool clientActive = NetworkClient.active;

            NetState = (serverActive, clientActive) switch
            {
                (true, true) => NetState.Host,
                (true, false) => NetState.Server,
                (false, true) => NetState.Client,
                _ => NetState.None
            };
        }
    }
}

