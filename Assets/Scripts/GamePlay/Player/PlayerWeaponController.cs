using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Core.Input;
using Core.Utility;
using TMPro;
using UnityEngine;

public class PlayerWeaponController : MonoBehaviour
{
    public event Action<WeaponInputAction> OnWeaponInvoked;
    
    public void Simulate(ref PlayerInput input)
    {
        Span<WeaponInputAction> actions=stackalloc WeaponInputAction[30];
        var ptr=GetActionFromInput(ref input,actions);
        for(int i=0;i<ptr;++i) OnWeaponInvoked?.Invoke(actions[i]);
    }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static int GetActionFromInput(ref PlayerInput input,Span<WeaponInputAction> actions)
    {
        int ptr=0;
        if (input.lMouse)   actions[ptr++]=WeaponInputAction.MLeft;
        if(input.key1)      actions[ptr++]=WeaponInputAction.Key1;
        if(input.key2)      actions[ptr++]=WeaponInputAction.Key2;
        if(input.key3)      actions[ptr++]=WeaponInputAction.Key3;
        if(input.r)         actions[ptr++]=WeaponInputAction.R;
        if(input.space)     actions[ptr++]=WeaponInputAction.Space;
        return ptr;
    }
}
