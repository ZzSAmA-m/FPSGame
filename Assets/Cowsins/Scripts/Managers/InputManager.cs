// 文件：InputManager.cs
// 作用：玩家输入管理器：基于 Unity Input System 的 PlayerActions 读取所有游戏与 UI 输入，
//       将其转换为布尔/数值状态并通过事件广播给其它脚本，同时负责按键绑定（Rebind）的
//       加载、保存、重置等操作，是 FPS 引擎输入系统的中枢。
using UnityEngine;
using UnityEngine.InputSystem;
using System;
using TMPro;

namespace cowsins
{
    /// <summary>
    /// Manages player inputs and broadcasts them to other scripts.
    /// </summary>
    public class InputManager : MonoBehaviour
    {
        #region events
        // 各类输入触发事件：供其它系统（UI、武器、移动等）订阅并响应。
        public Action OnInventoryOpenPressed, OnInventoryFavOpenPressed;
        // 武器与交互相关事件：射击、瞄准、近战、丢弃、检视、暂停、手电、返回 UI 等。
        public Action OnDrop, OnInspect, OnMelee, OnShoot, OnStopShoot, OnTogglePause, OnToggleFlashlight,OnBackUI, OnAimPressed, OnAimReleased;
        // 移动相关事件：跳跃、冲刺、钩索、移动输入变化、蹲伏等。
        public Action OnJump, OnDash, OnStartGrapple, OnStopGrapple, OnMoveInputChanged, OnSprintPressed, OnSprintReleased, OnCrouchPressed, OnCrouchReleased;

        // 按键重绑定（Rebind）相关静态事件：完成 / 取消 / 开始。
        public static event Action rebindComplete;
        public static event Action rebindCanceled;
        public static event Action<InputAction, int> rebindStarted;
        #endregion

        #region variables
        // Inputs
        // 各输入状态的布尔快照（隐藏于 Inspector），供其它脚本读取当前输入状态。
        [HideInInspector] public bool Jumping, Sprinting, Crouching, Dashing, Shooting, Reloading, Aiming, Melee, Inspecting, Interacting, StartInteraction, Dropping, Nextweapon, Previousweapon,
            Pausing, OpenInventory, OpenFavMenu, ToggleFlashlight, Grappling, BackUI, SelectUI, WestButtonUI, NorthButtonUI;
        // 各类输入数值快照：移动轴 X/Y、滚轮、鼠标与手柄视角增量。
        [HideInInspector] public float X, Y, Scrolling, Mousex, Mousey, Controllerx, Controllery;


        // 全局共享的 PlayerActions 输入资源实例（静态，仅初始化一次）。
        public static PlayerActions inputActions;
        // 当前处于启用状态的 InputManager 实例计数，用于控制静态 inputActions 的启用/禁用。
        private static int enabledInstances = 0;

        // 玩家依赖组件引用。
        private PlayerDependencies playerDependencies;

        // 武器行为与武器引用接口，供某些武器输入逻辑使用。
        private IWeaponBehaviourProvider weaponBehaviour;
        private IWeaponReferenceProvider weaponController;

        // 上一次移动输入向量，用于检测移动输入是否发生变化。
        private Vector2 lastMoveInput;
        // 冲刺/蹲伏/瞄准的上一次状态，用于状态变化检测。
        private bool lastSprintState, lastCrouchState, lastAimState;
        // 冲刺/蹲伏/瞄准是否采用“切换模式”（按一次切换开关）。
        private bool alternateAiming, alternateSprint, alternateCrouch;

