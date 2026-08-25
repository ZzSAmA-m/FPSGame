// ============================================================
// CrouchSlideBehaviour（蹲伏与滑铲行为）
// 负责玩家的蹲伏与滑铲：管理蹲伏缩放、滑铲的加速/转向/计时，
// 处理滑铲结束与起身判定（含头顶障碍检测），并在合适条件下触发滑铲。
// 通过 Tick 平滑蹲伏缩放，FixedTick 驱动滑铲物理。
// ============================================================
using cowsins;
using UnityEngine;

/// <summary>处理玩家的蹲伏与滑铲：蹲伏缩放、滑铲加速/转向/计时、起身判定与结束。</summary>
public class CrouchSlideBehaviour
{
    // 移动上下文：聚合刚体、输入、依赖与设置等共享数据。
    private MovementContext context;
    // 玩家刚体，滑铲时直接读写速度与施加力。
    private Rigidbody rb;
    // 输入管理器：提供蹲伏键与 X/Y 轴输入。
    private InputManager inputManager;
    // 玩家移动状态提供者：读写蹲伏/滑铲标记与速度等状态。
    private IPlayerMovementStateProvider playerMovement;
    // 移动事件提供者：订阅/派发蹲伏与滑铲相关事件。
    private IPlayerMovementEventsProvider playerEvents;

    // 移动设置：蹲伏/滑铲相关参数与音效来源。
    private PlayerMovementSettings playerSettings;

    // 玩家朝向：提供 Forward/Right 向量用于确定滑铲方向。
    private PlayerOrientation orientation => playerMovement.Orientation;
    // 站立时的原始缩放，用于起身时恢复。
    private Vector3 playerScale;
    // 蹲伏时的目标缩放（高度减半）。
    public Vector3 crouchScale { get; private set; } = new Vector3(1, 0.5f, 1);
    // 头顶无障碍时是否允许起身。
    private bool canUnCrouch = false;
    // 滑铲剩余持续时间。
    private float slideTimer = 0f;
    // 滑铲方向（水平且已归一化）。
    private Vector3 slideDirection = Vector3.zero;
    // 滑铲初始加速的剩余持续时间。
    private float slideBoostRemaining = 0f;
    // 是否处于滑铲初始加速阶段。
    private bool isBoosting = false;
    // 滑铲期间累计的离地时长，用于判定是否已真正离地。
    private float slideAirborneTimer = 0f; // how long we have been airborne mid slide
    // 滑铲允许的短暂离地容差，超过则结束滑铲。
    private const float slideAirborneTolerance = 0.25f;

    /// <summary>构造函数：从移动上下文缓存依赖引用，记录原始缩放并订阅滑铲允许事件。</summary>
    /// <param name="context">聚合刚体、输入、依赖与设置的移动上下文。</param>
    public CrouchSlideBehaviour(MovementContext context)
    {
        this.context = context;
        this.rb = context.Rigidbody;
        this.inputManager = context.InputManager;

        this.playerMovement = context.Dependencies.PlayerMovementState;
        this.playerEvents = context.Dependencies.PlayerMovementEvents;

        this.playerSettings = context.Settings;
        this.playerScale = context.Transform.localScale;

        playerEvents.Events.AllowSlide += AllowSliding;
    }

