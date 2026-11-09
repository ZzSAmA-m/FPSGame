using System;
using System.Collections.Generic;
using GamePlay.FSM;

namespace GamePlay.Player.MotionFSM
{
    /// <summary>保留尚未实现的死亡动作状态，不在动作与表现分离中补充死亡规则。</summary>
    public class PlayerMS_Death : IState
    {
        public PlayerMS_Death(PlayerMotionState motionState, PlayerMotionStateMachine machine) { }
        public IState CurState { get; set; }
        public Dictionary<Type, IState> Type2State => null;
        public void OnEnter() => throw new NotImplementedException();
        public void OnExit() { }
        public void OnUpdate() { }
        public void OnPhysicsUpdate() { }
    }
}
