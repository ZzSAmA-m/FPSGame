using System;
using System.Collections.Generic;
using GamePlay.FSM;
using GamePlay.Player;
using GamePlay.Player.MotionFSM;
using UnityEngine;

public class PlayerMS_Death : IState
{
    PlayerMotionState _motionState;
    PlayerAnimatorController _controller;
    public PlayerMS_Death(PlayerMotionState motionState,PlayerAnimatorController controller)
    {
        _motionState=motionState;
        _controller=controller;
    }

    public IState CurState { get => throw new System.NotImplementedException(); set => throw new System.NotImplementedException(); }

    public Dictionary<Type, IState> Type2State =>null;

    public void OnEnter()
    {
        throw new System.NotImplementedException();
    }

    public void OnExit()
    {
        throw new System.NotImplementedException();
    }

    public void OnPhysicsUpdate()
    {
        throw new System.NotImplementedException();
    }

    public void OnUpdate()
    {
        throw new System.NotImplementedException();
    }
}
