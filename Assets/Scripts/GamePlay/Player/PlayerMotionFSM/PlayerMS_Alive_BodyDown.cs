using System;
using System.Collections.Generic;
using GamePlay.FSM;
using Mirror.Examples.Basic;
using UnityEngine;

namespace GamePlay.Player.MotionFSM
{
    public class PlayerMS_Alive_BodyDown : IState
    {
        
        public IState CurState{get;set;}

        public Dictionary<Type, IState> Type2State => _type2State;

        private PlayerMS_Alive _playerMS_Alive;
        private Dictionary<Type,IState> _type2State;

        private PlayerAnimatorController _controller;

        #region ChildrenStates
        private PlayerMS_Alive_BodyDown_Stand _playerMS_Alive_BodyDown_Stand;
        #endregion

        public PlayerMS_Alive_BodyDown(PlayerMS_Alive playerMS_Alive,PlayerAnimatorController controller)
        {
            _playerMS_Alive=playerMS_Alive;

            _controller=controller;
            _playerMS_Alive_BodyDown_Stand=new(this,_controller);
        

            _type2State = new()
            {
                {typeof(PlayerMS_Alive_BodyDown_Stand),_playerMS_Alive_BodyDown_Stand},
                //TODO : More States
            };
        }

        public void OnEnter()
        {
            this.SwitchState<PlayerMS_Alive_BodyDown_Stand>();
        }

        public void OnExit()
        {

        }

        public void OnPhysicsUpdate()
        {
            CurState.OnPhysicsUpdate();
        }

        public void OnUpdate()
        {
            CurState.OnUpdate();
        }
    }
}