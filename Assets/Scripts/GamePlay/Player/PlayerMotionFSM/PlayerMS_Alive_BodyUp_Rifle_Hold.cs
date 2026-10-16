using System;
using System.Collections.Generic;
using GamePlay.FSM;
using UnityEngine;
namespace GamePlay.Player.MotionFSM
{
    public class PlayerMS_Alive_BodyUp_Rifle_Hold:IState
    {
        private PlayerMS_Alive_BodyUp_Rifle _playerMS_Alive_BodyUp_Rifle;
        private PlayerAnimatorController _controller;
        public PlayerMS_Alive_BodyUp_Rifle_Hold(PlayerMS_Alive_BodyUp_Rifle playerMS_Alive_BodyUp_Rifle,PlayerAnimatorController controller)
        {
            _controller=controller;
            _playerMS_Alive_BodyUp_Rifle=playerMS_Alive_BodyUp_Rifle;
        }

        public IState CurState { get; set; }

        public Dictionary<Type, IState> Type2State =>  throw new NotImplementedException();

        public void OnEnter()
        {
            Debug.Log("Enter Rifle Hold");
            _controller.PlayAnim(PlayerAnimatorName.draw_ak,_controller.UpLayer);
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