    /// <summary>释放资源：取消对 AllowSlide 事件的订阅。</summary>
    public void Dispose()
    {
        playerEvents.Events.AllowSlide -= AllowSliding;
    }
    /// <summary>进入蹲伏：标记蹲伏、播放音效，并在满足条件时启动滑铲。</summary>
    public void Enter()
    {
        if (!playerSettings.allowCrouch) return;

        playerMovement.IsCrouching = true;

        playerSettings.events.OnCrouch.Invoke();
        playerEvents.Events.OnCrouchStart?.Invoke(); // Internal Event
        SoundManager.Instance.PlaySound(playerSettings.sounds.startCrouchSFX, 0, 0, false);

        // Start sliding when conditions match.
        // 速度足够且接地、允许滑铲、未跳跃时启动滑铲。
        if (rb.linearVelocity.magnitude >= playerMovement.WalkSpeed && playerMovement.CurrentSpeed > playerMovement.WalkSpeed && playerMovement.Grounded && playerSettings.allowSliding && !context.HasJumped)
        {
            Vector3 horizontalVel = new Vector3(rb.linearVelocity.x, 0, rb.linearVelocity.z);
            if (horizontalVel.magnitude > 0.1f)
                slideDirection = horizontalVel.normalized;
            else
                slideDirection = Vector3.ProjectOnPlane(orientation.Forward, Vector3.up).normalized;

            slideBoostRemaining = Mathf.Max(0.0001f, playerSettings.slideBoostDuration);
            isBoosting = true;

            // Begin local boost and timer.
            slideTimer = playerSettings.slideDuration;

            playerSettings.events.OnSlideStart.Invoke();
            playerEvents.Events.OnSlideStart?.Invoke();
            SoundManager.Instance.PlaySound(playerSettings.sounds.slideSFX, 0, 0, false);
        }
    }

    /// <summary>每帧处理蹲伏缩放：未按蹲伏键时平滑起身，蹲伏/加速期间维持蹲伏缩放。</summary>
    public void Tick() 
    {
        Transform transform = context.Transform;
        // When not crouching, smoothly stand up
        if (!inputManager.Crouching)
        {
            playerMovement.IsCrouching = false;
            transform.localScale = Vector3.MoveTowards(transform.localScale, playerScale, Time.deltaTime * playerSettings.crouchTransitionSpeed);
            return;
        }

        // Maintain crouch scale while crouching or during the short boost window for the slide
        if (playerMovement.IsCrouching || isBoosting)
        {
            transform.localScale = Vector3.MoveTowards(transform.localScale, crouchScale, Time.deltaTime * playerSettings.crouchTransitionSpeed * 1.5f);
        }
    }

    /// <summary>物理帧驱动滑铲：处理离地容差、斜坡投影、初始加速、转向与结束判定。</summary>
    public void FixedTick()
    {
        if (!playerMovement.IsSliding) return;

        // Allow the slide to survive brief airtime
        // 允许滑铲短暂离地，超过容差则结束滑铲。
        if (!playerMovement.Grounded)
        {
            slideAirborneTimer += Time.fixedDeltaTime;
            if (slideAirborneTimer > slideAirborneTolerance)
            {
                // the player is genuinely airborne, end the slide
                EndSlide();
            }

            return;
        }

        // Back on the ground
        slideAirborneTimer = 0f;

        // on a slope, project onto the surface so the slide follows it
        // 斜坡上将速度投影到坡面，让滑铲贴合斜面。
        if (context.IsPlayerOnSlope && !context.HasJumped)
        {
            rb.linearVelocity = Vector3.ProjectOnPlane(rb.linearVelocity, context.SlopeHit.normal);
        }
        else if (rb.linearVelocity.y > 0f)
        {
            Vector3 v = rb.linearVelocity;
            v.y = 0f;
            rb.linearVelocity = v;
        }

        // Apply initial boost
        // 初始加速阶段：在持续时间内分布施加滑铲推力。
        if (isBoosting && slideBoostRemaining > 0f)
        {
            float dt = Time.fixedDeltaTime;
            // Distribute the configured slideForce across the boost duration as acceleration
            float boostAmount = playerSettings.slideForce * dt / Mathf.Max(playerSettings.slideBoostDuration, dt);
            rb.AddForce(slideDirection * boostAmount, ForceMode.Acceleration);
            slideBoostRemaining -= dt;
            if (slideBoostRemaining <= 0f) isBoosting = false;
        }

        // Steering while sliding
        // 滑铲转向：根据输入方向施加额外转向力。
        Vector3 inputDir = (orientation.Forward * inputManager.Y + orientation.Right * inputManager.X);
        inputDir.y = 0;
        if (inputDir.sqrMagnitude > 0.0001f)
        {
            Vector3 steer = inputDir.normalized * playerSettings.acceleration * playerSettings.slideSteerMultiplier * Time.fixedDeltaTime;
            rb.AddForce(steer, ForceMode.Acceleration);
        }

        // Decrease timer and check for stop condition
        // 递减滑铲计时，并在计时结束或速度过低时结束滑铲。
        slideTimer -= Time.fixedDeltaTime;
        Vector3 horizontalVel = new Vector3(rb.linearVelocity.x, 0, rb.linearVelocity.z);
        if (slideTimer <= 0 || horizontalVel.magnitude < playerSettings.slideStopSpeed)
        {
            EndSlide();
        }
    }

