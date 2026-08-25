// ============================================================
// ClimbLadderBehaviour（攀爬梯子行为）
// 负责玩家在梯子上的检测与攀爬：检测前方/空中的梯子，管理进入/退出
// 攀爬状态，处理上/下移动、到达梯顶的翻越冲量，以及基于视角的攀爬方向。
// 支持 WS 专属、视角驱动、组合三种攀爬模式。
// ============================================================
using cowsins;
using UnityEngine;
using static cowsins.PlayerMovementSettings;

/// <summary>处理梯子的检测与攀爬：进入/退出状态、上下移动、梯顶翻越以及三种攀爬模式。</summary>
public class ClimbLadderBehaviour
{
    // 移动上下文：聚合刚体、输入、依赖与设置等共享数据。
    private MovementContext context;
    // 玩家刚体，攀爬时通过 MovePosition 与速度清零来控制位置。
    private Rigidbody rb;
    // 输入管理器：提供 X/Y 轴输入用于判断攀爬方向与意图。
    private InputManager inputManager;
    // 玩家移动状态提供者：读写接地与攀爬标记等状态。
    private IPlayerMovementStateProvider playerMovement;
    // 移动事件提供者：派发攀爬开始/结束等事件。
    private IPlayerMovementEventsProvider playerEvents;
    // 武器引用提供者：提供主相机引用用于读取俯仰角。
    private IWeaponReferenceProvider weaponReference;
    // 玩家朝向：提供 Forward/Right 向量用于梯子检测与移动方向。
    private PlayerOrientation orientation => playerMovement.Orientation;
    // 移动设置：攀爬速度、检测距离、攀爬模式等参数来源。
    private PlayerMovementSettings playerSettings;
    // 梯子所在的物理层掩码，用于射线检测梯子。
    private LayerMask ladderMask;

    /// <summary>构造函数：从移动上下文缓存依赖引用，并解析 "Ladder" 层掩码。</summary>
    /// <param name="context">聚合刚体、输入、依赖与设置的移动上下文。</param>
    public ClimbLadderBehaviour(MovementContext context)
    {
        this.context = context;
        this.rb = context.Rigidbody;
        this.inputManager = context.InputManager;
        this.playerMovement = context.Dependencies.PlayerMovementState;
        this.playerEvents = context.Dependencies.PlayerMovementEvents;
        this.weaponReference = context.Dependencies.WeaponReference;
        this.playerSettings = context.Settings;
        this.ladderMask = LayerMask.GetMask("Ladder");
    }

    /// <summary>进入攀爬状态：关闭重力、标记攀爬并清零速度，避免与物理模拟冲突。</summary>
    public void Enter()
    {
        rb.useGravity = false;
        playerMovement.IsClimbing = true;
        rb.linearVelocity = Vector3.zero;
    }

    /// <summary>每帧处理攀爬移动（委托给 HandleClimbMovement）。</summary>
    public void Tick() => HandleClimbMovement();

    /// <summary>退出攀爬状态：恢复重力、取消攀爬标记并派发攀爬结束事件。</summary>
    public void Exit()
    {
        playerMovement.IsClimbing = false;
        rb.useGravity = true;
        playerEvents.Events.OnClimbStop?.Invoke(playerSettings.hideWeaponWhileClimbing);
    }

    /// <summary>判断当前是否可开始攀爬（即前方是否检测到梯子）。</summary>
    public bool CanExecute()
    {
        return DetectLadders();
    }

