using System;
using System.Collections.Generic;
using GamePlay.FSM;

namespace GamePlay.Player.MotionFSM
{
    public class PlayerMotionState : IState
    {
        private readonly Dictionary<Type, IState> _states;
        public IState CurState { get; set; }
        public Dictionary<Type, IState> Type2State => _states;

        public PlayerMotionState(PlayerMotionStateMachine machine)
        {
            _states = new Dictionary<Type, IState>
            {
                { typeof(PlayerMS_Alive), new PlayerMS_Alive(this, machine) },
                { typeof(PlayerMS_Death), new PlayerMS_Death(this, machine) }
            };
        }

        public void OnEnter() => this.SwitchState<PlayerMS_Alive>();

        public void OnExit()
        {
            CurState?.OnExit();
            CurState = null;
        }

        public void OnUpdate() => CurState?.OnUpdate();
        public void OnPhysicsUpdate() => CurState?.OnPhysicsUpdate();
    }
}
