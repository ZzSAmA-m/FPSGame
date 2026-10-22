using Mirror;
using UnityEngine;
using Network.Common.Messages;
using Network.Client;
using PrimeTween;
using Unity.VisualScripting;
namespace GamePlay.Player.Network
{
    public class PlayerMovementNetwork : MonoBehaviour
    {
        private PlayerNetworkManager _networkManager;
        private PlayerMovementController _controller;
        private ClientManager _clientManager;
        private NetworkId Id;

        #region LerpMove
        private Vector3 _targetPos;
        private float _totalTime;
        private float _nowTime;
        private bool _isLerpMoving;

        #endregion
        void Awake()
        {
            _networkManager=GetComponent<PlayerNetworkManager>();
            _controller=_networkManager.Player.GetComponent<PlayerMovementController>();
            _clientManager=ClientManager.Instance;
            Id=GetComponent<NetworkId>();
        }
        void Update(){
            LerpSimulate();
        }
        public void ApplyMovement(Vector2 deltaMove)
        {
            _controller.Move(ref deltaMove);
        }
        public void UpdateWithSnapshot(PlayerPose playerPose)
        {
            if(playerPose.netId==_clientManager.ClientId)return;
            StartLerpSimulate(ref playerPose);
        }
        void StartLerpSimulate(ref PlayerPose pose){
            _nowTime=0f;
            _totalTime=pose.durationTime;
            _targetPos=pose.position;
            _isLerpMoving=true;

        }
        void LerpSimulate(){
            LerpMove();
            LerpSight();
        }   
        void LerpMove(){
            if(!_isLerpMoving)return;
            Vector3 moveVec=(_targetPos-_controller.transform.position)*(Time.deltaTime/_totalTime);
            _controller.LawMove(ref moveVec);
            _nowTime+=Time.deltaTime;
            if(_nowTime>=_totalTime){
                _isLerpMoving=false;
                _nowTime=0;
            }
        }
        void LerpSight(){

        }
    }

}
