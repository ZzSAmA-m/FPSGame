using System;
using System.Collections.Generic;
using GamePlay.FSM;

namespace GamePlay.Player.MotionFSM
{
    public class PlayerMS_Alive_BodyDown_Stand_Idle : IState
    {
        private readonly PlayerMS_Alive_BodyDown_Stand _parent;
        private readonly PlayerMotionStateMachine _machine;
        public IState CurState { get; set; }
        public Dictionary<Type, IState> Type2State => null;

        public PlayerMS_Alive_BodyDown_Stand_Idle(PlayerMS_Alive_BodyDown_Stand parent, PlayerMotionStateMachine machine)
        {
            _parent = parent;
            _machine = machine;
        }

        public void OnEnter() => _machine.EnterLocomotion(EStandRunState.NONE);
        public void OnExit() { }
        public void OnPhysicsUpdate() { }

        public void OnUpdate()
        {
            if (_machine.LocalVelocity.magnitude > _machine.MovementThreshold)
                _parent.SwitchState<PlayerMS_Alive_BodyDown_Stand_Run>();
        }
    }
}
