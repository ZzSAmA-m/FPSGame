using System;
using System.Collections.Generic;
using GamePlay.FSM;

namespace GamePlay.Player.MotionFSM
{
    public class PlayerMS_Alive : IState
    {
        private readonly PlayerMS_Alive_BodyDown _bodyDown;
        private readonly PlayerMS_Alive_BodyUp _bodyUp;
        public IState CurState { get; set; }
        public Dictionary<Type, IState> Type2State => null;

        public PlayerMS_Alive(PlayerMotionState motionState, PlayerMotionStateMachine machine)
        {
            _bodyDown = new PlayerMS_Alive_BodyDown(this, machine);
            _bodyUp = new PlayerMS_Alive_BodyUp(this, machine);
        }

        public void OnEnter()
        {
            _bodyDown.OnEnter();
            _bodyUp.OnEnter();
        }

        public void OnExit()
        {
            _bodyDown.OnExit();
            _bodyUp.OnExit();
        }

        public void OnUpdate()
        {
            _bodyDown.OnUpdate();
            _bodyUp.OnUpdate();
        }

        public void OnPhysicsUpdate()
        {
            _bodyDown.OnPhysicsUpdate();
            _bodyUp.OnPhysicsUpdate();
        }
    }
}
