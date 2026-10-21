using Mirror;
using UnityEngine;
using PrimeTween;
namespace GamePlay.Player.Network
{
    public class PlayerMovementNetwork : MonoBehaviour
    {
        private PlayerNetworkManager _networkManager;
        private PlayerMovementController _controller;
        [SerializeField,Sirenix.OdinInspector.ReadOnly]private CharacterController _cc;
        void Awake()
        {
            _networkManager=GetComponent<PlayerNetworkManager>();
            _cc=_networkManager.Player.GetComponent<CharacterController>();
            _controller=_networkManager.Player.GetComponent<PlayerMovementController>();
        }
        public void ApplyMovement(Vector2 deltaMove)
        {
            _controller.Move(ref deltaMove);
        }
    }

}
