using Core.Event;
using Mirror;
using Network.Client;
using Network.Common;
using UnityEngine;

namespace GamePlay.Player.Network
{
    public class NetworkId : NetworkBehaviour
    {
        [SyncVar(hook =nameof(OnIdChanged))]
        private int _id;
        [field:SerializeField]
        public int Id{get;private set;}
        public override void OnStartServer()
        {
            FPSNetworkManager manager = (FPSNetworkManager)NetworkManager.singleton;
            ClientSession session = manager.ServerManagerInst.Sessions
                .GetClientSession(connectionToClient.connectionId);
            _id = session.Id;
        }

        void OnIdChanged(int oldValue,int newValue)
        {
            Id=newValue;
            EventCenter.NetEvents.AddLocalPlayer2ClientManager.RaiseEvent(GetComponentInParent<NetworkIdentity>().gameObject);
        }
    }
}