        // 各输入动作的回调委托，统一在 Awake 中构造、在 OnEnable/OnDisable 中订阅与退订。
        private System.Action<InputAction.CallbackContext> sprintStarted, sprintCanceled;
        private System.Action<InputAction.CallbackContext> crouchStarted, crouchCanceled;
        private System.Action<InputAction.CallbackContext> aimStarted, aimCanceled;
        private System.Action<InputAction.CallbackContext> pauseHandler, inventoryOpenHandler, inventoryFavOpenHandler;
        private System.Action<InputAction.CallbackContext> dropHandler, jumpHandler, dashHandler, inspectHandler;
        private System.Action<InputAction.CallbackContext> meleeHandler, flashlightHandler, grappleStartHandler, grappleStopHandler;
        private System.Action<InputAction.CallbackContext> backUIHandler, firingStartHandler, firingStopHandler;

        #endregion

        // 在 Awake 中统一构造所有输入回调委托（lambda）：将原始输入上下文映射为状态更新与事件触发。
        private void Awake()
        {
            // 冲刺：按下/松开时根据切换模式更新状态并触发对应事件。
            sprintStarted = ctx => HandleActionInput(ref Sprinting,ref lastSprintState,true,alternateSprint,OnSprintPressed,OnSprintReleased);
            sprintCanceled = ctx => HandleActionInput(ref Sprinting,ref lastSprintState,false,alternateSprint,OnSprintPressed,OnSprintReleased);
            crouchStarted = ctx => HandleActionInput(ref Crouching, ref lastCrouchState, true, alternateCrouch, OnCrouchPressed, OnCrouchReleased);
            crouchCanceled = ctx => HandleActionInput(ref Crouching, ref lastCrouchState, false, alternateCrouch, OnCrouchPressed, OnCrouchReleased);
            aimStarted = ctx => HandleActionInput(ref Aiming, ref lastAimState, true, alternateAiming, OnAimPressed, OnAimReleased);
            aimCanceled = ctx => HandleActionInput(ref Aiming, ref lastAimState, false, alternateAiming, OnAimPressed, OnAimReleased);
            pauseHandler = ctx => OnTogglePause?.Invoke();
            inventoryOpenHandler = ctx => OnInventoryOpenPressed?.Invoke();
            inventoryFavOpenHandler = ctx => OnInventoryFavOpenPressed?.Invoke();
            dropHandler = ctx => OnDrop?.Invoke();
            jumpHandler = ctx => OnJump?.Invoke();
            dashHandler = ctx => OnDash?.Invoke();
            inspectHandler = ctx => OnInspect?.Invoke();
            meleeHandler = ctx => OnMelee?.Invoke();
            flashlightHandler = ctx => OnToggleFlashlight?.Invoke();
            grappleStartHandler = ctx => OnStartGrapple?.Invoke();
            grappleStopHandler = ctx => OnStopGrapple?.Invoke();
            backUIHandler = ctx => OnBackUI?.Invoke();
            // 开火开始：置 Shooting 为 true 并触发 OnShoot。
            firingStartHandler = ctx => {
                Shooting = true;
                OnShoot?.Invoke();
            };
            // 开火结束：置 Shooting 为 false 并触发 OnStopShoot。
            firingStopHandler = ctx => {
                Shooting = false;
                OnStopShoot?.Invoke();
            };
        }

        // 启用时：初始化输入资源，订阅所有输入动作的 started/performed/canceled 回调，并维护启用实例计数。
        private void OnEnable()
        {
            Init();

            // track enabled instances
            enabledInstances++;

            // 订阅蹲伏/冲刺/瞄准的 started 与 canceled，用于切换/按住两种输入模式。
            inputActions.GameControls.Crouching.started += crouchStarted;
            inputActions.GameControls.Crouching.canceled += crouchCanceled;
            inputActions.GameControls.Sprinting.started += sprintStarted;
            inputActions.GameControls.Sprinting.canceled += sprintCanceled;
            inputActions.GameControls.Aiming.started += aimStarted;
            inputActions.GameControls.Aiming.canceled += aimCanceled;


            // 订阅其余一次性/持续动作的输入回调。
            inputActions.GameControls.Pause.started += pauseHandler;
            inputActions.GameControls.InventoryOpen.performed += inventoryOpenHandler;
            inputActions.GameControls.InventoryFavOpen.performed += inventoryFavOpenHandler;
            inputActions.GameControls.Drop.started += dropHandler;
            inputActions.GameControls.Jumping.started += jumpHandler;
            inputActions.GameControls.Dashing.started += dashHandler;
            inputActions.GameControls.Inspect.started += inspectHandler;
            inputActions.GameControls.Melee.started += meleeHandler;
            inputActions.GameControls.ToggleFlashLight.started += flashlightHandler;
            inputActions.GameControls.Grapple.started += grappleStartHandler;
            inputActions.GameControls.Grapple.canceled += grappleStopHandler;
            inputActions.UI.Back.started += backUIHandler;
            inputActions.GameControls.Firing.started += firingStartHandler;
            inputActions.GameControls.Firing.canceled += firingStopHandler;
        }

