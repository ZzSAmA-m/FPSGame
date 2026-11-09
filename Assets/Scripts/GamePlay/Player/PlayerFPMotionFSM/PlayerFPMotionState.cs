using System;
using System.Collections.Generic;
using Core.Utility;
using GamePlay.FSM;

namespace GamePlay.Player.MotionFSM
{
    public class PlayerFPMotionState : IState
    {
        private readonly PlayerMotionStateMachine _machine;
        private readonly Dictionary<Type, IState> _states;
        private bool _active;
        public IState CurState { get; set; }
        public Dictionary<Type, IState> Type2State => _states;

        public PlayerFPMotionState(PlayerMotionStateMachine machine)
        {
            _machine = machine;
            _states = new Dictionary<Type, IState>
            {
                { typeof(PlayerFPMS_Knife), new PlayerFPMS_Knife(this, machine) },
                { typeof(PlayerFPMS_Rifle), new PlayerFPMS_Rifle(this, machine) }
            };
        }

        public void OnEnter()
        {
            _active = true;
            _machine.EquipmentChanged += ApplyEquipment;
            _machine.WeaponInput += OnWeaponInput;
            ApplyEquipment();
        }

        public void OnExit()
        {
            _active = false;
            _machine.EquipmentChanged -= ApplyEquipment;
            _machine.WeaponInput -= OnWeaponInput;
            CurState?.OnExit();
            CurState = null;
        }

        public void OnUpdate() => CurState?.OnUpdate();
        public void OnPhysicsUpdate() => CurState?.OnPhysicsUpdate();

        private void ApplyEquipment()
        {
            if (!_active) return;
            if (_machine.EquipmentPose == PlayerHoldPose.Rifle)
                this.SwitchState<PlayerFPMS_Rifle>();
            else
                this.SwitchState<PlayerFPMS_Knife>();
        }

        private void OnWeaponInput(WeaponInputAction action)
        {
            if (!_active || _machine.HasEquippedWeapon) return;
            if (action == WeaponInputAction.Key1)
                this.SwitchState<PlayerFPMS_Rifle>();
            else if (action == WeaponInputAction.Key3)
                this.SwitchState<PlayerFPMS_Knife>();
        }
    }
}
