using System;
using System.Collections.Generic;
using Core.Utility;
using GamePlay.FSM;
using GamePlay.Player;
using GamePlay.Player.MotionFSM;
using UnityEngine;
namespace GamePlay.Player.MotionFSM
{
    public class PlayerFPMotionState : IState
    {
        public IState CurState { get; set; }

        public Dictionary<Type, IState> Type2State => _type2State;

        private PlayerAnimatorController _controller;

        private PlayerWeaponController _playerWeaponController;

        private Dictionary<Type,IState> _type2State;


        #region States

        private PlayerFPMS_Knife _playerFPMS_Knife;
        private PlayerFPMS_Rifle _playerFPMS_Rifle;

        #endregion

        public PlayerFPMotionState(PlayerAnimatorController controller)
        {
            _controller=controller;


            _playerFPMS_Knife=new(this,controller);
            _playerFPMS_Rifle=new(this,controller);

            _type2State = new()
            {
                {typeof(PlayerFPMS_Knife),_playerFPMS_Knife},
                {typeof(PlayerFPMS_Rifle),_playerFPMS_Rifle},
            };
            _playerWeaponController=controller.WeaponController;
        }


        public void OnEnter()
        {
            _controller.SetLayerWeight(_controller.FPLayer,1);
            this.SwitchState<PlayerFPMS_Knife>();
            _playerWeaponController.OnWeaponInvoked+=OnWeaponMotion;
        }

        public void OnExit()
        {
            _playerWeaponController.OnWeaponInvoked-=OnWeaponMotion;
        }

        public void OnPhysicsUpdate()
        {
            CurState.OnPhysicsUpdate();
        }

        public void OnUpdate()
        {
            CurState.OnUpdate();
        }


        void OnWeaponMotion(WeaponInputAction action)
        {
            switch (action)
            {
                case WeaponInputAction.Key1:
                    OnClickHand1();
                    break;
                case WeaponInputAction.Key2:
                    OnClickHand2();
                    break;
                case WeaponInputAction.Key3:
                    OnClickHand3();
                    break;
            }
        }

        private void OnClickHand1()
        {
            
            this.SwitchState<PlayerFPMS_Rifle>();
        }

        private void OnClickHand2()
        {

        }

        private void OnClickHand3()
        {
            
            this.SwitchState<PlayerFPMS_Knife>();
        }
    }
}