        // 禁用时：退订所有输入回调，并在没有剩余启用实例时禁用静态 inputActions。
        private void OnDisable()
        {
            // 先退订蹲伏/冲刺/瞄准的回调。
            inputActions.GameControls.Sprinting.started -= sprintStarted;
            inputActions.GameControls.Sprinting.canceled -= sprintCanceled;
            inputActions.GameControls.Crouching.started -= crouchStarted;
            inputActions.GameControls.Crouching.canceled -= crouchCanceled;
            inputActions.GameControls.Aiming.started -= aimStarted;
            inputActions.GameControls.Aiming.canceled -= aimCanceled;

            inputActions.GameControls.Pause.started -= pauseHandler;
            inputActions.GameControls.InventoryOpen.performed -= inventoryOpenHandler;
            inputActions.GameControls.InventoryFavOpen.performed -= inventoryFavOpenHandler;
            inputActions.GameControls.Drop.started -= dropHandler;
            inputActions.GameControls.Jumping.started -= jumpHandler;
            inputActions.GameControls.Dashing.started -= dashHandler;
            inputActions.GameControls.Inspect.started -= inspectHandler;
            inputActions.GameControls.Melee.started -= meleeHandler;
            inputActions.GameControls.ToggleFlashLight.started -= flashlightHandler;
            inputActions.GameControls.Grapple.started -= grappleStartHandler;
            inputActions.GameControls.Grapple.canceled -= grappleStopHandler;
            inputActions.UI.Back.started -= backUIHandler;
            inputActions.GameControls.Firing.started -= firingStartHandler;
            inputActions.GameControls.Firing.canceled -= firingStopHandler;

            // Only disable the static inputActions when no InputManager instances remain enabled
            enabledInstances = Mathf.Max(0, enabledInstances - 1);
            if (enabledInstances == 0)
            {
                inputActions.Disable();
            }
        }
        // 每帧读取各输入动作的最新状态：更新鼠标/手柄视角增量、移动输入以及各布尔状态快照，
        // 并在移动输入发生变化时触发 OnMoveInputChanged 事件。
        private void Update()
        {
            // 尚未绑定玩家依赖时直接返回，避免空引用。
            if (playerDependencies == null) return;

            // 读取鼠标增量并写入 Mousex/Mousey（NaN 时置 0 兜底）。
            if (Mouse.current != null)
            {
                float dx = Mouse.current.delta.x.ReadValue();
                float dy = Mouse.current.delta.y.ReadValue();

                Mousex = float.IsNaN(dx) ? 0f : dx;
                Mousey = float.IsNaN(dy) ? 0f : dy;
            }

            // 读取手柄右摇杆增量（Y 轴取反以匹配视角方向），并写入 Controllerx/Controllery。
            if (Gamepad.current != null)
            {
                float dx = Gamepad.current.rightStick.x.ReadValue();
                float dy = -Gamepad.current.rightStick.y.ReadValue();

                Controllerx = float.IsNaN(dx) ? 0f : dx;
                Controllery = float.IsNaN(dy) ? 0f : dy;
            }

            // 读取移动输入向量，若与上一帧不同则更新 X/Y 并触发 OnMoveInputChanged。
            Vector2 moveInput = inputActions.GameControls.Movement.ReadValue<Vector2>();
            if (moveInput != lastMoveInput)
            {
                lastMoveInput = moveInput;

                X = moveInput.x;
                Y = moveInput.y;
                OnMoveInputChanged?.Invoke();
            }

            // 以下批量读取各动作的最新状态，写入对应的布尔/数值字段。
            Reloading = inputActions.GameControls.Reloading.IsPressed();
            Melee = inputActions.GameControls.Melee.WasPressedThisFrame();

            // 滚轮数值与切换武器（按滚轮方向区分上一把/下一把）。
            Scrolling = inputActions.GameControls.Scrolling.ReadValue<Vector2>().y;
            Nextweapon = inputActions.GameControls.ChangeWeapons.WasPressedThisFrame() && inputActions.GameControls.ChangeWeapons.ReadValue<float>() > 0;
            Previousweapon = inputActions.GameControls.ChangeWeapons.WasPressedThisFrame() && inputActions.GameControls.ChangeWeapons.ReadValue<float>() < 0;

            // 交互、背包、丢弃、检视、手电、钩索、冲刺、跳跃等状态。
            Interacting = inputActions.GameControls.Interacting.IsPressed();
            StartInteraction = inputActions.GameControls.Interacting.WasPressedThisFrame();
            OpenInventory = inputActions.GameControls.InventoryOpen.WasPressedThisFrame();
            OpenFavMenu = inputActions.GameControls.InventoryFavOpen.WasPressedThisFrame();
            Dropping = inputActions.GameControls.Drop.WasPressedThisFrame();

            Inspecting = inputActions.GameControls.Inspect.IsPressed();

            ToggleFlashlight = inputActions.GameControls.ToggleFlashLight.WasPressedThisFrame();

            Grappling = inputActions.GameControls.Grapple.IsPressed();

            Dashing = inputActions.GameControls.Dashing.WasPressedThisFrame();
            Jumping = inputActions.GameControls.Jumping.WasPressedThisFrame();

            // UI 相关输入状态。
            BackUI = inputActions.UI.Back.WasPressedThisFrame();
            SelectUI = inputActions.UI.Select.WasPressedThisFrame();
            WestButtonUI = inputActions.UI.WestButton.WasPressedThisFrame();
            NorthButtonUI = inputActions.UI.NorthButton.WasPressedThisFrame();
            Pausing = inputActions.GameControls.Pause.WasPressedThisFrame();
        }

