using System;
using System.Collections.Generic;
using GamePlay.FSM;

namespace GamePlay.Player.MotionFSM
{
    public class PlayerFPMS_Knife : IState
    {
        private readonly PlayerMotionStateMachine _machine;
        public IState CurState { get; set; }
        public Dictionary<Type, IState> Type2State => null;

        public PlayerFPMS_Knife(PlayerFPMotionState parent, PlayerMotionStateMachine machine) => _machine = machine;
        public void OnEnter() => _machine.EnterHoldPose(PlayerHoldPose.Knife);
        public void OnExit() { }
        public void OnUpdate() { }
        public void OnPhysicsUpdate() { }
    }
}