    /// <summary>退出蹲伏/滑铲：派发停止事件、播放音效并结束滑铲。</summary>
    public void Exit()
    {
        playerEvents.Events.OnCrouchStop?.Invoke();
        playerSettings.events.OnStopCrouch?.Invoke();
        SoundManager.Instance.PlaySound(playerSettings.sounds.stopCrouchSFX, 0, 0, false);
        EndSlide();
    }

    /// <summary>判断是否可进入蹲伏（按下蹲伏键且不在墙跑等受限状态）。</summary>
    public bool CanExecute()
    {
        return inputManager.Crouching && !playerMovement.IsWallRunning && playerSettings.allowCrouch && (playerMovement.Grounded || !playerMovement.Grounded && playerSettings.allowCrouchWhileJumping);
    }


    /// <summary>处理蹲伏：维持蹲伏缩放并标记蹲伏状态。</summary>
    public void HandleCrouch()
    {
        if (inputManager.Crouching)
        {
            context.Transform.localScale = Vector3.MoveTowards(context.Transform.localScale, crouchScale, Time.deltaTime * playerSettings.crouchTransitionSpeed * 1.5f);
        }

        playerMovement.IsCrouching = true;

        playerEvents.Events.OnCrouching?.Invoke();
    }

    /// <summary>检查是否可以起身：仅在头顶无障碍且缩放恢复到位时返回 true。</summary>
    /// <returns>是否已完成起身。</returns>
    public bool CheckUnCrouch()
    {
        if (!inputManager.Crouching) // Prevent from uncrouching when there�s a roof and we can get hit with it
        {
            RaycastHit hit;
            bool isObstacleAbove = Physics.Raycast(context.Transform.position, context.Transform.up, out hit, playerSettings.roofCheckDistance, context.WhatIsGround);

            canUnCrouch = !isObstacleAbove;

            if (canUnCrouch)
            {
                Tick();
                if (context.Transform.localScale == playerScale)
                    return true;
            }
        }

        return false;
    }

    // 返回是否允许滑铲（直接读取设置）。
    private bool AllowSliding() => playerSettings.allowSliding;

    // 结束滑铲：清除滑铲标记并保留部分水平动量。
    private void EndSlide()
    {
        if (!playerMovement.IsSliding) return;

        slideAirborneTimer = 0f;
        playerMovement.IsSliding = false;
        //playerEvents.Events.OnSlideEnd?.Invoke();
        playerSettings.events.OnStopCrouch?.Invoke();

        Vector3 vel = rb.linearVelocity;
        Vector3 horizontal = new Vector3(vel.x, 0, vel.z);

        rb.linearVelocity = new Vector3(horizontal.magnitude > 0 ? horizontal.normalized.x * Mathf.Max(0, horizontal.magnitude * 0.6f) : 0, vel.y, horizontal.magnitude > 0 ? horizontal.normalized.z * Mathf.Max(0, horizontal.magnitude * 0.6f) : 0);
    }
}
