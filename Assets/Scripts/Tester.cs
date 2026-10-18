using UnityEngine;
using Cysharp.Threading.Tasks;
using Sirenix.OdinInspector;
using Core.Input;
using System;
using System.Runtime.InteropServices;
using Core.ZLog;
public class Tester : MonoBehaviour
{

    void Start()
    {
        ZLog.Log<Tester>("OK");
    }
}
