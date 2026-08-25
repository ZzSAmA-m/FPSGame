// ============================================================
// 文件：PlayerJumpState.cs
// 作用：玩家跳跃/空中状态。进入后执行跳跃行为并注册钩索与死亡事件，
//       空中期间驱动镜头、墙面反弹、移动与墙面奔跑，并检测落地、二段跳、冲刺、下蹲等切换条件。
// ============================================================
using UnityEditor;
using UnityEngine;
namespace cowsins
{
    /// <summary>
    /// 玩家跳跃状态：负责空中移动与跳跃相关行为，并处理落地、二段跳、墙面反弹、冲刺、下蹲等切换检测。
    /// </summary>
    public class PlayerJumpState : PlayerBaseState
    {
        // 玩家属性提供者接口，用于订阅/退订死亡事件等属性相关逻辑
        private IPlayerStatsProvider statsProvider; // IPlayerStatsProvider is implemented in PlayerStats.cs
        // 玩家控制提供者接口，用于判断玩家当前是否可被操控
        private IPlayerControlProvider playerControl; // IPlayerControlProvider is implemented in PlayerControl.cs

        /// <summary>
        /// 构造函数：通过基类注入上下文与工厂，并缓存属性提供者、控制提供者引用。
        /// </summary>
        /// <param name="currentContext">玩家状态机上下文，提供共享引用。</param>
        /// <param name="playerStateFactory">状态工厂，用于创建或获取其他状态。</param>
        public PlayerJumpState(PlayerStates currentContext, PlayerStateFactory playerStateFactory)
            : base(currentContext, playerStateFactory)
        {
            statsProvider = _ctx.PlayerStatsProvider;
            playerControl = _ctx.PlayerControlProvider;
        }

        /// <summary>
        /// 进入跳跃状态：执行跳跃行为，注册钩索输入事件并订阅死亡事件。
        /// </summary>
        public sealed override void EnterState()
        {
            playerMovement.jumpBehaviour.Enter();

            inputManager.OnStartGrapple += StartGrapple;
            inputManager.OnStopGrapple += StopGrapple;
            statsProvider.AddOnDieListener(SwitchToDie);
        }

        /// <summary>
        /// 每帧更新：先执行切换检测；若可操控，则更新镜头视角、墙面反弹与站起检测。
        /// </summary>
        public sealed override void UpdateState()
        {
            CheckSwitchState();
            if (!playerControl.IsControllable) return;
            playerMovement.cameraLookBehaviour?.Tick();
            playerMovement.wallBounceBehaviour?.Tick();
            playerMovement.crouchSlideBehaviour?.CheckUnCrouch();
        }

        /// <summary>
        /// 按固定物理步长更新：若可操控，则驱动基础移动与墙面奔跑物理。
        /// </summary>
        public sealed override void FixedUpdateState()
        {
            if (!playerControl.IsControllable) return;
            playerMovement.basicMovementBehaviour?.Movement();
            playerMovement.wallRunBehaviour?.FixedTick();
        }

        /// <summary>退出跳跃状态：注销钩索输入事件并退订死亡事件。</summary>
        public sealed override void ExitState() 
        {
            inputManager.OnStartGrapple -= StartGrapple;
            inputManager.OnStopGrapple -= StopGrapple;
            statsProvider.RemoveOnDieListener(SwitchToDie);
        }

        /// <summary>
        /// 切换检测：依次检测墙面反弹、攀爬、二段跳、落地、冲刺、下蹲等条件，并决定是否切换状态。
        /// </summary>
        public sealed override void CheckSwitchState()
        {
            // 存在墙面反弹行为时，先驱动反弹检测；若可执行则触发反弹并广播墙面反弹事件
            if (playerMovement.wallBounceBehaviour != null)
            {
                playerMovement.wallBounceBehaviour?.Tick();

                if (playerMovement.wallBounceBehaviour.CanExecute())
                {
                    playerMovement.wallBounceBehaviour?.Enter();
                    playerMovement.playerSettings.events.OnWallBounce.Invoke();
                }
            }

            // 攀爬可执行时切换到攀爬状态
            if (playerMovement.climbLadderBehaviour.CanExecute())
            {
                SwitchState(_factory.Climb());
                return;
            }

            // 按下跳跃键且可执行二段跳时，切换到跳跃状态
            if (inputManager.Jumping && playerMovement.jumpBehaviour.CanExecuteDoubleJump())
            {
                SwitchState(_factory.Jump());
                return;
            }

            // 已接地且未处于跳跃标记（或正在墙面奔跑）时，回到默认状态
            if (playerMovement.Grounded && !playerMovement.movementContext.HasJumped || playerMovement.IsWallRunning)
            {
                SwitchState(_factory.Default());
                return;
            }

            // 按下冲刺键且冲刺可执行时，切换到冲刺状态
            if (inputManager.Dashing && playerMovement.dashBehaviour.CanExecute())
            {
                SwitchState(_factory.Dash());
                return;
            }

            // 设置允许空中下蹲且下蹲可执行时，切换到下蹲状态
            if (playerMovement.playerSettings.allowCrouchWhileJumping && playerMovement.crouchSlideBehaviour.CanExecute())
            {
                SwitchState(_factory.Crouch());
                return;
            }

            // 允许钩索时驱动钩索行为
            if (playerMovement.playerSettings.allowGrapple)
            {
                playerMovement.grapplingHookBehaviour?.Tick();
            }
        }
    }
}