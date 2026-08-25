// ============================================================
// GroundDetectionBehaviour（地面检测行为）
// 负责检测玩家是否接地以及是否处于斜坡：通过胶囊体投射/射线检测地面，
// 使用带缓冲的接地状态机（协程延迟离开地面），并管理落地/离地事件。
// 同时更新坡度标记与跳跃后的短暂忽略接地窗口。
// ============================================================
using UnityEngine;
using System.Collections;

namespace cowsins
{
    /// <summary>检测玩家接地与斜坡状态：物理检测、带缓冲的接地状态机、落地/离地事件。</summary>
    public class GroundDetectionBehaviour
    {
        // 移动上下文：聚合刚体、输入、依赖、设置与地面掩码等共享数据。
        private MovementContext context;
        // 玩家刚体，用于读取速度以判断滑铲/上升状态。
        private Rigidbody rb;
        // 输入管理器：提供输入以辅助判断滑铲等状态。
        private InputManager inputManager;
        // 玩家移动状态提供者：读写接地/滑铲标记并暴露朝向。
        private IPlayerMovementStateProvider playerMovement;
        // 移动事件提供者：订阅跳跃事件并派发落地事件。
        private IPlayerMovementEventsProvider playerEvents;

        // 玩家朝向（此处备用，通过可空访问避免未初始化）。
        private PlayerOrientation orientation => playerMovement?.Orientation;

        // 玩家胶囊碰撞体，用于计算地面检测的胶囊端点与半径。
        private CapsuleCollider playerCapsuleCollider;

        // 移动设置：地面检测距离、音效等参数来源。
        private PlayerMovementSettings playerSettings;

        // 玩家缩放，用于按身高动态调整地面检测距离。
        private Vector3 playerScale;

        // 延迟离开地面的协程引用，避免重复启动。
        private Coroutine ungroundCoroutine;
        // 用于启动协程的 MonoBehaviour。
        private MonoBehaviour coroutineRunner;

        // Jump ignore timer when the player has just jumped, ignore ground hits
        // 跳跃后忽略地面命中的持续时间。
        private float jumpIgnoreDuration = 0.2f;
        // 跳跃忽略接地的剩余计时。
        private float jumpTimer = 0f;
        // 上一帧是否处于跳跃状态，用于检测跳跃开始的瞬间。
        private bool prevHasJumped = false;

        // Slopes
        // 可视为“可站立地面”的最大坡度角，超过则视为墙壁。
        private float maxSlopeAngle = 60f;

        // 是否正在取消接地（延迟离开地面过程中）。
        private bool cancellingGrounded;

        /// <summary>构造函数：缓存依赖引用、记录缩放，并订阅跳跃事件以取消延迟离地。</summary>
        /// <param name="context">聚合刚体、输入、依赖与设置的移动上下文。</param>
        public GroundDetectionBehaviour(MovementContext context)
        {
            this.context = context;
            this.rb = context.Rigidbody;
            this.inputManager = context.InputManager;
            this.playerMovement = context.Dependencies.PlayerMovementState;
            this.playerEvents = context.Dependencies.PlayerMovementEvents;

            this.playerSettings = context.Settings;
            this.playerCapsuleCollider = context.Capsule;

            this.coroutineRunner = context.Transform.GetComponent<MonoBehaviour>();

            this.playerScale = context.Transform.localScale;

            playerEvents.Events.OnJump.AddListener(NegateCancellingGrounded);
        }

        /// <summary>释放资源：移除跳跃事件监听。</summary>
        public void Dispose()
        {
            playerEvents.Events.OnJump.RemoveListener(NegateCancellingGrounded);
        }

