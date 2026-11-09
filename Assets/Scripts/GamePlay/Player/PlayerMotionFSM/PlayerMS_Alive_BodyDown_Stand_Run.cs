using System;
using System.Collections.Generic;
using GamePlay.FSM;

namespace GamePlay.Player.MotionFSM
{
    public class PlayerMS_Alive_BodyDown_Stand_Run : IState
    {
        private readonly PlayerMS_Alive_BodyDown_Stand _parent;
        private readonly PlayerMotionStateMachine _machine;
        private EStandRunState _direction;
        public IState CurState { get; set; }
        public Dictionary<Type, IState> Type2State => null;

        public PlayerMS_Alive_BodyDown_Stand_Run(PlayerMS_Alive_BodyDown_Stand parent, PlayerMotionStateMachine machine)
        {
            _parent = parent;
            _machine = machine;
        }

        public void OnEnter() => _direction = EStandRunState.NONE;
        public void OnExit() { }
        public void OnPhysicsUpdate() { }

        public void OnUpdate()
        {
            var velocity = _machine.LocalVelocity;
            float threshold = _machine.MovementThreshold;
            EStandRunState next;
            if (velocity.z > threshold) next = EStandRunState.N;
            else if (velocity.z < -threshold) next = EStandRunState.S;
            else if (velocity.x > threshold) next = EStandRunState.E;
            else if (velocity.x < -threshold) next = EStandRunState.W;
            else
            {
                _parent.SwitchState<PlayerMS_Alive_BodyDown_Stand_Idle>();
                return;
            }

            if (_direction == next)
                return;
            _direction = next;
            _machine.EnterLocomotion(next);
        }
    }
}
