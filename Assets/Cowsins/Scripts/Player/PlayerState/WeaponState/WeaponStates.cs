// ============================================================================
// WeaponStates.cs —— 武器状态机上下文（Context）。
// 作为武器有限状态机（FSM）的宿主组件，保存当前状态与状态工厂，并在 Unity
// 生命周期（Start/Update/FixedUpdate）中驱动当前状态的更新；同时集中缓存各
// 子系统引用，供所有武器状态共享，避免重复 GetComponent。
// ============================================================================
using UnityEngine;
namespace cowsins
{
    /// <summary>
    /// 武器状态机上下文：持有当前武器状态与状态工厂，负责驱动状态更新并共享各组件引用。
    /// </summary>
    public class WeaponStates : MonoBehaviour
    {
        WeaponBaseState _currentState; // 当前激活的武器状态
        WeaponStateFactory _states; // 武器状态工厂，用于创建/获取各状态实例

        public WeaponBaseState CurrentState { get { return _currentState; } set { _currentState = value; } } // 当前状态属性
        public WeaponStateFactory _States { get { return _states; } set { _states = value; } } // 状态工厂属性

        // Shared references stored in context to avoid repeated GetComponent calls across states.
        // 以下为各状态共享的引用，集中缓存以避免跨状态重复 GetComponent。
        public PlayerDependencies Dependencies { get; private set; } // 玩家依赖集合
        // 玩家控制接口（只读，供各状态读取玩家是否可控）
        public IPlayerControlProvider PlayerControlProvider { get; private set; } // IPlayerControlProvider is implemented in PlayerControl.cs
        // 玩家移动状态接口（只读，供各状态读取移动状态）
        public IPlayerMovementStateProvider PlayerMovement { get; private set; } // IPlayerMovementStateProvider is implemented in PlayerMovement.cs
        public WeaponController WeaponController { get; private set; } // 武器控制器
        public WeaponAnimator WeaponAnimator { get; private set; } // 武器动画控制器
        public InteractManager InteractManager { get; private set; } // 交互管理器

        public bool holding = false; // 标记玩家是否正按住射击键（用于 Press 射击方式去重）

        // 初始化：获取共享引用，创建状态工厂与默认状态并进入，
        // 同时订阅松开射击、掉落武器、收枪等事件。
        private void Start()
        {
            GetContextReferences();

            _states = new WeaponStateFactory(this);
            _currentState = _states.Default(); // 初始为默认状态
            _currentState.EnterState();

            Dependencies.InputManager.OnStopShoot += TriggerStopHolding; // 松开射击键时清除 holding
            InteractManager.userEvents.onDrop.AddListener(OnDropReset); // 掉落武器时重置为默认状态
            WeaponController.settings.userEvents.OnUnholster.AddListener(OnUnholster); // 收枪时切换到掏枪状态
        }

        // 组件禁用时：移除所有事件订阅，防止泄漏。
        private void OnDisable()
        {
            if (Dependencies != null && Dependencies.InputManager != null)
                Dependencies.InputManager.OnStopShoot -= TriggerStopHolding;
            if (InteractManager != null && InteractManager.userEvents != null && InteractManager.userEvents.onDrop != null)
                InteractManager.userEvents.onDrop.RemoveListener(OnDropReset);
            if (WeaponController != null && WeaponController.settings != null && WeaponController.settings.userEvents != null && WeaponController.settings.userEvents.OnUnholster != null)
                WeaponController.settings.userEvents.OnUnholster.RemoveListener(OnUnholster);
        }

        // 每帧更新：驱动当前状态的 UpdateState。
        private void Update()
        {
            _currentState.UpdateState();
        }

        // 固定时间步更新：驱动当前状态的 FixedUpdateState。
        private void FixedUpdate()
        {
            _currentState.FixedUpdateState();
        }

        /// <summary>
        /// Force to change a Weapon state by passing the desired new state.
        /// 强制切换武器状态：传入目标状态，先退出当前状态，再进入新状态。
        /// </summary>
        /// <param name="newState"></param>
        public void ForceChangeState(WeaponBaseState newState)
        {
            _currentState.ExitState(); // 退出当前状态
            _currentState = newState;
            _currentState.EnterState(); // 进入新状态
        }

        // 获取共享引用：从自身组件获取依赖集合、控制、移动、武器控制器、动画、交互等。
        private void GetContextReferences()
        {
            Dependencies = GetComponent<PlayerDependencies>();
            PlayerControlProvider = Dependencies.PlayerControl;
            PlayerMovement = Dependencies.PlayerMovementState;
            WeaponController = GetComponent<WeaponController>();
            WeaponAnimator = GetComponent<WeaponAnimator>();
            InteractManager = GetComponent<InteractManager>();
        }
        // 松开射击键时调用：清除 holding 标记。
        private void TriggerStopHolding() => holding = false;

        // 掉落武器事件回调：重置为默认武器状态。
        private void OnDropReset(Pickeable dummyPickeable)
        {
            ForceChangeState(_states.Default());
        }
        // 收枪事件回调：切换到掏枪状态。
        private void OnUnholster() => ForceChangeState(_states.Unholster());
    }
}