        /// <summary>每帧更新地面检测：更新跳跃忽略计时、坡度标记并驱动接地状态机。</summary>
        public void Tick()
        {
            // set on jump start, reduce each frame
            if (context.HasJumped && !prevHasJumped)
            {
                jumpTimer = jumpIgnoreDuration;
            }

            if (jumpTimer > 0f)
            {
                jumpTimer -= Time.deltaTime;
            }

            prevHasJumped = context.HasJumped;

            // 满足滑铲条件时写入滑铲状态。
            if (IsSliding())
                playerMovement.IsSliding = true;

            // Perform ground check every frame
            // 执行本帧地面检测，得到命中信息。
            bool foundGroundThisFrame = PerformGroundCheck(out RaycastHit hit);

            context.IsPlayerOnSlope = IsPlayerOnSlope(hit, foundGroundThisFrame);

            // Store the latest ground hit so other behaviours can use the slope normal
            context.SlopeHit = hit;

            // Buffered grounding system
            bool wasGrounded = playerMovement.Grounded;
            bool newGroundedState = UpdateGroundedState(foundGroundThisFrame, wasGrounded);

            // 接地状态发生变化时触发落地/离地处理。
            if (newGroundedState != wasGrounded)
            {
                // Just landed
                if (newGroundedState)
                {
                    HandleLanding();
                }
                else // Just left ground
                {
                    HandleLeavingGround();
                }
            }

            playerMovement.Grounded = newGroundedState;
        }

        // 根据本帧检测结果与跳跃状态更新接地状态（含跳跃忽略与延迟离地缓冲）。
        private bool UpdateGroundedState(bool foundGroundThisFrame, bool wasGrounded)
        {
            // If the player has just jumped and is still moving upward, ignore ground
            // but only while the jump ignore timer is still active.
            if (context.HasJumped && rb.linearVelocity.y > 0.1f && jumpTimer > 0f)
            {
                if (wasGrounded && !cancellingGrounded)
                {
                    cancellingGrounded = true;
                    if (ungroundCoroutine == null)
                    {
                        ungroundCoroutine = coroutineRunner.StartCoroutine(StopGroundedCoroutine());
                    }
                }

                return false;
            }

            if (foundGroundThisFrame)
            {
                // Cancel any pending unground coroutine
                if (ungroundCoroutine != null)
                {
                    coroutineRunner.StopCoroutine(ungroundCoroutine);
                    ungroundCoroutine = null;
                    cancellingGrounded = false;
                }

                // Become grounded after consistent detection
                if (!wasGrounded)
                {
                    return true;
                }
                else if (wasGrounded)
                {
                    return true; // Stay grounded
                }
            }
            else
            {
                // Only start leaving ground process if we were previously grounded
                if (wasGrounded && !cancellingGrounded)
                {
                    cancellingGrounded = true;
                    if (ungroundCoroutine == null)
                    {
                        ungroundCoroutine = coroutineRunner.StartCoroutine(StopGroundedCoroutine());
                    }
                }
            }

            // No state change
            return wasGrounded;
        }

        private void HandleLanding()
        {
            context.HasJumped = false;

            cancellingGrounded = false;

            playerEvents.Events.OnLand?.Invoke();
            playerSettings.events.OnLand.Invoke();
            SoundManager.Instance.PlaySound(playerSettings.sounds.landSFX, 0, 0, false);
        }

        // 离地处理：设置土狼时间（Coyote Time）以便离地后仍能短暂跳跃。
        private void HandleLeavingGround()
        {
            context.CoyoteTimer = context.CoyoteJumpTime;
            cancellingGrounded = false;
        }

        // 重置接地状态为已接地并取消未决的离地协程。
        private void ResetGroundingState()
        {
            playerMovement.Grounded = true;
            cancellingGrounded = false;

            if (ungroundCoroutine != null)
            {
                coroutineRunner.StopCoroutine(ungroundCoroutine);
                ungroundCoroutine = null;
            }
        }

