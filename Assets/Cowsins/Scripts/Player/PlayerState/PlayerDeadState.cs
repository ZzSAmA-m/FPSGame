// ============================================================
// 文件：PlayerDeadState.cs
// 作用：玩家死亡状态（终态）。进入后使玩家失去控制，可选地将刚体冻结以停止物理模拟；
//       除退出时恢复刚体外，其余更新与切换检测均为空实现。
// ============================================================
using UnityEngine;
namespace cowsins
{
    /// <summary>
    /// 玩家死亡状态：使玩家失去控制，并可在死亡时冻结刚体；作为终态，不处理更新与切换检测。
    /// </summary>
    public class PlayerDeadState : PlayerBaseState
    {
        // 玩家刚体引用，用于在死亡时冻结/恢复物理模拟
        private Rigidbody rb;
        // 玩家控制提供者接口，用于在死亡时使玩家失去控制
        private IPlayerControlProvider playerControlProvider; // IPlayerControlProvider is implemented in PlayerControl.cs
        // 玩家属性提供者接口，用于读取死亡时是否冻结玩家的设置
        private IPlayerStatsProvider statsProvider; // IPlayerStatsProvider is implemented in PlayerStats.cs
        /// <summary>
        /// 构造函数：通过基类注入上下文与工厂，并缓存刚体、控制提供者、属性提供者引用。
        /// </summary>
        /// <param name="currentContext">玩家状态机上下文，提供共享引用。</param>
        /// <param name="playerStateFactory">状态工厂，用于创建或获取其他状态。</param>
        public PlayerDeadState(PlayerStates currentContext, PlayerStateFactory playerStateFactory)
            : base(currentContext, playerStateFactory) 
        {
            rb = _ctx.Rigidbody;
            playerControlProvider = _ctx.PlayerControlProvider;
            statsProvider = _ctx.PlayerStatsProvider;
        }

        /// <summary>
        /// 进入死亡状态：使玩家失去控制；若设置要求死亡时冻结玩家，则将刚体设为运动学状态以停止物理模拟。
        /// </summary>
        public sealed override void EnterState()
        {
            playerControlProvider.LoseControl();
            
            if (statsProvider.FreezePlayerOnDeath) rb.isKinematic = true;
        }

        /// <summary>死亡状态下玩家无需逐帧更新逻辑，此方法留空。</summary>
        public sealed override void UpdateState() {}

        /// <summary>死亡状态下玩家无需物理更新逻辑，此方法留空。</summary>
        public sealed override void FixedUpdateState() { }

        /// <summary>退出死亡状态：恢复刚体为非运动学状态，使玩家重新受物理影响。</summary>
        public sealed override void ExitState() { rb.isKinematic = false; }

        /// <summary>死亡状态是终态，不做切换检测，此方法留空。</summary>
        public sealed override void CheckSwitchState() { }


    }
}