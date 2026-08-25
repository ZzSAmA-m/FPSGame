// ============================================================================
// WeaponBaseState.cs —— 武器状态的抽象基类。
// 定义了武器有限状态机（FSM）中所有具体状态共有的生命周期接口（进入、更新、
// 物理更新、退出、检查切换），并保存对共享上下文（WeaponStates）和状态工厂
// （WeaponStateFactory）的引用，同时提供统一的状态切换方法 SwitchState。
// ============================================================================
namespace cowsins
{
    /// <summary>
    /// 所有武器状态的抽象基类：统一管理武器状态的进入/更新/退出生命周期，
    /// 并通过 SwitchState 完成「退出当前状态 → 进入新状态」的切换流程。
    /// </summary>
    public abstract class WeaponBaseState
    {
        protected WeaponStates _ctx; // 共享上下文：保存所有状态共用的组件引用
        protected WeaponStateFactory _factory; // 状态工厂：用于获取其它具体状态实例

        // 构造函数：注入共享上下文与状态工厂，供派生状态使用。
        public WeaponBaseState(WeaponStates currentContext, WeaponStateFactory playerStateFactory)
        {
            _ctx = currentContext;
            _factory = playerStateFactory;
        }

        // 进入状态时调用：执行该状态开始时的一次性初始化逻辑。
        public abstract void EnterState();

        // 每帧更新时调用：执行该状态的常规逐帧逻辑。
        public abstract void UpdateState();

        // 固定时间步（物理帧）更新时调用：执行与物理相关的逐帧逻辑。
        public abstract void FixedUpdateState();

        // 退出状态时调用：执行该状态结束时的清理逻辑。
        public abstract void ExitState();

        // 检查是否需要切换到其它状态：由各状态自行实现切换条件。
        public abstract void CheckSwitchState();

        void UpdateStates() { }

        // 切换到指定状态：先退出当前状态，再进入新状态，最后更新上下文中的当前状态引用。
        protected void SwitchState(WeaponBaseState newState)
        {
            ExitState(); // 清理当前状态

            newState.EnterState(); // 初始化新状态

            _ctx.CurrentState = newState; // 将上下文中的当前状态指向新状态
        }
    }
}