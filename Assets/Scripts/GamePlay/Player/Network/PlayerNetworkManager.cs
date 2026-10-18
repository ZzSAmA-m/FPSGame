using Mirror;
using UnityEngine;
namespace GamePlay.Player.Network
{
    public sealed class PlayerNetworkManager : NetworkBehaviour
    {
        [SerializeField]private GameObject _player;
        /// <summary>
        /// Player对象
        /// </summary>
        public GameObject Player => _player;

        public ClientCommandPublisher CmdPublisher{get;private set;}
        public PlayerMovementNetwork NetPlayerMovement{get;private set;}

        void Awake()
        {
            if (Player == null)
            {
                Debug.LogError("没有给 PlayerNetworkManager 赋值本地Player对象!");
            }
            CmdPublisher=GetComponent<ClientCommandPublisher>();
            NetPlayerMovement=GetComponent<PlayerMovementNetwork>();
        }
        void Start()
        {
            CmdPublisher.StartPublish();
        }

    }

}
