using System;
using System.Collections.Generic;
using Core.Event;
using Core.Utility;
using Core.ZLog;
using Mirror;
using Network.Common;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Network.Client
{
    public sealed partial class ClientManager : Singleton<ClientManager>
    {
        public int ClientId{get;private set;}
        private NetBridge _bridge;
        public GamePlayerManager PlayerManager{get;private set;}=new();
        public void Initialize(NetBridge bridge)
        {

            _bridge = bridge ?? throw new ArgumentNullException(nameof(bridge));


            _bridge.ClientHelloReceived += HandleHelloResponse;
            _bridge.GetClientIdResultReceived += HandleGetClientId;
            _bridge.PlayerSnapshotReceived+=HandlePlayerSnapshot;
            ZLog.Log<ClientManager>("客户端应用层已初始化");
        }
        public void ShutDown()
        {
            _bridge.ClientHelloReceived -= HandleHelloResponse;
            _bridge.GetClientIdResultReceived -= HandleGetClientId;
            _bridge.PlayerSnapshotReceived-=HandlePlayerSnapshot;
        }
        void OnEnable()
        {
            EventCenter.NetEvents.AddLocalPlayer2ClientManager.OnEventRaised+=OnAddLocalPlayer;
        }
        void OnDisable()
        {
            EventCenter.NetEvents.AddLocalPlayer2ClientManager.OnEventRaised-=OnAddLocalPlayer;
        }

        private void OnAddLocalPlayer(GameObject obj)
        {
            PlayerManager.AddPlayer(obj);
        }

        [Button]
        void DebugShowPlayerCount(){
            ZLog.Log<ClientManager>(PlayerManager.Players.Count.ToString());
        }
    }
}
