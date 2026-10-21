using Core.Event;
using Core.Input;
using Mirror;
using UnityEngine;

namespace GamePlay.Player
{
    public class PlayerMovementController : MonoBehaviour
    {
        private CharacterController _cc;
        [SerializeField]private PlayerMovementDataSO _playerMovementDataSO;
        public PlayerMovementDataSO MovementData=>_playerMovementDataSO;

        void Awake()
        {
            _cc=GetComponent<CharacterController>();
        }

        /// <summary>
        /// 通过玩家输入结构体模拟运动
        /// </summary>
        /// <param name="input"></param>
        /// <param name="deltaTime"></param>
        public void Simulate(ref PlayerInput input)
        {
            Move(ref input.move);
        }
        public void Move(ref Vector2 vec)
        {
            Vector3 moveVec=new Vector3(vec.x,0,vec.y)*_playerMovementDataSO.MoveSpeed;
            _cc.Move(moveVec);
        }
    }
}

