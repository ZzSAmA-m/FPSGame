using System;
using System.Collections.Generic;
using GamePlay.FSM;
using UnityEngine;
namespace GamePlay.Player.MotionFSM
{
    public class PlayerMS_Alive_BodyUp_Rifle : IState
    {
        private PlayerMS_Alive_BodyUp _playerMS_Alive_BodyUp;
        private PlayerAnimatorController _controller;
        private Dictionary<Type,IState> _type2State;

        #region States
        
        private PlayerMS_Alive_BodyUp_Rifle_Hold _playerMS_Alive_BodyUp_Rifle_Hold;
        
        #endregion
        public IState CurState { get ; set ; }

        public Dictionary<Type, IState> Type2State => _type2State;


        public PlayerMS_Alive_BodyUp_Rifle(PlayerMS_Alive_BodyUp playerMS_Alive_BodyUp,PlayerAnimatorController controller)
        {
            _controller=controller;
            _playerMS_Alive_BodyUp=playerMS_Alive_BodyUp;

            _playerMS_Alive_BodyUp_Rifle_Hold=new(this,controller);

            _type2State = new()
            {
                {typeof(PlayerMS_Alive_BodyUp_Rifle_Hold),_playerMS_Alive_BodyUp_Rifle_Hold},
            };


        }

        public void OnEnter()
        {
            this.SwitchState<PlayerMS_Alive_BodyUp_Rifle_Hold>();
        }

        public void OnExit()
        {
            
        }

        public void OnPhysicsUpdate()
        {
            CurState.OnUpdate();
        }

        public void OnUpdate()
        {
            CurState.OnUpdate();
        }
    }

}