        // 在物理帧更新移动输入的 Y 值（用于与物理同步的移动逻辑）。
        private void FixedUpdate()
        {
            Y = inputActions.GameControls.Movement.ReadValue<Vector2>().y;
        }

        /// <summary>
        /// 强制解除蹲伏状态：若当前处于蹲伏则置为 false 并触发蹲伏释放事件。
        /// </summary>
        public void ForceUncrouch()
        {
            if (Crouching)
            {
                Crouching = false;
                lastCrouchState = false;
                OnCrouchReleased?.Invoke();
            }
        }

        #region others

        // 统一处理“按住 / 切换”两种输入模式下的状态更新与事件派发：
        // 切换模式下按下时翻转状态，按住模式下状态等于是否按下；仅当状态发生变化时才更新并触发对应事件。
        private void HandleActionInput(ref bool currentState, ref bool lastState, bool pressed, bool useToggleMode, Action onPressed, Action onReleased)
        {
            bool newState = currentState;

            if (useToggleMode)
            {
                if (pressed)
                    newState = !currentState;
            }
            else
            {
                newState = pressed;
            }

            if (newState != lastState)
            {
                currentState = newState;
                lastState = newState;

                if (newState)
                    onPressed?.Invoke();
                else
                    onReleased?.Invoke();
            }
        }

