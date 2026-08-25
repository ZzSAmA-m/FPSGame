// ============================================================
// 文件：PlayerDefaultState.cs
// 作用：玩家默认状态（地面行走/站立等常态）。注册跳跃、冲刺、钩索等输入事件与死亡监听，
//       驱动镜头、脚步声、基础移动、墙面奔跑等逻辑，并检测攀爬与下蹲切换条件。
// ============================================================
using UnityEngine;

namespace cowsins
{
    /// <summary>
    /// 玩家默认状态：玩家处于地面常态时的行为集合，负责常规移动、镜头、钩索、墙面奔跑，
    /// 以及向攀爬/下蹲等状态的切换检测。
    /// </summary>
    public class PlayerDefaultState : PlayerBaseState
    {
        // 玩家属性提供者接口，用于订阅/退订死亡事件等属性相关逻辑
        private IPlayerStatsProvider statsProvider;
        // 玩家控制提供者接口，用于判断玩家当前是否可被操控
        private IPlayerControlProvider playerControlProvider;
        // 记录最近一次离开梯子（攀爬）的时间，用于攀爬切换的冷却判断
        private float ladderExitTime = -1f;
        // 离开梯子后再次进入攀爬状态所需的冷却时间（秒）
        private const float ladderExitCooldown = 0.3f;

        /// <summary>
        /// 构造函数：通过基类注入上下文与工厂，并缓存属性提供者、控制提供者引用。
        /// </summary>
        /// <param name="currentContext">玩家状态机上下文，提供共享引用。</param>
        /// <param name="playerStateFactory">状态工厂，用于创建或获取其他状态。</param>
        public PlayerDefaultState(PlayerStates currentContext, PlayerStateFactory playerStateFactory)
            : base(currentContext, playerStateFactory)
        {
            statsProvider = _ctx.PlayerStatsProvider;
            playerControlProvider = _ctx.PlayerControlProvider;
        }

        /// <summary>
        /// 进入默认状态：注册跳跃、冲刺、钩索等输入事件与死亡事件监听，并在刚离开攀爬状态时记录离开时间。
        /// </summary>
        public sealed override void EnterState()
        {
            inputManager.OnJump += SwitchToJumpState;
            inputManager.OnDash += SwitchToDashState;
            inputManager.OnStartGrapple += StartGrapple;
            inputManager.OnStopGrapple += StopGrapple;
            statsProvider.AddOnDieListener(SwitchToDie);

            // 若刚离开攀爬状态（IsClimbing 仍为真），记录离开时间用于后续冷却判断
            if (playerMovement.IsClimbing)
            {
                ladderExitTime = Time.time;
            }
        }

        /// <summary>
        /// 每帧更新：若可操控，则更新镜头视角、脚步声、钩索与站起检测，并执行状态切换检测。
        /// </summary>
        public sealed override void UpdateState()
        {
            if (!playerControlProvider.IsControllable) return;

            playerMovement.cameraLookBehaviour.Tick();
            playerMovement.footstepsBehaviour?.FootSteps();
            if (playerMovement.playerSettings.allowGrapple)
                playerMovement.grapplingHookBehaviour?.Tick();
            playerMovement.crouchSlideBehaviour?.CheckUnCrouch();

            CheckSwitchState();
        }

        /// <summary>
        /// 按固定物理步长更新：驱动基础移动与墙面奔跑物理。
        /// </summary>
        public sealed override void FixedUpdateState()
        {
            playerMovement.basicMovementBehaviour?.Movement();
            playerMovement.wallRunBehaviour?.FixedTick();
        }

        /// <summary>退出默认状态：注销在进入时注册的所有输入事件与死亡事件监听。</summary>
        public sealed override void ExitState()
        {
            inputManager.OnJump -= SwitchToJumpState;
            inputManager.OnDash -= SwitchToDashState;
            inputManager.OnStartGrapple -= StartGrapple;
            inputManager.OnStopGrapple -= StopGrapple;
            statsProvider.RemoveOnDieListener(SwitchToDie);
        }

        /// <summary>
        /// 切换检测：在离开梯子的冷却时间过后检测攀爬条件，并检测下蹲条件。
        /// </summary>
        public sealed override void CheckSwitchState()
        {
            // 判断距离上次离开梯子是否已超过冷却时间，避免状态频繁闪烁
            bool canCheckLadder = Time.time - ladderExitTime > ladderExitCooldown;

            // Check climbing only if cooldown has passed
            if (canCheckLadder && playerMovement.climbLadderBehaviour.CanExecute())
                SwitchState(_factory.Climb());

            // Check Crouch
            if (playerMovement.crouchSlideBehaviour.CanExecute())
                SwitchState(_factory.Crouch());
        }

        /// <summary>输入回调：跳跃键触发时，若跳跃可执行则切换到跳跃状态。</summary>
        public void SwitchToJumpState()
        {
            if (playerMovement.jumpBehaviour.CanExecute())
                SwitchState(_factory.Jump());
        }

        /// <summary>输入回调：冲刺键触发时，若冲刺可执行则切换到冲刺状态。</summary>
        public void SwitchToDashState()
        {
            if (playerMovement.dashBehaviour.CanExecute())
                SwitchState(_factory.Dash());
        }
    }
}