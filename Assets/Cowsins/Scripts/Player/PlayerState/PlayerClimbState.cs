using UnityEngine;

// ============================================================
// 文件：PlayerClimbState.cs
// 作用：玩家攀爬（爬梯子）状态。进入后接管梯子攀爬行为，逐帧更新攀爬与镜头垂直视角，
//       并在切换检测中根据跳跃、接地后移、到达梯子顶端/底端等条件退出到默认状态。
// ============================================================
namespace cowsins
{
    /// <summary>
    /// 玩家攀爬状态：负责驱动梯子攀爬行为，处理攀爬期间的镜头垂直视角与状态切换检测。
    /// </summary>
    public class PlayerClimbState : PlayerBaseState
    {
        // 玩家刚体引用，用于物理相关操作
        private Rigidbody rb;
        // 玩家控制提供者接口，用于判断玩家当前是否可被操控
        private IPlayerControlProvider playerControlProvider;
        // 玩家属性提供者接口，用于订阅/退订死亡事件等属性相关逻辑
        private IPlayerStatsProvider statsProvider;

        /// <summary>
        /// 构造函数：通过基类注入上下文与工厂，并缓存刚体、控制提供者、属性提供者引用。
        /// </summary>
        /// <param name="currentContext">玩家状态机上下文，提供共享引用。</param>
        /// <param name="playerStateFactory">状态工厂，用于创建或获取其他状态。</param>
        public PlayerClimbState(PlayerStates currentContext, PlayerStateFactory playerStateFactory)
            : base(currentContext, playerStateFactory)
        {
            rb = _ctx.Rigidbody;
            playerControlProvider = _ctx.PlayerControlProvider;
            statsProvider = _ctx.PlayerStatsProvider;
        }

        /// <summary>
        /// 进入攀爬状态：启动梯子攀爬行为、触发开始攀爬事件、停止速度线特效，并订阅死亡事件。
        /// </summary>
        public sealed override void EnterState()
        {
            playerMovement.climbLadderBehaviour?.Enter();
            playerMovement.playerSettings.events.OnStartClimb.Invoke();
            playerMovement.speedLinesBehaviour.StopSpeedlines();
            statsProvider.AddOnDieListener(SwitchToDie);
        }

        /// <summary>
        /// 每帧更新：若玩家可被操控，则驱动梯子攀爬行为、更新镜头垂直视角，并执行状态切换检测。
        /// </summary>
        public sealed override void UpdateState()
        {
            if (!playerControlProvider.IsControllable) return;

            playerMovement.climbLadderBehaviour?.Tick();
            playerMovement.cameraLookBehaviour?.VerticalLook();
            CheckSwitchState();
        }

        /// <summary>攀爬状态下无独立物理逻辑，此方法留空。</summary>
        public sealed override void FixedUpdateState() { }

        /// <summary>
        /// 退出攀爬状态：触发结束攀爬事件、停止梯子攀爬行为，并退订死亡事件。
        /// </summary>
        public sealed override void ExitState()
        {
            playerMovement.playerSettings.events.OnEndClimb.Invoke();
            playerMovement.climbLadderBehaviour?.Exit();
            statsProvider.RemoveOnDieListener(SwitchToDie);
        }

        /// <summary>
        /// 切换检测：根据跳跃、接地后移、到达梯子顶端/底端等条件，判断是否退出攀爬状态回到默认状态。
        /// </summary>
        public sealed override void CheckSwitchState()
        {
            bool isGrounded = playerMovement.Grounded;

            // Jump always exits
            // 按下跳跃键时立即退出攀爬状态，回到默认状态
            if (inputManager.Jumping)
            {
                SwitchState(_factory.Default());
                return;
            }

            // Grounded + backward movement exits based on cooldown to prevent states flickering issues
            // 已接地且持续向后（下）移动时退出，配合冷却时间避免状态频繁闪烁
            if (isGrounded && inputManager.Y < -0.01f)
            {
                SwitchState(_factory.Default());
                return;
            }

            // Reached top of ladder
            // 到达梯子顶端且继续向上输入时：施加登顶作用力并退出攀爬状态
            if (playerMovement.climbLadderBehaviour.DetectTopLadder() && inputManager.Y > 0.01f)
            {
                playerMovement.climbLadderBehaviour?.ApplyForcesOnTopReached();
                SwitchState(_factory.Default());
                return;
            }

            // Reached bottom of ladder
            // 到达梯子底端且向下输入时退出攀爬状态
            if (playerMovement.climbLadderBehaviour.DetectBottomLadder() && inputManager.Y < -0.01f)
            {
                SwitchState(_factory.Default());
            }
        }
    }
}