    /// <summary>检测玩家前方/空中是否存在可攀爬的梯子，并在满足条件时触发进入攀爬。</summary>
    /// <returns>是否已判定可以开始攀爬。</returns>
    public bool DetectLadders()
    {
        if (!playerSettings.canClimb) return false;

        bool isGrounded = playerMovement.Grounded;

        // Check forward direction for ladder
        bool ladderAhead = Physics.Raycast(context.Transform.position, orientation.Forward,
            playerSettings.maxLadderDetectionDistance, ladderMask);

        // Only attach to climb ladder state when pressing W if player is grounded
        if (isGrounded && ladderAhead)
        {
            // Only if moving forward
            if (inputManager.Y > 0.01f)
            {
                if (playerSettings.ladderMovementMode != LadderMovementMode.WSOnly)
                {
                    float cameraPitch = GetCameraPitch();
                    // 抬头角度过大时不允许进入攀爬。
                    if (cameraPitch > 40f) return false;
                }
                playerEvents.Events.OnClimbStart?.Invoke(playerSettings.hideWeaponWhileClimbing);
                return true;
            }
            return false;
        }

        // If not grounded: Attach when in air
        if (!isGrounded)
        {
            // Backwards: Always attach regardless of look direction
            if (inputManager.Y < -0.01f && ladderAhead)
            {
                playerEvents.Events.OnClimbStart?.Invoke(playerSettings.hideWeaponWhileClimbing);
                return true;
            }

            // forward: Only when looking down
            if (inputManager.Y > 0.01f && IsLookingDown())
            {
                if (ladderAhead)
                {
                    playerEvents.Events.OnClimbStart?.Invoke(playerSettings.hideWeaponWhileClimbing);
                    return true;
                }
            }

            // Strafe: Only when looking down
            if (Mathf.Abs(inputManager.X) > 0.01f && IsLookingDown())
            {
                Vector3 moveDir = GetMovementDirection();
                if (Physics.Raycast(context.Transform.position, moveDir,
                    playerSettings.maxLadderDetectionDistance * 1.5f, ladderMask))
                {
                    playerEvents.Events.OnClimbStart?.Invoke(playerSettings.hideWeaponWhileClimbing);
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>检测是否已到达梯子顶部（前方与稍上方均无梯子）。</summary>
    /// <returns>是否已到梯顶。</returns>
    public bool DetectTopLadder()
    {
        if (!playerSettings.canClimb) return false;

        // Check if ladder no longer detected ahead
        bool noLadderAhead = !Physics.Raycast(context.Transform.position, orientation.Forward,
            playerSettings.maxLadderDetectionDistance, ladderMask);

        // Also check slightly above current position to ensure we are actually at the top of the ladder
        bool isAtTop = !Physics.Raycast(context.Transform.position + Vector3.up * 0.5f,
            orientation.Forward, playerSettings.maxLadderDetectionDistance, ladderMask);

        return noLadderAhead && isAtTop;
    }

    /// <summary>检测是否已到达梯子底部（脚下存在地面）。</summary>
    /// <returns>脚下是否有地面。</returns>
    public bool DetectBottomLadder()
    {
        return Physics.Raycast(context.Transform.position, Vector3.down, 0.6f, context.WhatIsGround);
    }

    /// <summary>到达梯顶时施加向上的翻越冲量与向前推力，把玩家送上平台。</summary>
    public void ApplyForcesOnTopReached()
    {
        // Clear velocity
        rb.linearVelocity = Vector3.zero;

        // Apply impulse
        rb.AddForce(context.Transform.up * playerSettings.topReachedUpperForce, ForceMode.Impulse);
        rb.AddForce(orientation.Forward * playerSettings.topReachedForwardForce, ForceMode.Impulse);
    }

    /// <summary>处理攀爬时的上/下移动：按模式解析输入、阻挡天花板与地面，并移动位置。</summary>
    public void HandleClimbMovement()
    {
        if (PauseMenu.isPaused) return;

        float verticalInput = inputManager.Y;

        // Apply movement mode
        // 根据攀爬模式解析垂直输入。
        switch (playerSettings.ladderMovementMode)
        {
            case LadderMovementMode.WSOnly:
                break;
            case LadderMovementMode.LookBased:
                verticalInput = GetLookBasedClimbInput(verticalInput);
                break;
            case LadderMovementMode.Combined:
                if (verticalInput > 0f)
                    verticalInput = GetLookBasedClimbInput(verticalInput);
                break;
        }

        // Prevent climbing into ceiling
        // 检测头顶是否有障碍，防止爬进天花板。
        bool isObstacleAbove = Physics.Raycast(context.Transform.position, context.Transform.up,
            2f, context.WhatIsGround);
        if (isObstacleAbove && verticalInput > 0f)
            verticalInput = 0;

        // Prevent climbing below ground
        // 已在梯子底部时禁止继续向下爬。
        if (DetectBottomLadder() && verticalInput < 0f)
            verticalInput = 0;

        // Apply movement
        if (Mathf.Abs(verticalInput) > 0.01f)
        {
            // 按输入方向与攀爬速度计算目标位置，并用 MovePosition 平滑移动。
            Vector3 targetPosition = context.Transform.position +
                context.Transform.up * verticalInput * playerSettings.climbSpeed * Time.deltaTime;
            rb.MovePosition(Vector3.Lerp(context.Transform.position, targetPosition, 0.5f));
            rb.linearVelocity = Vector3.zero;
        }
        else
        {
            // Keep velocity zero when not moving to prevent sliding
            rb.linearVelocity = Vector3.zero;
        }
    }

    // 根据相机俯仰角决定基于视角的攀爬方向：抬头向上、低头向下。
    private float GetLookBasedClimbInput(float currentInput)
    {
        if (currentInput <= 0.01f) return 0f;

        float cameraPitch = GetCameraPitch();

        if (cameraPitch > 40f) return -1f;  // Look down = climb down
        if (cameraPitch < -40f) return 1f;  // Look up = climb up

        return 1f;  // Default to climbing up
    }

    // 判断是否存在任何方向上的移动输入。
    private bool IsMovingTowardLadder()
    {
        // Check if any movement input is active
        return Mathf.Abs(inputManager.X) > 0.01f || Mathf.Abs(inputManager.Y) > 0.01f;
    }

    // 判断玩家是否在向下看（俯仰角超过阈值）。
    private bool IsLookingDown()
    {
        float cameraPitch = GetCameraPitch();
        // Looking down threshold
        return cameraPitch > 25f;
    }

    // 根据朝向与输入计算水平移动方向并归一化。
    private Vector3 GetMovementDirection()
    {
        // Calculate movement direction based on input
        Vector3 direction = orientation.Forward * inputManager.Y + orientation.Right * inputManager.X;
        return direction.normalized;
    }

    // 读取主相机俯仰角并规范到 -180~180 范围。
    private float GetCameraPitch()
    {
        float cameraPitch = weaponReference.MainCamera.transform.eulerAngles.x;
        if (cameraPitch > 180f) cameraPitch -= 360f;
        return cameraPitch;
    }
}