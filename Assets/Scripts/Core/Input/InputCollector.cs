using System;
using UnityEngine;
namespace Core.Input
{
    public sealed class InputCollector:MonoBehaviour
    {
        private GameInput _input;
        private bool _key1,_key2,_key3;

        void Awake()
        {
            _input=new();
        }
        void OnEnable()
        {
            _input.Player.Hand1.started+=OnPressKey1;
            _input.Player.Hand2.started+=OnPressKey2;
            _input.Player.Hand3.started+=OnPressKey3;
            _input.Enable();
        }
        void OnDisable()
        {
            _input.Player.Hand1.started-=OnPressKey1;
            _input.Player.Hand2.started-=OnPressKey2;
            _input.Player.Hand3.started-=OnPressKey3;
            _input.Disable();
        }

        

        void OnDestroy()
        {
            _input.Dispose();
            _input=null;
        }
        public PlayerInput GetSample()
        {
            PlayerInput input = new()
            {
                move=_input.Player.Move.ReadValue<Vector2>(),
                look=_input.Player.Look.ReadValue<Vector2>(),
                key1=_key1,
                key2=_key2,
                key3=_key3
            };
            ResetKeyState();
            return input;
        }

        void ResetKeyState()
        {
            _key1=false;
            _key2=false;
            _key3=false;
        }


        private void OnPressKey1(UnityEngine.InputSystem.InputAction.CallbackContext context)=>_key1=true;
        private void OnPressKey2(UnityEngine.InputSystem.InputAction.CallbackContext context)=>_key2=true;
        private void OnPressKey3(UnityEngine.InputSystem.InputAction.CallbackContext context)=>_key3=true;

    }

}
