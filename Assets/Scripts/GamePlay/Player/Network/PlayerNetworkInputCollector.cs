using System;
using Core.Input;
using Mirror;
using UnityEngine;
namespace GamePlay.Player.Network
{
    public class PlayerNetworkInputCollector : MonoBehaviour
    {
        private PlayerNetworkManager _networkNanager;
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
            _networkNanager=GetComponent<PlayerNetworkManager>();
            _inputDriver=PlayerInputDriver.Instance;
            _playerTransf=_networkNanager.Player.transform;
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
            // 本帧移动已经按当时的朝向写入角色位置。取位置差，避免把局部摇杆值直接相加。
            Vector3 position=_playerTransf.position;
            Vector3 worldDelta=position-_lastPosition;
            _moveDelta+=new Vector2(worldDelta.x,worldDelta.z);
            _lastPosition=position;
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
