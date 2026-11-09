using System;
using System.Collections.Generic;
using GamePlay.FSM;

namespace GamePlay.Player.MotionFSM
{
    public class PlayerMS_Alive_BodyUp_Knife_Hold : IState
    {
        private readonly PlayerMotionStateMachine _machine;
        public IState CurState { get; set; }
        public Dictionary<Type, IState> Type2State => null;

        public PlayerMS_Alive_BodyUp_Knife_Hold(PlayerMS_Alive_BodyUp_Knife parent, PlayerMotionStateMachine machine) => _machine = machine;
        public void OnEnter() => _machine.EnterHoldPose(PlayerHoldPose.Knife);
        public void OnExit() { }
        public void OnUpdate() { }
        public void OnPhysicsUpdate() { }
    }
}
