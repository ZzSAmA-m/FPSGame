using System;
using System.Threading;
using Core.Input;
using Core.Timer;
using Core.ZLog;
using Mirror;
using Network.Client;
using Network.Common;
using Network.Common.Messages;
using UnityEngine;
using MyTimer=Core.Timer.Timer;
namespace GamePlay.Player.Network
{
    public class ClientCommandPublisher : NetworkBehaviour
    {
        private PlayerInputHandler _inputHandler;
        private PlayerNetworkManager _networkManager;
        public bool IsPublishing{get;private set;}
        void Awake()
        {
            _networkManager=GetComponent<PlayerNetworkManager>();
            _inputHandler=_networkManager.Player.GetComponent<PlayerInputHandler>();
        }
        void Start()
        {
            _inputHandler.isLocalPlayer=isLocalPlayer;
            
        }

        void OnEnable()
        {
            _inputHandler.CommandProduced+=PublishCommand;
        }


        void OnDisable()
        {
            _inputHandler.CommandProduced-=PublishCommand;
        }

        public void StartPublish()
        {
            IsPublishing=true;
        }
        public void StopPublish()
        {
            IsPublishing=false;
        }

        private void PublishCommand(ref PlayerCommandMessage command)
        {
            if(!IsPublishing)return;
            var manager=ClientManager.Instance??throw new System.Exception("找不到ClientManager");
            manager.PublishPlayerInputCmd(ref command);
            ZLog.Log<ClientCommandPublisher>("Send Command: "+command);
        }
    }
}
