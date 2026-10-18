using System;
using System.Collections.Generic;
using GamePlay.FSM;
using GamePlay.Player;
using GamePlay.Player.MotionFSM;
using UnityEngine;

public class PlayerMS_Alive_BodyDown_Stand : IState
{
    private PlayerMS_Alive_BodyDown _playerMS_Alive_BodyDown;

    private PlayerAnimatorController _controller;
    private Dictionary<Type,IState> _type2State;
    #region States
    private PlayerMS_Alive_BodyDown_Stand_Idle _playerMS_Alive_BodyDown_Stand_Idle;
    private PlayerMS_Alive_BodyDown_Stand_Run  _playerMS_Alive_BodyDown_Stand_Run;
    #endregion
    public PlayerMS_Alive_BodyDown_Stand(PlayerMS_Alive_BodyDown playerMS_Alive_BodyDown,PlayerAnimatorController controller)
    {

        _playerMS_Alive_BodyDown=playerMS_Alive_BodyDown;

        _controller = controller;

        _playerMS_Alive_BodyDown_Stand_Idle= new(this,_controller);
        _playerMS_Alive_BodyDown_Stand_Run = new(this,_controller);
        _type2State = new()
        {
            {typeof(PlayerMS_Alive_BodyDown_Stand_Idle),_playerMS_Alive_BodyDown_Stand_Idle},
            {typeof(PlayerMS_Alive_BodyDown_Stand_Run), _playerMS_Alive_BodyDown_Stand_Run},
        };
    }

    public IState CurState { get; set ; }

    public Dictionary<Type, IState> Type2State =>_type2State;

    public void OnEnter()
    {
        this.SwitchState<PlayerMS_Alive_BodyDown_Stand_Idle>();
    }

    public void OnExit()
    {
        
    }

    public void OnPhysicsUpdate()
    {
        CurState.OnPhysicsUpdate();
    }

    public void OnUpdate()
    {
        CurState.OnUpdate();
    }
}
