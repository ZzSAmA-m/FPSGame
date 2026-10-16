using System;
using System.Collections.Generic;
using GamePlay.FSM;
using GamePlay.Player;
using UnityEngine;

namespace GamePlay.Player.MotionFSM
{
    public class PlayerMS_Alive_BodyDown_Stand_Run : IState
    {
        private PlayerMS_Alive_BodyDown_Stand _playerMS_Alive_BodyDown_Stand;
        private PlayerAnimatorController _controller;

        private EStandRunState _state;

        public PlayerMS_Alive_BodyDown_Stand_Run(PlayerMS_Alive_BodyDown_Stand playerMS_Alive_BodyDown_Stand,PlayerAnimatorController controller)
        {
            _playerMS_Alive_BodyDown_Stand=playerMS_Alive_BodyDown_Stand;
            _controller=controller;
        }

        public IState CurState { get ; set; }

        public Dictionary<Type, IState> Type2State => null;

        public void OnEnter()
        {
            Debug.Log("Switch to Run");
            _state=EStandRunState.NONE;
        }

        public void OnExit()
        {

        }

        public void OnPhysicsUpdate()
        {

        }

        public void OnUpdate()
        {
            Update();
        }

        void Update()
        {
            var v=_controller.Cc.velocity;
            var offset=_controller.offset;
            v=_controller.transform.InverseTransformDirection(v);
            if (v.z > offset)
            {
                if(_state==EStandRunState.N)return;
                _state=EStandRunState.N;
                _controller.CrossFadeAnim(PlayerAnimatorName.run_n,_controller.switchAnimTime,_controller.DownLayer);
            }else if (v.z < -offset)
            {
                if(_state==EStandRunState.S)return;
                _state=EStandRunState.S;
                _controller.CrossFadeAnim(PlayerAnimatorName.run_s,_controller.switchAnimTime,_controller.DownLayer);
            }else if (v.x > offset)
            {
                if(_state==EStandRunState.E)return;
                _state=EStandRunState.E;
                _controller.CrossFadeAnim(PlayerAnimatorName.run_e,_controller.switchAnimTime,_controller.DownLayer);
            }else if (v.x < -offset)
            {
                if(_state==EStandRunState.W)return;
                _state=EStandRunState.W;
                _controller.CrossFadeAnim(PlayerAnimatorName.run_w,_controller.switchAnimTime,_controller.DownLayer);
            }
            else
            {
                _controller.CrossFadeAnim(PlayerAnimatorName.idle,_controller.switchAnimTime,_controller.DownLayer);
                _playerMS_Alive_BodyDown_Stand.SwitchState<PlayerMS_Alive_BodyDown_Stand_Idle>();
            }
        }
    }
}