        /// <summary>
        /// 启用或禁用游戏玩法输入动作集（GameControls）。
        /// </summary>
        /// <param name="enable">true 启用，false 禁用。</param>
        public void ToggleGameControls(bool enable)
        {
            if (enable) inputActions.GameControls.Enable();
            else inputActions.GameControls.Disable();
        }

        /// <summary>
        /// 启用或禁用 UI 输入动作集（UI）。
        /// </summary>
        /// <param name="enable">true 启用，false 禁用。</param>
        public void ToggleUIControls(bool enable)
        {
            if (enable) inputActions.UI.Enable();
            else inputActions.UI.Disable();
        }

        /// <summary>
        /// 汇总鼠标与手柄的水平视角输入增量（乘以灵敏度与时间增量）。
        /// </summary>
        /// <param name="currentSensX">当前鼠标水平灵敏度。</param>
        /// <param name="currentControllerSensX">当前手柄水平灵敏度。</param>
        /// <returns>综合后的水平视角增量。</returns>
        public float GatherRawMouseX(float currentSensX, float currentControllerSensX)
        {
            return (Mousex * currentSensX * Time.fixedDeltaTime + Controllerx * Time.deltaTime * currentControllerSensX); 
        }

        /// <summary>
        /// 汇总鼠标与手柄的垂直视角输入增量（考虑 Y 轴反转设置）。
        /// </summary>
        /// <param name="sensYInverted">鼠标 Y 轴是否反转（1 正常，-1 反转）。</param>
        /// <param name="sensYInvertedController">手柄 Y 轴是否反转（1 正常，-1 反转）。</param>
        /// <param name="currentSensY">当前鼠标垂直灵敏度。</param>
        /// <param name="currentControllerSensY">当前手柄垂直灵敏度。</param>
        /// <returns>综合后的垂直视角增量。</returns>
        public float GatherRawMouseY(int sensYInverted, int sensYInvertedController, float currentSensY, float currentControllerSensY)
        {
            return (Mousey * currentSensY * sensYInverted * Time.fixedDeltaTime + Controllery * sensYInvertedController * Time.deltaTime * currentControllerSensY);
        }
        // 初始化输入系统：创建并启用 PlayerActions、加载已保存的按键覆盖，并设置游戏/UI 动作集的初始启用状态。
        private void Init()
        {
            // Initialize Inputs
            if (inputActions == null) inputActions = new PlayerActions();
            inputActions.Enable();
            
            // Load saved bindings overrides
            LoadAllBindings();

            ToggleGameControls(true);
            ToggleUIControls(false);
        }

        // 遍历 PlayerActions 中所有动作映射（Action Map）与动作，逐一从 PlayerPrefs 加载每个绑定的覆盖。
        private void LoadAllBindings()
        {
            // Iterate through all the acction maps in the Player Actions
            foreach (var actionMap in inputActions.asset.actionMaps)
            {
                foreach (var action in actionMap.actions)
                {
                    // For each of the bindings of each of the actions, load the binding binding from PlayerPrefs
                    for (int i = 0; i < action.bindings.Count; i++)
                    {
                        LoadBindingOverride(action, i);
                    }
                }
            }
        }

        // 从 PlayerPrefs 读取指定动作某个绑定的覆盖路径，若非空则应用到该绑定。
        private static void LoadBindingOverride(InputAction action, int bindingIndex)
        {

            // Gather the path from the Player Prefs
            string overridePath = PlayerPrefs.GetString(action.actionMap + action.name + bindingIndex);
            // If the path is valid, apply it to the action that needs to be loaded.
            if (!string.IsNullOrEmpty(overridePath))
            {
                action.ApplyBindingOverride(bindingIndex, overridePath);
            }
        }

