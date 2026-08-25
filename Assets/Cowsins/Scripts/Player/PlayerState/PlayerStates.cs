// ============================================================
// 文件：PlayerStates.cs
// 作用：玩家状态机核心组件（挂在玩家身上的 MonoBehaviour）。保存当前状态与状态工厂，
//       集中缓存共享引用，并在 Unity 生命周期（Start/Update/FixedUpdate）中驱动当前状态。
// ============================================================
using UnityEngine;
namespace cowsins
{
    /// <summary>
    /// 玩家状态机核心：维护当前状态与状态工厂，缓存共享引用，并在 Unity 生命周期中驱动当前状态。
    /// </summary>
    public class PlayerStates : MonoBehaviour
    {
        // 当前正在运行的玩家状态实例
        PlayerBaseState _currentState;
        // 状态工厂，负责创建/缓存各玩家状态实例
        PlayerStateFactory _states;

        /// <summary>当前正在运行的玩家状态（可读写，供状态切换时更新）。</summary>
        public PlayerBaseState CurrentState { get { return _currentState; } set { _currentState = value; } }
        /// <summary>状态工厂（可读写），负责创建/缓存各玩家状态。</summary>
        public PlayerStateFactory _States { get { return _states; } set { _states = value; } }

        // Shared references stored in context to avoid repeated GetComponent calls across states.
        /// <summary>玩家控制提供者接口，供各状态读取是否可操控等信息。</summary>
        public IPlayerControlProvider PlayerControlProvider { get; private set; } // IPlayerControlProvider is implemented in PlayerControl.cs
        /// <summary>玩家属性提供者接口，供各状态订阅死亡等属性事件。</summary>
        public IPlayerStatsProvider PlayerStatsProvider { get; private set; } // IPlayerStatsProvider is implemented in PlayerStats.cs
        /// <summary>玩家刚体组件，供各状态进行物理操作。</summary>
        public Rigidbody Rigidbody { get; private set; }
        /// <summary>玩家移动组件，封装移动/攀爬/滑铲等行为。</summary>
        public PlayerMovement PlayerMovement { get; private set; }
        /// <summary>输入管理器，供各状态读取玩家按键输入。</summary>
        public InputManager InputManager { get; private set; }

        // Unity 生命周期：初始化时获取共享引用，创建状态工厂并进入默认状态
        private void Start()
        {
            GetContextReferences();

            _states = new PlayerStateFactory(this);
            _currentState = _states.Default();
            _currentState.EnterState();
        }

        // Unity 生命周期：每帧驱动当前状态的更新逻辑
        private void Update()
        {
            _currentState.UpdateState();
        }

        // Unity 生命周期：按固定物理步长驱动当前状态的物理更新逻辑
        private void FixedUpdate()
        {
            _currentState.FixedUpdateState();
        }

        // 强制切换玩家状态：先退出当前状态，再进入指定新状态
        /// <summary>
        /// Force to change a Player state by passing the desired new state.
        /// </summary>
        /// <param name="newState"></param>
        public void ForceChangeState(PlayerBaseState newState)
        {
            _currentState.ExitState();
            _currentState = newState;
            _currentState.EnterState();
        }

        /// <summary>判断当前状态是否为指定类型 T。</summary>
        /// <typeparam name="T">要判断的玩家状态类型。</typeparam>
        /// <returns>若当前状态是 T 类型则返回 true，否则返回 false。</returns>
        public bool IsInState<T>() where T : PlayerBaseState
        {
            return _currentState is T;
        }

        // 集中获取并缓存各共享引用，避免各状态重复调用 GetComponent
        private void GetContextReferences()
        {
            PlayerControlProvider = GetComponent<IPlayerControlProvider>();
            Rigidbody = GetComponent<Rigidbody>();
            PlayerMovement = GetComponent<PlayerMovement>();
            PlayerStatsProvider = GetComponent<IPlayerStatsProvider>();
            InputManager = GetComponent<PlayerDependencies>().InputManager;
        }
    }
}