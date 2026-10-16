using System;
using System.Collections.Generic;
using GamePlay.FSM;
using GamePlay.Player;
using UnityEngine;
namespace GamePlay.Player.MotionFSM
{
    public class PlayerMS_Alive_BodyDown_Stand_Idle : IState
    {
        private PlayerMS_Alive_BodyDown_Stand _playerMS_Alive_BodyDown_Stand;
        private PlayerAnimatorController _controller;
    
        public PlayerMS_Alive_BodyDown_Stand_Idle(PlayerMS_Alive_BodyDown_Stand playerMS_Alive_BodyDown_Stand,PlayerAnimatorController controller)
        {
            _playerMS_Alive_BodyDown_Stand=playerMS_Alive_BodyDown_Stand;
            _controller=controller;
        }
    
        public IState CurState { get ; set; }

        public Dictionary<Type, IState> Type2State => null;

        public void OnEnter()
        {
            Debug.Log("Switch to Idle");
            _controller.CrossFadeAnim(PlayerAnimatorName.idle,_controller.switchAnimTime,_controller.DownLayer);
        }
    
        public void OnExit()
        {
            
        }
    
        public void OnPhysicsUpdate()
        {
            
        }
    
        public void OnUpdate()
        {
            if (_controller.Cc.velocity.magnitude>_controller.offset)
            {
                _playerMS_Alive_BodyDown_Stand.SwitchState<PlayerMS_Alive_BodyDown_Stand_Run>();
            }
        }
    }

}
