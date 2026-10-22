using Mirror;
using UnityEngine;
using PrimeTween;
using Network.Common.Messages;
namespace GamePlay.Player.Network
{
    public class PlayerMovementNetwork : MonoBehaviour
    {
        private PlayerNetworkManager _networkManager;
        private PlayerMovementController _controller;
        private NetworkId Id;
        [SerializeField,Sirenix.OdinInspector.ReadOnly]private CharacterController _cc;
        void Awake()
        {
            _networkManager=GetComponent<PlayerNetworkManager>();
            _cc=_networkManager.Player.GetComponent<CharacterController>();
            _controller=_networkManager.Player.GetComponent<PlayerMovementController>();
            Id=GetComponent<NetworkId>();
        }
        public void ApplyMovement(Vector2 deltaMove)
        {
            _controller.Move(ref deltaMove);
        }
        public void UpdateWithSnapshot(PlayerPose playerPose)
        {
            if(playerPose.netId==Id.Id)return;
            this.transform.position=playerPose.position;
        }
    }

}
