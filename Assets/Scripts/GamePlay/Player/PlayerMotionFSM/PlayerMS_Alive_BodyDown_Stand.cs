using System;
using System.Collections.Generic;
using GamePlay.FSM;

namespace GamePlay.Player.MotionFSM
{
    public class PlayerMS_Alive_BodyDown_Stand : IState
    {
        private readonly Dictionary<Type, IState> _states;
        public IState CurState { get; set; }
        public Dictionary<Type, IState> Type2State => _states;

        public PlayerMS_Alive_BodyDown_Stand(PlayerMS_Alive_BodyDown bodyDown, PlayerMotionStateMachine machine)
        {
            _states = new Dictionary<Type, IState>
            {
                { typeof(PlayerMS_Alive_BodyDown_Stand_Idle), new PlayerMS_Alive_BodyDown_Stand_Idle(this, machine) },
                { typeof(PlayerMS_Alive_BodyDown_Stand_Run), new PlayerMS_Alive_BodyDown_Stand_Run(this, machine) }
            };
        }

        public void OnEnter() => this.SwitchState<PlayerMS_Alive_BodyDown_Stand_Idle>();

        public void OnExit()
        {
            CurState?.OnExit();
            CurState = null;
        }

        public void OnUpdate() => CurState?.OnUpdate();
        public void OnPhysicsUpdate() => CurState?.OnPhysicsUpdate();
    }
}