        /// <summary>
        /// Sets the player that the InputManager will take as a reference
        /// </summary>
        /// <param name="player"></param>
        public void SetPlayer(PlayerDependencies player)
        {
            this.playerDependencies = player;

            weaponBehaviour = player.WeaponBehaviour;
            weaponController = player.WeaponReference;
        }
        
        /// <summary>
        /// 从玩家移动设置中读取冲刺/蹲伏是否采用切换模式。
        /// </summary>
        /// <param name="playerSettings">玩家移动设置。</param>
        public void SetPlayerInputModes(PlayerMovementSettings playerSettings)
        {
            this.alternateSprint = playerSettings.alternateSprint; 
            this.alternateCrouch = playerSettings.alternateCrouch;
        }

        /// <summary>
        /// 从武器控制器设置中读取瞄准是否采用切换模式。
        /// </summary>
        /// <param name="weaponControllerSettings">武器控制器设置。</param>
        public void SetWeaponInputModes(WeaponControllerSettings weaponControllerSettings)
        {
            this.alternateAiming = weaponControllerSettings.alternateAiming;
        }

        /// <summary>
        /// 启动按键重绑定流程：按名称找到输入动作并校验绑定索引，若为复合绑定则逐个重绑定其组成部分。
        /// </summary>
        /// <param name="actionName">输入动作名称。</param>
        /// <param name="bindingIndex">绑定索引。</param>
        /// <param name="statusTxt">用于显示重绑定提示的文本。</param>
        /// <param name="excludeMouse">是否在重绑定期间排除鼠标输入。</param>
        /// <param name="rebindOverlay">重绑定期间显示的遮罩 UI。</param>
        /// <param name="rebindOverlayTitle">遮罩 UI 的标题文本。</param>
        public static void StartRebind(string actionName, int bindingIndex, TextMeshProUGUI statusTxt, bool excludeMouse, GameObject rebindOverlay, TextMeshProUGUI rebindOverlayTitle)
        {
            // Find the Input Action based on its name
            InputAction action = inputActions.asset.FindAction(actionName);

            if (action == null || action.bindings.Count <= bindingIndex)
            {
                Debug.LogError("Action or Binding not Found");
                return;
            }

            // If it is valid check if it is a composite
            // Iterate through each each composite part and rebind it
            if (action.bindings[bindingIndex].isComposite)
            {
                var firstPartIndex = bindingIndex + 1;

                if (firstPartIndex < action.bindings.Count && action.bindings[firstPartIndex].isComposite) PerformRebind(action, bindingIndex, statusTxt, true, excludeMouse, rebindOverlay, rebindOverlayTitle);
            }
            else PerformRebind(action, bindingIndex, statusTxt, false, excludeMouse, rebindOverlay, rebindOverlayTitle);
        }
        // 执行单个绑定的交互式重绑定：显示提示、禁用动作、发起 PerformInteractiveRebinding，
        // 完成时保存覆盖并继续下一个复合部分，取消时恢复，并支持 Esc 取消与排除鼠标。
        private static void PerformRebind(InputAction actionToRebind, int bindingIndex, TextMeshProUGUI statusTxt, bool allCompositeParts, bool excludeMouse, GameObject rebindOverlay, TextMeshProUGUI rebindOverlayTitle)
        {
            if (actionToRebind == null || bindingIndex < 0)
                return;

            // Update the text status
            statusTxt.text = $"Press a {actionToRebind.expectedControlType}";
            rebindOverlay.SetActive(true);
            rebindOverlayTitle.text = $"Rebinding {actionToRebind.name}";
            actionToRebind.Disable();

            var rebind = actionToRebind.PerformInteractiveRebinding(bindingIndex);

            // Handle rebind completion
            rebind.OnComplete(operation =>
            {
                rebindOverlay.SetActive(false);
                // Enable the rebind and stop the operation
                actionToRebind.Enable();
                operation.Dispose();

                // Rebind for Composite
                if (allCompositeParts)
                {
                    var nextBindingIndex = bindingIndex + 1;
                    if (nextBindingIndex < actionToRebind.bindings.Count && actionToRebind.bindings[nextBindingIndex].isComposite) PerformRebind(actionToRebind, nextBindingIndex, statusTxt, allCompositeParts, excludeMouse, rebindOverlay, rebindOverlayTitle);
                }

                // Save the new rebinds
                SaveBindingOverride(actionToRebind);

                rebindComplete?.Invoke();
            });

            // Handle rebind cancel
            rebind.OnCancel(operation =>
            {
                rebindOverlay.SetActive(false);
                actionToRebind.Enable();
                operation.Dispose();

                rebindCanceled?.Invoke();
            });

            // Cancel rebind if pressing escape
            rebind.WithCancelingThrough("<Keyboard>/escape");

            // Exclude mouse
            if (excludeMouse)
                rebind.WithControlsExcluding("<Mouse>/escape");

            rebindStarted?.Invoke(actionToRebind, bindingIndex);
            // Actually start the rebind process
            rebind.Start();
        }


