// ============================================================
// 文件：PlayerBaseState.cs
// 作用：玩家状态机的抽象基类。定义所有玩家状态共有的上下文引用与生命周期方法
//       （进入 EnterState / 更新 UpdateState / 物理更新 FixedUpdateState /
//         退出 ExitState / 切换检测 CheckSwitchState），并封装状态切换、钩索等通用辅助逻辑，
//       各具体状态类继承本类后实现各自行为。
// ============================================================
using System.Collections.Generic;

namespace cowsins
{
    /// <summary>
    /// 玩家状态的抽象基类：持有状态上下文与状态工厂的引用，统一封装状态切换逻辑，
    /// 并声明进入/更新/物理更新/退出/切换检测等生命周期方法供子类实现。
    /// </summary>
    public abstract class PlayerBaseState
    {
        // 当前状态机上下文，集中保存玩家状态间共享的引用与依赖
        protected PlayerStates _ctx;
        // 状态工厂，用于按需创建或获取其他玩家状态实例
        protected PlayerStateFactory _factory;

        // 输入管理器（通过上下文便捷访问），用于读取玩家按键输入
        protected InputManager inputManager => _ctx.InputManager;
        // 玩家移动组件（通过上下文便捷访问），封装移动/攀爬/滑铲等行为
        protected PlayerMovement playerMovement => _ctx.PlayerMovement;

        /// <summary>
        /// 构造函数：注入状态上下文与状态工厂，供子类在状态生命周期内访问共享依赖。
        /// </summary>
        /// <param name="currentContext">玩家状态机上下文，提供各状态共享的引用。</param>
        /// <param name="playerStateFactory">状态工厂，用于创建或获取其他状态。</param>
        public PlayerBaseState(PlayerStates currentContext, PlayerStateFactory playerStateFactory)
        {
            _ctx = currentContext;
            _factory = playerStateFactory;
        }

        /// <summary>进入该状态时调用一次，用于执行状态的初始化逻辑。</summary>
        public abstract void EnterState();

        /// <summary>状态处于激活期间每帧调用，用于处理该状态的逐帧逻辑。</summary>
        public abstract void UpdateState();

        /// <summary>状态处于激活期间按固定物理步长调用，用于处理刚体运动等物理逻辑。</summary>
        public abstract void FixedUpdateState();

        /// <summary>离开该状态时调用一次，用于执行清理逻辑（如注销事件监听）。</summary>
        public abstract void ExitState();

        /// <summary>每帧检查是否需要切换到其他状态，是状态机跳转的核心判断逻辑。</summary>
        public abstract void CheckSwitchState();

        // 切换到指定状态：先退出当前状态，再进入新状态，最后更新上下文中的当前状态引用
        protected void SwitchState(PlayerBaseState newState)
        {
            ExitState();

            newState.EnterState();

            _ctx.CurrentState = newState;
        }

        /// <summary>启动钩索：若设置允许使用钩索，则触发钩索行为的进入逻辑。</summary>
        public void StartGrapple()
        {
            if (!playerMovement.playerSettings.allowGrapple) return;

            playerMovement.grapplingHookBehaviour?.Enter();
        }
        /// <summary>停止钩索：若设置允许使用钩索，则触发钩索行为的退出逻辑。</summary>
        public void StopGrapple()
        {
            if (!playerMovement.playerSettings.allowGrapple) return;
            playerMovement.grapplingHookBehaviour?.Exit();
        }

        /// <summary>便捷方法：直接切换到死亡状态。</summary>
        public void SwitchToDie() => SwitchState(_factory.Die());
    }
}