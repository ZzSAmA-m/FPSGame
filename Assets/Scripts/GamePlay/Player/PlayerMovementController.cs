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

        void Awake()
        {
            _cc=GetComponent<CharacterController>();
        }

        /// <summary>
        /// 通过玩家输入结构体模拟运动
        /// </summary>
        /// <param name="input"></param>
        /// <param name="deltaTime"></param>
        public void Simulate(ref PlayerInput input,float deltaTime)
        {
            Vector2 vec=input.move;
            var moveVec=transform.right*vec.x+transform.forward*vec.y;
            moveVec=moveVec*deltaTime*_playerMovementDataSO.MoveSpeed;
            _cc.Move(moveVec);
        }
    }
}

