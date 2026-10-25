using Mirror;
using UnityEngine;
using Network.Common.Messages;
using Network.Client;
using PrimeTween;
namespace GamePlay.Player.Network
{
    public class PlayerPoseSync : MonoBehaviour
    {
        private PlayerNetworkManager _networkManager;
        private PlayerMovementController _controller;
        private ClientManager _clientManager;
        private NetworkId Id;

        #region MathVars
        private Vector3 _targetPos;
        private Vector3 _targetSight;
        private float _totalTime;
        private float _nowTime;
        private bool _isLerpMoving;
        #endregion
        private PlayerFPRotation _fpRotation;
        private PlayerSightController _sight;

        void Awake()
        {
            _networkManager=GetComponent<PlayerNetworkManager>();
            _controller=_networkManager.Player.GetComponent<PlayerMovementController>();
            _clientManager=ClientManager.Instance;
            Id=GetComponent<NetworkId>();
            _fpRotation=_networkManager.Player.GetComponentInChildren<PlayerFPRotation>();
            _sight=_networkManager.Player.GetComponent<PlayerSightController>();
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
            _targetSight=pose.facingDirection;
            _isLerpMoving=true;

        }
        void LerpSimulate(){
            if(!_isLerpMoving)return;
            LerpMove();
            LerpSight();
            _nowTime+=Time.deltaTime;
            if(_nowTime>=_totalTime){
                _isLerpMoving=false;
                _nowTime=0f;
            }
        }   
        void LerpMove(){
            Vector3 moveVec=(_targetPos-_controller.transform.position)*(Time.deltaTime/_totalTime);
            _controller.LawMove(ref moveVec);
        }
        void LerpSight(){
            if(_sight==null)return;
            Vector3 current=_fpRotation!=null ? _fpRotation.transform.forward : _controller.transform.forward;
            Vector3 target=Quaternion.Euler(_targetSight)*Vector3.forward;
            float t=_totalTime>0f ? Time.deltaTime/_totalTime : 1f;
            _sight.Simulate(Vector3.Slerp(current, target, t));
        }
    }

}
