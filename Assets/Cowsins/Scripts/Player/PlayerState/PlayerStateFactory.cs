// ============================================================
// 文件：PlayerStateFactory.cs
// 作用：玩家状态工厂。集中创建并缓存各玩家状态实例（默认/跳跃/下蹲/死亡/攀爬），
//       对外提供统一的访问入口；冲刺状态因依赖运行时输入方向而不缓存、每次动态创建。
// ============================================================
namespace cowsins
{
    /// <summary>
    /// 玩家状态工厂：创建并缓存玩家状态实例，为状态机提供统一的获取入口。
    /// </summary>
    public class PlayerStateFactory
    {
        // 状态机上下文，工厂创建各状态时将其注入到状态实例中
        private readonly PlayerStates _context;

        // Cache Player States
        // DashState is not cached because it depends on runtime input (direction vector)
        // 缓存的默认状态实例
        private readonly PlayerDefaultState _defaultState;
        // 缓存的跳跃状态实例
        private readonly PlayerJumpState _jumpState;
        // 缓存的下蹲状态实例
        private readonly PlayerCrouchState _crouchState;
        // 缓存的死亡状态实例
        private readonly PlayerDeadState _deadState;
        // 缓存的攀爬状态实例
        private readonly PlayerClimbState _climbState;


        // When PlayerStateFactory is created, retrieve all PlayerStates and store them.
        /// <summary>
        /// 构造函数：保存状态机上下文，并一次性创建并缓存所有不依赖运行时输入的状态实例。
        /// </summary>
        /// <param name="currentContext">玩家状态机上下文，供各状态共享引用。</param>
        public PlayerStateFactory(PlayerStates currentContext)
        {
            _context = currentContext;

            _defaultState = new PlayerDefaultState(_context, this);
            _jumpState = new PlayerJumpState(_context, this);
            _crouchState = new PlayerCrouchState(_context, this);
            _deadState = new PlayerDeadState(_context, this);
            _climbState = new PlayerClimbState(_context, this);
        }

        // Access Player States
        /// <summary>获取缓存的默认状态实例。</summary>
        public PlayerBaseState Default() => _defaultState;
        /// <summary>获取缓存的跳跃状态实例。</summary>
        public PlayerBaseState Jump() => _jumpState;
        /// <summary>获取缓存的下蹲状态实例。</summary>
        public PlayerBaseState Crouch() => _crouchState;
        /// <summary>获取缓存的死亡状态实例。</summary>
        public PlayerBaseState Die() => _deadState;
        /// <summary>获取缓存的攀爬状态实例。</summary>
        public PlayerBaseState Climb() => _climbState;

        // Dash state is created dynamically since it depends on current input direction
        /// <summary>动态创建冲刺状态实例（冲刺依赖当前输入方向，故不缓存），并使用当前输入向量构造。</summary>
        public PlayerBaseState Dash() => new PlayerDashState(_context, this, new UnityEngine.Vector2(_context.InputManager.X, _context.InputManager.Y));
    }
}
