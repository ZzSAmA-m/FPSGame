using Mirror;
using UnityEngine;
using PrimeTween;
namespace GamePlay.Player.Network
{
    public class PlayerMovementNetwork : MonoBehaviour
    {
        private PlayerNetworkManager _networkManager;
        [SerializeField,Sirenix.OdinInspector.ReadOnly]private CharacterController _cc;
        void Awake()
        {
            _networkManager=GetComponent<PlayerNetworkManager>();
            _cc=_networkManager.Player.GetComponent<CharacterController>();
        }
        public void ApplyMovement(Vector2 deltaMove)
        {
            var moveDist=new Vector3(deltaMove.x,0,deltaMove.y);
            _cc.Move(moveDist);
        }
    }

}
