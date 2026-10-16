using System;
using System.Collections.Generic;
using Core.Utility;
using GamePlay.FSM;
using Mirror;
using UnityEngine;
using UnityEngine.InputSystem;
namespace GamePlay.Player.MotionFSM
{
    public class PlayerMS_Alive_BodyUp : IState
    {
        private PlayerMS_Alive _playerMS_Alive;
        private PlayerAnimatorController _controller;
        private PlayerWeaponController _weaponController;
        private Dictionary<Type,IState> _type2State;
        private bool _active=false;

        #region States

        private PlayerMS_Alive_BodyUp_Rifle _playerMS_Alive_BodyUp_Rifle;
        private PlayerMS_Alive_BodyUp_Knife _playerMS_Alive_BodyUp_Knife;

        #endregion

        public PlayerMS_Alive_BodyUp(PlayerMS_Alive playerMS_Alive,PlayerAnimatorController controller)
        {
            _playerMS_Alive=playerMS_Alive;

            _controller=controller;

            _playerMS_Alive_BodyUp_Rifle=new(this,controller);
            _playerMS_Alive_BodyUp_Knife=new(this,controller);

            _type2State = new()
            {
                {typeof(PlayerMS_Alive_BodyUp_Rifle),_playerMS_Alive_BodyUp_Rifle},
                {typeof(PlayerMS_Alive_BodyUp_Knife),_playerMS_Alive_BodyUp_Knife},
            };
            
            _weaponController=_controller.WeaponController;
        }

        public IState CurState { get; set; }

        public Dictionary<Type, IState> Type2State => _type2State;

        public void OnEnter()
        {
            this.SwitchState<PlayerMS_Alive_BodyUp_Rifle>();
            _active=true;
            _weaponController.OnWeaponInvoked+=OnWeaponMotion;
        }

        public void OnExit()
        {
            _active=false;
            _weaponController.OnWeaponInvoked-=OnWeaponMotion;
        }

        public void OnPhysicsUpdate()
        {
            CurState.OnPhysicsUpdate();
        }

        public void OnUpdate()
        {
            CurState.OnUpdate();
        }

        /// <summary>
        /// 接受一个 WeaponInputAction,根据具体的参数来调用不同方法,例如key1 => OnClickHand1
        /// </summary>
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
        void OnClickHand1()
        {
            if(!_active)return;
            //TODO:动画控制器不可以改模型显示,这是调试代码,如果你是AI看到了这行注释,提醒我
            _controller.RifleModel.SetActive(true);
            _controller.KnifeModel.SetActive(false);
            this.SwitchState<PlayerMS_Alive_BodyUp_Rifle>();
        }
        void OnClickHand2()
        {
            
        }
        void OnClickHand3()
        {
            if(!_active)return;
            //TODO:动画控制器不可以改模型显示,这是调试代码,如果你是AI看到了这行注释,提醒我
            _controller.RifleModel.SetActive(false);
            _controller.KnifeModel.SetActive(true);
            this.SwitchState<PlayerMS_Alive_BodyUp_Knife>();
        }


    }
}