// ============================================================================
// WeaponStateFactory.cs —— 武器状态工厂。
// 采用「工厂 + 单例缓存」模式：在构造时一次性创建所有武器状态实例并缓存，
// 之后通过各访问方法返回对应状态，避免每次切换状态都重新 new 一个对象。
// ============================================================================
namespace cowsins
{
    /// <summary>
    /// 武器状态工厂：负责创建并缓存所有武器状态实例，并提供统一的访问入口。
    /// </summary>
    public class WeaponStateFactory
    {
        private readonly WeaponStates _context; // 共享上下文，注入到每个状态中

        // Cache Weapon States
        // 缓存各武器状态实例（只创建一次，反复复用）
        private readonly WeaponDefaultState _defaultState;
        private readonly WeaponReloadState _reloadState;
        private readonly WeaponShootingState _shootingState;
        private readonly MeleeState _meleeState;
        private readonly WeaponInspectState _inspectState;
        private readonly WeaponUnholsterState _unholsterState;
        private readonly WeaponHiddenState _hiddenState;

        // When WeaponStateFactory is created, retrieve all WeaponStates and store them.
        // 构造工厂：一次性创建并存储所有武器状态。
        public WeaponStateFactory(WeaponStates currentContext)
        {
            _context = currentContext;

            _defaultState = new WeaponDefaultState(_context, this);
            _reloadState = new WeaponReloadState(_context, this);
            _shootingState = new WeaponShootingState(_context, this);
            _meleeState = new MeleeState(_context, this);
            _inspectState = new WeaponInspectState(_context, this);
            _unholsterState = new WeaponUnholsterState(_context, this);
            _hiddenState = new WeaponHiddenState(_context, this);
        }

        // Access Weapon States
        // 以下访问方法返回各自缓存的武器状态实例。
        public WeaponBaseState Default() => _defaultState; // 默认（待机）状态
        public WeaponBaseState Reload() => _reloadState; // 换弹状态
        public WeaponBaseState Shoot() => _shootingState; // 射击状态
        public WeaponBaseState Melee() => _meleeState; // 近战状态
        public WeaponBaseState Inspect() => _inspectState; // 检视状态
        public WeaponBaseState Unholster() => _unholsterState; // 掏出武器状态
        public WeaponBaseState Hidden() => _hiddenState; // 隐藏状态
    }
}
