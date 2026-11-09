using System;
using System.Collections.Generic;
using GamePlay.FSM;
namespace GamePlay.Player.MotionFSM
{
    public static class PlayerMotionFSMUtil
    {
        public static void SwitchState<T>(this IState state) where T:IState
        {
            var dict=state.Type2State;
            var typeInfo=typeof(T);
            if (dict == null || !dict.TryGetValue(typeInfo, out IState next))
                throw new InvalidOperationException($"动作状态中不存在子状态 {typeInfo.Name}。");

            state.CurState?.OnExit();

            state.CurState=next;

            state.CurState.OnEnter();

        }    
    }
}

