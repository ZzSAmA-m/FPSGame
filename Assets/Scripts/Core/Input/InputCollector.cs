using UnityEngine;
using UnityEngine.InputSystem;

namespace Core.Input
{
    public sealed class InputCollector:MonoBehaviour
    {
        private GameInput _input;
        private bool _key1,_key2,_key3,_reload;
        private bool _jumpPressed;
        private bool _lightAttackPressed;
        private bool _heavyAttackPressed;

        void Awake()
        {
            _input=new();
        }
        void OnEnable()
        {
            _input.Player.Hand1.started+=OnPressKey1;
            _input.Player.Hand2.started+=OnPressKey2;
            _input.Player.Hand3.started+=OnPressKey3;
            _input.Player.Reload.started+=OnReload;
            _input.Player.Jump.performed += OnJump;
            _input.Player.LMouse.started += OnLightAttack;
            _input.Player.RMouse.started += OnHeavyAttack;
            _input.Enable();
        }
        void OnDisable()
        {
            _input.Player.Hand1.started-=OnPressKey1;
            _input.Player.Hand2.started-=OnPressKey2;
            _input.Player.Hand3.started-=OnPressKey3;
            _input.Player.Reload.started-=OnReload;
            _input.Player.Jump.performed -= OnJump;
            _input.Player.LMouse.started -= OnLightAttack;
            _input.Player.RMouse.started -= OnHeavyAttack;
            _input.Disable();
            ClearPendingActions();
        }

        

        void OnDestroy()
        {
            _input.Dispose();
            _input=null;
        }
        public PlayerInput GetSample()
        {
            Mouse mouse = Mouse.current;
            PlayerInput input = new()
            {
                move = _input.Player.Move.ReadValue<Vector2>(),
                look = _input.Player.Look.ReadValue<Vector2>(),
                key1 = _key1,
                key2 = _key2,
                key3 = _key3,
                r = _reload,
                jumpPressed = _jumpPressed,
                lMouse = mouse != null && mouse.leftButton.isPressed,
                lightAttackPressed = _lightAttackPressed,
                heavyAttackPressed = _heavyAttackPressed
            };
            ClearPendingActions();
            return input;
        }

        internal void ClearPendingActions()
        {
            _key1=false;
            _key2=false;
            _key3=false;
            _reload=false;
            _jumpPressed = false;
            _lightAttackPressed = false;
            _heavyAttackPressed = false;
        }


        private void OnPressKey1(UnityEngine.InputSystem.InputAction.CallbackContext context)=>_key1=true;
        private void OnPressKey2(UnityEngine.InputSystem.InputAction.CallbackContext context)=>_key2=true;
        private void OnPressKey3(UnityEngine.InputSystem.InputAction.CallbackContext context)=>_key3=true;
        private void OnReload(UnityEngine.InputSystem.InputAction.CallbackContext context)=>_reload=true;
        private void OnJump(InputAction.CallbackContext context) => _jumpPressed = true;
        private void OnLightAttack(InputAction.CallbackContext context) => _lightAttackPressed = true;
        private void OnHeavyAttack(InputAction.CallbackContext context) => _heavyAttackPressed = true;

    }

}