        // 协程：延迟 0.1 秒后复核地面，若确实离地则正式置为未接地。
        private IEnumerator StopGroundedCoroutine()
        {
            yield return new WaitForSeconds(0.1f);

            // Check if we are grounded
            bool stillHasGround = PerformGroundCheck(out RaycastHit hit);

            if (!stillHasGround)
            {
                playerMovement.Grounded = false;
                context.CoyoteTimer = context.CoyoteJumpTime;
            }

            cancellingGrounded = false;
            ungroundCoroutine = null;
        }

        /// <summary>
        /// Determines whether the player is on a slope.
        /// </summary>
        // 判断玩家是否处于斜坡（接地且地面法线夹角在最大坡度角以内）。
        // 判断玩家是否处于斜坡（接地且地面法线夹角在最大坡度角以内）。
        private bool IsPlayerOnSlope(RaycastHit hit, bool isGrounded)
        {
            if (isGrounded)
            {
                float angle = Vector3.Angle(Vector3.up, hit.normal);
                return angle < maxSlopeAngle && angle != 0;
            }
            return false;
        }


        // 判断是否处于滑铲（蹲伏且速度超过步行速度）。
        private bool IsSliding()
        {
            return playerMovement.IsCrouching && rb.linearVelocity.magnitude > playerMovement.WalkSpeed;
        }

        // 取消“正在取消接地”标记（跳跃时立即视为未接地）。
        private void NegateCancellingGrounded() => cancellingGrounded = false;

        /// <summary>
        /// Checks if the player is grounded through physics raycast.
        /// </summary>
        // 通过胶囊体投射（并回退到射线）检测脚下地面，返回是否站在可站立地面上。
        private bool PerformGroundCheck(out RaycastHit hit)
        {
            // Get capsule center
            Vector3 center = playerCapsuleCollider.transform.TransformPoint(playerCapsuleCollider.center);

            // Calculate height considering both capsule height and current scale
            float actualScale = context.Transform.localScale.y;
            float baseHeight = playerCapsuleCollider.height;
            float height = baseHeight * actualScale;

            float radius = playerCapsuleCollider.radius * 0.8f;
            float halfHeight = Mathf.Max(0, (height * 0.5f) - radius);

            // Calculate capsule endpoints from center
            Vector3 bottom = center - Vector3.up * halfHeight;
            Vector3 top = center + Vector3.up * halfHeight;

            // Add small offset
            float startOffset = 0.1f;
            bottom += Vector3.up * startOffset;
            top += Vector3.up * startOffset;

            // Scale ground check distance based on player height
            float dynamicGroundCheckDistance = playerSettings.groundCheckDistance * actualScale;

            dynamicGroundCheckDistance = Mathf.Max(dynamicGroundCheckDistance, 0.2f);
            float castDistance = dynamicGroundCheckDistance + startOffset;

            Vector3 rayOrigin = center + Vector3.up * startOffset;
            // 以胶囊体向下投射检测地面。
            bool foundGround = Physics.CapsuleCast(
                top,
                bottom,
                radius,
                Vector3.down,
                out hit,
                castDistance,
                context.WhatIsGround
            );

            if (foundGround && CowsinsUtilities.IsFloor(hit.normal, maxSlopeAngle))
            {

                RaycastHit refineHit;
                if (Physics.Raycast(rayOrigin, Vector3.down, out refineHit, castDistance, context.WhatIsGround)
                    && CowsinsUtilities.IsFloor(refineHit.normal, maxSlopeAngle))
                {
                    if (Vector3.Angle(Vector3.up, refineHit.normal) > Vector3.Angle(Vector3.up, hit.normal))
                        hit = refineHit;
                }
                return true;
            }

            // Fallbacks to Raycast
            // 回退方案：用射线向下检测，并校验坡度是否可站立。
            if (Physics.Raycast(rayOrigin, Vector3.down, out hit,
                castDistance, context.WhatIsGround))
            {
                return CowsinsUtilities.IsFloor(hit.normal, maxSlopeAngle);
            }

            return false;
        }
    }
}