using System;
using System.Collections.Generic;
using GamePlay.FSM;
using GamePlay.Player;
using UnityEngine;
namespace GamePlay.Player.MotionFSM{
    public class PlayerFPMS_Rifle : IState
    {
        public IState CurState { get ; set ; }

        public Dictionary<Type, IState> Type2State => throw new NotImplementedException();
        private PlayerFPMotionState _playerFPMotionState;

        private PlayerAnimatorController _controller;
        public PlayerFPMS_Rifle(PlayerFPMotionState playerFPMotionState,PlayerAnimatorController controller)
        {
            _playerFPMotionState=playerFPMotionState;
            _controller=controller;

        }

        public void OnEnter()
        {
            _controller.PlayAnim(PlayerAnimatorName.FP.draw_ak,_controller.FPLayer);
        }

        public void OnExit()
        {

        }

        public void OnPhysicsUpdate()
        {

        }

        public void OnUpdate()
        {

        }
    }
}