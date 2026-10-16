using System;
using System.Collections.Generic;
using GamePlay.FSM;
using GamePlay.Player.MotionFSM;
using UnityEngine;
namespace GamePlay.Player.MotionFSM
{
    public class PlayerMS_Alive : IState
    {
        private PlayerMotionState _motionState;

        private PlayerMS_Alive_BodyDown _playerMS_Alive_BodyDown;
        private PlayerMS_Alive_BodyUp   _playerMS_Alive_BodyUp;
        private PlayerAnimatorController _controller;

        public PlayerMS_Alive(PlayerMotionState motionState,PlayerAnimatorController controller)
        {
            _motionState=motionState;

            _controller=controller;
            _playerMS_Alive_BodyDown=new(this,_controller);
            _playerMS_Alive_BodyUp=new(this,_controller);

        }

        public IState CurState { get ; set; }

        public Dictionary<Type, IState> Type2State => null;

        public void OnEnter()
        {
            EnterDownAndUpSM();
        }

        public void OnExit()
        {
            ExitDownAndUpSM();
        }

        public void OnPhysicsUpdate()
        {
            _playerMS_Alive_BodyDown.OnPhysicsUpdate();
            _playerMS_Alive_BodyUp.OnPhysicsUpdate();
        }

        public void OnUpdate()
        {
            _playerMS_Alive_BodyDown.OnUpdate();
            _playerMS_Alive_BodyUp.OnUpdate();
        }

        void EnterDownAndUpSM()
        {
            _playerMS_Alive_BodyDown.OnEnter();
            _playerMS_Alive_BodyUp.OnEnter();
        }
        void ExitDownAndUpSM()
        {
            _playerMS_Alive_BodyDown.OnExit();
            _playerMS_Alive_BodyUp.OnExit();
        }
    }
}