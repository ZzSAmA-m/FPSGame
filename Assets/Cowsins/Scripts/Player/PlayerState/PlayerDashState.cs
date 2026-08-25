// ============================================================
// 文件：PlayerDashState.cs
// 作用：玩家冲刺状态。进入时按输入方向启动冲刺行为并调整镜头视野(FOV)，
//       逐帧驱动冲刺逻辑并倒计时，计时结束或冲刺被中断时切回默认状态。
// ============================================================
using UnityEngine;
using System;

namespace cowsins
{
    /// <summary>
    /// 玩家冲刺状态：负责启动并驱动冲刺行为、调整镜头视野，并按计时或中断条件结束冲刺。
    /// </summary>
    public class PlayerDashState : PlayerBaseState
    {
        // 玩家刚体引用，用于物理相关操作
        private Rigidbody rb;

        // 冲刺剩余时间计时器，从冲刺时长开始递减，归零即结束冲刺
        private float dashTimer;
        // 冲刺方向输入向量，决定冲刺的移动方向
        private Vector2 input;

        /// <summary>
        /// 构造函数：通过基类注入上下文与工厂，并缓存刚体引用与冲刺方向输入。
        /// </summary>
        /// <param name="currentContext">玩家状态机上下文，提供共享引用。</param>
        /// <param name="playerStateFactory">状态工厂，用于创建或获取其他状态。</param>
        /// <param name="inp">冲刺方向输入向量。</param>
        public PlayerDashState(PlayerStates currentContext, PlayerStateFactory playerStateFactory, Vector2 inp)
            : base(currentContext, playerStateFactory)
        {
            rb = _ctx.Rigidbody;
            input = inp;
        }

        /// <summary>
        /// 进入冲刺状态：初始化冲刺计时器，按输入方向启动冲刺行为，缩小镜头视野并触发开始冲刺事件。
        /// </summary>
        public sealed override void EnterState()
        {
            dashTimer = playerMovement.playerSettings.dashDuration;

            playerMovement.dashBehaviour?.Enter(input);
            playerMovement.playerSettings.cameraFOVManager.ForceAddFOV(-playerMovement.playerSettings.fovToAddOnDash);
            playerMovement.playerSettings.events.OnStartDash.Invoke();
        }

        /// <summary>
        /// 每帧更新：触发冲刺进行事件、驱动冲刺行为，并执行状态切换检测。
        /// </summary>
        public sealed override void UpdateState()
        {
            playerMovement.playerSettings.events.OnDashing?.Invoke();
            playerMovement.dashBehaviour?.Tick();
            CheckSwitchState();
        }
        /// <summary>冲刺状态下无独立物理逻辑，此方法留空。</summary>
        public sealed override void FixedUpdateState() {}

        /// <summary>退出冲刺状态：结束冲刺行为。</summary>
        public sealed override void ExitState()
        {
            playerMovement.dashBehaviour?.Exit();
        }

        /// <summary>
        /// 切换检测：递减冲刺计时器，计时结束或冲刺状态被外部中断时切回默认状态。
        /// </summary>
        public sealed override void CheckSwitchState()
        {
            // 每帧递减冲刺剩余时间
            dashTimer -= Time.deltaTime;

            if (dashTimer <= 0 || !playerMovement.IsDashing) SwitchState(_factory.Default());
        }
    }
}