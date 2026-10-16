using System;
using System.Collections.Generic;
using GamePlay.FSM;
using UnityEngine;
namespace GamePlay.Player.MotionFSM
{
    public static class PlayerMotionFSMUtil
    {
        public static void SwitchState<T>(this IState state) where T:IState
        {
            var dict=state.Type2State;
            var typeInfo=typeof(T);
            if (!dict.ContainsKey(typeInfo))
            {
                Debug.LogError($"[PlayerMotion FSM] 无法切换状态到 {nameof(T)}");
                return;
            }

            state.CurState?.OnExit();

            state.CurState=dict[typeInfo];

            state.CurState.OnEnter();

        }    
    }
}

