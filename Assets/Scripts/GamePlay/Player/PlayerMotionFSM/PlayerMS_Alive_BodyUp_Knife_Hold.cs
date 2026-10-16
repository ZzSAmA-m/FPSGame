using System;
using System.Collections.Generic;
using GamePlay.FSM;
using UnityEngine;
namespace GamePlay.Player.MotionFSM
{
    public class PlayerMS_Alive_BodyUp_Knife_Hold:IState
    {
        private PlayerMS_Alive_BodyUp_Knife _playerMS_Alive_BodyUp_Knife;
        private PlayerAnimatorController _controller;
        public PlayerMS_Alive_BodyUp_Knife_Hold(PlayerMS_Alive_BodyUp_Knife playerMS_Alive_BodyUp_Knife,PlayerAnimatorController controller)
        {
            _controller=controller;
            _playerMS_Alive_BodyUp_Knife=playerMS_Alive_BodyUp_Knife;
        }

        public IState CurState { get; set; }

        public Dictionary<Type, IState> Type2State =>  throw new NotImplementedException();

        public void OnEnter()
        {
            Debug.Log("Enter Knife Hold");
            _controller.PlayAnim(PlayerAnimatorName.draw_butterfly,_controller.UpLayer);
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
