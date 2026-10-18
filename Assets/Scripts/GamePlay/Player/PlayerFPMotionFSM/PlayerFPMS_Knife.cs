using System;
using System.Collections.Generic;
using GamePlay.FSM;
using GamePlay.Player;
using UnityEngine;
namespace GamePlay.Player.MotionFSM{
    public class PlayerFPMS_Knife : IState
    {
        public IState CurState { get ; set ; }

        public Dictionary<Type, IState> Type2State => throw new NotImplementedException();
        private PlayerFPMotionState _playerFPMotionState;

        private PlayerAnimatorController _controller;
        public PlayerFPMS_Knife(PlayerFPMotionState playerFPMotionState,PlayerAnimatorController controller)
        {
            _playerFPMotionState=playerFPMotionState;
            _controller=controller;

        }

        public void OnEnter()
        {
            _controller.PlayAnim(PlayerAnimatorName.FP.draw_butterfly,_controller.FPLayer);
            ApplyModel();
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
        void ApplyModel()
        {
            _controller.RifleModel.SetActive(false);
            _controller.KnifeModel.SetActive(true);
        }
    }
}