        /// <summary>
        /// Retrieve the name of a binding.
        /// </summary>
        /// <param name="actionName"></param>
        /// <param name="bindingIndex"></param>
        /// <returns></returns>
        public static string GetBindingName(string actionName, int bindingIndex)
        {
            if (inputActions == null) inputActions = new PlayerActions();

            InputAction action = inputActions.asset.FindAction(actionName);
            return action.GetBindingDisplayString(bindingIndex);
        }

        // 保存某个动作所有绑定的覆盖路径到 PlayerPrefs。
        // Save the bindings into player prefs for each action
        private static void SaveBindingOverride(InputAction action)
        {
            for (int i = 0; i < action.bindings.Count; i++)
            {
                PlayerPrefs.SetString(action.actionMap + action.name + i, action.bindings[i].overridePath);
            }
        }

        /// <summary>
        /// 按动作名称从 PlayerPrefs 加载该动作所有绑定的覆盖并应用。
        /// </summary>
        /// <param name="actionName">输入动作名称。</param>
        public static void LoadBindingOverride(string actionName)
        {
            if (inputActions == null)
                inputActions = new PlayerActions();
            // Gather the Input Action given its name
            InputAction action = inputActions.asset.FindAction(actionName);

            // For each binding apply the binding from PlayerPrefs
            for (int i = 0; i < action.bindings.Count; i++)
            {
                if (!string.IsNullOrEmpty(PlayerPrefs.GetString(action.actionMap + action.name + i)))
                    action.ApplyBindingOverride(i, PlayerPrefs.GetString(action.actionMap + action.name + i));
            }
        }

        /// <summary>
        /// Reset the bindings for the given action
        /// </summary>
        /// <param name="actionName"></param>
        /// <param name="bindingIndex"></param>
        public static void ResetBinding(string actionName, int bindingIndex)
        {
            // Gather the Input Action given its name
            InputAction action = inputActions.asset.FindAction(actionName);

            if (action == null || action.bindings.Count <= bindingIndex)
            {
                Debug.LogError("Action or Binding not found");
                return;
            }
            if (action.bindings[bindingIndex].isComposite)
            {
                for (int i = bindingIndex; i < action.bindings.Count && action.bindings[i].isComposite; i++)
                    action.RemoveBindingOverride(i);
            }
            else
                action.RemoveBindingOverride(bindingIndex);

            SaveBindingOverride(action);
        }

        /// <summary>
        /// 重置所有动作映射下所有绑定的覆盖，并保存结果，最后触发 rebindComplete 事件。
        /// </summary>
        public static void ResetAllBindings()
        {
            if (inputActions == null) return;
            foreach (var actionMap in inputActions.asset.actionMaps)
            {
                foreach (var action in actionMap.actions)
                {
                    action.RemoveAllBindingOverrides();
                    SaveBindingOverride(action);
                }
            }
            rebindComplete?.Invoke();
        }
        #endregion
    }

}
