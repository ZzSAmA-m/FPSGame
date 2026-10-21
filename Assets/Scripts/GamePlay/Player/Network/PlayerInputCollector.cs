using System;
using Core.Input;
using Mirror;
using UnityEngine;
namespace GamePlay.Player
{
    public class PlayerInputCollector : MonoBehaviour
    {
        private PlayerInputDriver _inputDriver;
        /// <summary>
        /// 玩家变换
        /// </summary>
        private Transform _playerTransf;
        #region InputVars
        /// <summary> 窗口内的世界空间水平位移，x 为世界 X，y 为世界 Z。 </summary>
        Vector2 _moveDelta;
        Vector3 _lastPosition;
        Vector2 _lookDelta;
        bool _isJump,_isFire,_isKey1,_iskey2,_isKey3;
        #endregion

        void Awake()
        {
            _inputDriver=PlayerInputDriver.Instance;
            _playerTransf=transform;
            _lastPosition=_playerTransf.position;
            ClearInputData();
        }
        void OnEnable()
        {
            _inputDriver.OnDriveInputData+=AddInputData;
        }
        void OnDisable()
        {
            _inputDriver.OnDriveInputData-=AddInputData;
        }
        
        public PlayerInput GetSample()
        {
            PlayerInput input = new()
            {
              move=_moveDelta,
              look=_lookDelta,
              key1=_isKey1,
              key2=_iskey2,
              key3=_isKey3,  
              lMouse=_isFire,
              space=_isJump
            };
            ClearInputData();
            return input;
        }

        void AddInputData(ref PlayerInput input,float time)
        {
            // 摇杆 x 为右、y 为前。按本帧朝向换成世界 XZ，再累加。
            Vector3 worldMove=_playerTransf.right*input.move.x+_playerTransf.forward*input.move.y;
            _moveDelta+=new Vector2(worldMove.x,worldMove.z)*time;
            _lookDelta+=input.look;
            _isKey1=_isKey1||input.key1;
            _iskey2=_iskey2||input.key2;
            _isKey3=_isKey3||input.key3;
            _isJump=_isJump||input.space;
            _isFire=_isFire||input.lMouse;
        }
        void ClearInputData()
        {
            _moveDelta=Vector2.zero;
            _lookDelta=Vector2.zero;
            _isFire=false;
            _isJump=false;
            _isKey1=false;
            _iskey2=false;
            _isKey3=false;
        }
    }

}
