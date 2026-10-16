using System;
using System.Collections.Generic;
using GamePlay.FSM;
using UnityEngine;
namespace GamePlay.Player.MotionFSM
{
    public class PlayerMotionState : IState
    {
        public IState CurState{get; set;}

        public Dictionary<Type, IState> Type2State => _type2State;

        private PlayerMS_Alive _playerMS_Alive;
        private PlayerMS_Death _playerMS_Death;
        private PlayerAnimatorController _controller;

        private Dictionary<Type,IState> _type2State;

        public PlayerMotionState(PlayerAnimatorController controller)
        {

            _controller=controller;

            _playerMS_Alive=new(this,_controller); 
            _playerMS_Death=new(this,_controller);

            _type2State = new()
            {
                {typeof(PlayerMS_Alive),_playerMS_Alive},
                {typeof(PlayerMS_Death),_playerMS_Death},
            };
        }
        public void OnEnter()
        {
            _controller.SetLayerWeight(_controller.UpLayer,1);
            _controller.SetLayerWeight(_controller.DownLayer,1);
            _controller.SetLayerWeight(_controller.FPLayer,0);
            this.SwitchState<PlayerMS_Alive>();
        }

        public void OnExit()
        {
            
        }

        public void OnUpdate()
        {
            CurState.OnUpdate();
        }

        public void OnPhysicsUpdate()
        {
            CurState.OnPhysicsUpdate();
        }
    }

}
