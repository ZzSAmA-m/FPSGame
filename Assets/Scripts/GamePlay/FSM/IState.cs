using System;
using System.Collections.Generic;
using UnityEngine;
namespace GamePlay.FSM{
    public interface IState
    {
        public IState CurState{get;set;}
        public Dictionary<Type,IState> Type2State{get;}
        public void OnEnter();
        public void OnExit();
        public void OnUpdate();
        public void OnPhysicsUpdate();
    }
}