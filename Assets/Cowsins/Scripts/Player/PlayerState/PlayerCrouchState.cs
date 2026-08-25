// ============================================================
// 文件：PlayerCrouchState.cs
// 作用：玩家下蹲/滑铲状态。进入后驱动下蹲滑铲行为，逐帧与按物理步长更新移动、脚步声、
//       速度线等逻辑，并在切换检测中处理蹲跳、冲刺、空中退出、站起等状态转换。
// ============================================================
using UnityEngine;
namespace cowsins
{
    /// <summary>
    /// 玩家下蹲状态：负责下蹲/滑铲行为的驱动、下蹲期间的移动与特效更新，以及相关状态切换检测。
    /// </summary>
    public class PlayerCrouchState : PlayerBaseState
    {
        // 玩家控制提供者接口，用于判断玩家当前是否可被操控
        private IPlayerControlProvider playerControlProvider; // IPlayerControlProvider is implemented in PlayerControl.cs
        // 玩家属性提供者接口，用于订阅/退订死亡事件等属性相关逻辑
        private IPlayerStatsProvider statsProvider; // IPlayerStatsProvider is implemented in PlayerStats.cs

        // 标记是否在空中进入下蹲状态（用于空中下落时保持下蹲，直到落地后再恢复默认）
        private bool enteredWhileAirborne;

        /// <summary>
        /// 构造函数：通过基类注入上下文与工厂，并缓存属性提供者、控制提供者引用。
        /// </summary>
        /// <param name="currentContext">玩家状态机上下文，提供共享引用。</param>
        /// <param name="playerStateFactory">状态工厂，用于创建或获取其他状态。</param>
        public PlayerCrouchState(PlayerStates currentContext, PlayerStateFactory playerStateFactory)
            : base(currentContext, playerStateFactory)
        {
            statsProvider = _ctx.PlayerStatsProvider;
            playerControlProvider = _ctx.PlayerControlProvider;
        }

        /// <summary>
        /// 进入下蹲状态：记录是否在空中进入下蹲、启动下蹲滑铲行为，并订阅死亡事件。
        /// </summary>
        public sealed override void EnterState()
        {
            enteredWhileAirborne = !playerMovement.Grounded;
            playerMovement.crouchSlideBehaviour?.Enter();
            statsProvider.AddOnDieListener(SwitchToDie);
        }

        /// <summary>
        /// 每帧更新：若玩家可被操控，则处理下蹲逻辑、更新镜头视角，并执行状态切换检测。
        /// </summary>
        public sealed override void UpdateState()
        {
            if (!playerControlProvider.IsControllable) return;

            playerMovement.crouchSlideBehaviour?.HandleCrouch();
            playerMovement.cameraLookBehaviour?.Tick();

            CheckSwitchState();
        }
        /// <summary>
        /// 按固定物理步长更新：驱动基础移动、下蹲滑铲物理、脚步声与速度线特效。
        /// </summary>
        public sealed override void FixedUpdateState()
        {
            if (!playerControlProvider.IsControllable) return;

            playerMovement.basicMovementBehaviour?.Movement();
            playerMovement.crouchSlideBehaviour?.FixedTick();
            playerMovement.footstepsBehaviour?.FootSteps();
            playerMovement.speedLinesBehaviour?.Tick();
        }

        /// <summary>退出下蹲状态：结束下蹲滑铲行为，并退订死亡事件。</summary>
        public sealed override void ExitState() 
        { 
            playerMovement.crouchSlideBehaviour?.Exit(); // Invoke your own method on the moment you are standing up NOT WHILE YOU ARE NOT CROUCHING
            statsProvider.RemoveOnDieListener(SwitchToDie);
        }

        /// <summary>
        /// 切换检测：处理蹲跳、蹲中冲刺、空中退出以及站起等状态转换条件。
        /// </summary>
        public sealed override void CheckSwitchState()
        {
            // 满足跳跃条件、按下跳跃键且设置允许蹲跳时，切换到跳跃状态
            if (playerMovement.jumpBehaviour.CanExecute() && inputManager.Jumping && playerMovement.playerSettings.canJumpWhileCrouching)
                SwitchState(_factory.Jump());

            // 按下冲刺键且冲刺可执行时，切换到冲刺状态
            if (inputManager.Dashing && playerMovement.dashBehaviour.CanExecute()) SwitchState(_factory.Dash());

            // Clear the airborne-entry flag once we touch the ground
            // 一旦落地，清除“空中进入下蹲”的标记
            if (enteredWhileAirborne && playerMovement.Grounded)
                enteredWhileAirborne = false;


            // 非空中进入下蹲、且已离地且未滑铲时，回到默认状态
            if (!enteredWhileAirborne && !playerMovement.Grounded && !playerMovement.IsSliding)
            {
                SwitchState(_factory.Default());
                return;
            }

            // 检测到可站起（上方无遮挡）时，回到默认状态
            if(playerMovement.crouchSlideBehaviour.CheckUnCrouch()) SwitchState(_factory.Default());
        }
    }
}