// ============================================================
// BasicMovementBehaviour（基础移动行为）
// 负责玩家在水平面上的基础移动：读取输入、计算移动方向、施加加速度与
// 摩擦力、限制对角线与最大速度，并处理斜坡上的速度投影。
// 该行为通常在物理帧（FixedUpdate）中被调用，直接驱动玩家刚体的水平速度。
// ============================================================
using cowsins;
using UnityEngine;
using System; 

/// <summary>处理玩家基础水平移动：输入解析、加速度、摩擦力、限速以及斜坡上的速度投影。</summary>
public class BasicMovementBehaviour
{
    // 移动上下文：聚合刚体、输入、依赖与设置等所有行为共享的数据。
    private MovementContext context;
    // 玩家刚体，移动逻辑直接读写其速度并向其施加力。
    private Rigidbody rb;
    // 输入管理器：提供 X/Y 轴等原始移动输入。
    private InputManager inputManager;
    // 玩家移动状态提供者：暴露接地、蹲伏、当前速度等运行时状态。
    private IPlayerMovementStateProvider playerMovement;
    // 移动事件提供者：用于派发 Idle/Moving 等状态切换事件。
    private IPlayerMovementEventsProvider playerEvents;
    // 玩家控制提供者：判断当前是否可控制、是否允许移动。
    private IPlayerControlProvider playerControl;
    // 武器引用提供者：提供当前武器等运行时引用。
    private IWeaponReferenceProvider weaponReference;
    // 武器行为提供者：访问瞄准等武器行为状态。
    private IWeaponBehaviourProvider weaponController;
    // 玩家倍率提供者：提供重量等影响移动速度的倍率。
    private IPlayerMultipliers playerMultipliers;

    // 玩家朝向：提供基于视角的 Forward/Right 向量与 Yaw。
    private PlayerOrientation orientation => playerMovement?.Orientation;

    // 当前帧计算出的水平移动方向（已归一化）。
    private Vector3 moveDirection;
    // 移动设置：所有移动相关可调参数（加速度、速度、滑铲等）。
    private PlayerMovementSettings playerSettings;

    // 玩家胶囊碰撞体，缓存引用供碰撞/接地检测等使用。
    private CapsuleCollider playerCapsuleCollider;

    // 摩擦生效的速度阈值：分速度绝对值低于该值时不施加摩擦。
    private const float frictionThreshold = 0.1f;
    // 斜坡重力补偿倍数：用于斜坡上贴地时抵消下滑的辅助量。
    private const float slopeGravityMultiplier = 150;
    // 额外下坠力倍数：在非斜坡时施加的向下力，增强落地手感。
    private const float extraGravityMultiplier = 10f;
    // 上一帧是否处于移动状态，用于判定 Idle 与 Moving 之间的切换。
    private bool wasMovingLastFrame;
    /// <summary>构造函数：从移动上下文中缓存所有依赖引用，并获取移动设置与胶囊碰撞体。</summary>
    /// <param name="context">聚合了刚体、输入、依赖与设置的移动上下文。</param>
    public BasicMovementBehaviour(MovementContext context)
    {
        this.context = context;
        this.rb = context.Rigidbody;
        this.inputManager = context.InputManager;

        this.playerMovement = context.Dependencies.PlayerMovementState;
        this.playerEvents = context.Dependencies.PlayerMovementEvents;
        this.playerControl = context.Dependencies.PlayerControl;
        this.weaponReference = context.Dependencies.WeaponReference;
        this.weaponController = context.Dependencies.WeaponBehaviour;
        this.playerMultipliers = context.Dependencies.PlayerMultipliers;

        this.playerSettings = context.Settings;

        this.playerCapsuleCollider = context.Capsule;
    }

    /// <summary>
    /// Handle all the basics related to the movement of the player.
    /// </summary>
    // 处理玩家基础移动的主入口：施加额外重力、摩擦与限速，并根据斜坡情况投影速度。
    public void Movement()
    {
        // 若不允许移动（如被眩晕/锁定），直接跳过本帧移动处理。
        if(!playerControl.IsMovementControllable) return;

        // Extra gravity should be skipped on slope
        if (!context.IsPlayerOnSlope)
            rb.AddForce(Vector3.down * Time.fixedDeltaTime * extraGravityMultiplier);

        //Find actual velocity relative to where player is looking
        Vector2 relativeVelocity = FindVelRelativeToLook();

        // Counteract sliding and sloppy movement.
        FrictionForce(inputManager.X, inputManager.Y, relativeVelocity);
        //If speed is larger than maxspeed, clamp the velocity so you don't go over max speed
        LimitDiagonalVelocity();

        if (!playerControl.IsControllable)
        {
            if (playerMovement.Grounded) rb.linearVelocity = Vector3.zero;
            return;
        }

        Vector3 horizontalVel = new Vector3(rb.linearVelocity.x, 0, rb.linearVelocity.z);
        bool isCrouchSliding = playerMovement.IsCrouching && horizontalVel.magnitude >= playerMovement.CrouchSpeed;

        if (isCrouchSliding && !playerSettings.allowMoveWhileSliding) return;

        // 空中时应用空中操控系数，降低空中转向能力。
        float airborneMultiplier = !playerMovement.Grounded ? playerSettings.controlAirborne : 1;
        float movementMultipliers = playerSettings.acceleration * Time.deltaTime * airborneMultiplier;

        // Reduce movement influence while sliding if sliding is active and movement while sliding isn't allowed
        if (isCrouchSliding && !playerSettings.allowMoveWhileSliding)
            movementMultipliers *= 0f;

        CalculateMoveDirection();
        CallEvents();

        // 斜坡上增强加速度，让玩家更稳地贴坡移动。
        movementMultipliers *= context.IsPlayerOnSlope ? 2f : 1;
        // If crouch-sliding, respect steering multiplier and don't add full movement force
        movementMultipliers *= isCrouchSliding ? playerSettings.slideSteerMultiplier : 1;

        ApplyAirCameraInfluence();

        // Calculate the velocity change
        // 由加速度换算得到本帧的速度变化量（除以质量以适配不同刚体质量）。
        float velocityChange = (movementMultipliers / rb.mass) * Time.fixedDeltaTime;
            
        // Apply to current velocity to preserve momentum and add new input
        // 在当前速度基础上叠加输入产生的速度增量，保留动量。
        Vector3 targetVelocity = rb.linearVelocity + moveDirection * velocityChange;

        // Clamp Horizontal Scale
        // 仅对水平分量限速，垂直分量交由重力处理。
        Vector3 horizontalTarget = new Vector3(targetVelocity.x, 0, targetVelocity.z);
        float maxSpeed = playerMovement.CurrentSpeed * playerMultipliers.WeightMultiplier.Value;
            
        if (horizontalTarget.magnitude > maxSpeed)
        {
            horizontalTarget = horizontalTarget.normalized * maxSpeed;
            targetVelocity.x = horizontalTarget.x;
            targetVelocity.z = horizontalTarget.z;
        }
            
        // project current velocity onto the slope plane, then add input force.
        // 斜坡分支：将速度投影到坡面，使移动贴合斜面而非被弹起。
        if (context.IsPlayerOnSlope && !context.HasJumped)
        {
            Vector3 slopeVelocity = Vector3.ProjectOnPlane(rb.linearVelocity, context.SlopeHit.normal)
                                    + moveDirection * velocityChange;
            if (slopeVelocity.magnitude > maxSpeed)
                slopeVelocity = slopeVelocity.normalized * maxSpeed;
            rb.linearVelocity = slopeVelocity;
        }
        else
        {
            // rely on the standard physics gravity
            // 平地分支：保留受物理重力影响的垂直速度，避免贴地时反复向下累积。
            float yVel = (playerMovement.Grounded && !context.HasJumped && rb.linearVelocity.y <= 0f)
                ? Mathf.Min(rb.linearVelocity.y, 0f)
                : rb.linearVelocity.y;
            rb.linearVelocity = new Vector3(targetVelocity.x, yVel, targetVelocity.z);
        }
    }

    // 计算当前移动方向：斜坡上沿坡面投影，否则基于朝向与输入的组合并归一化。
    private void CalculateMoveDirection()
    {
        if (context.IsPlayerOnSlope)
        {
            moveDirection = GetSlopeDirection();
            
            if (moveDirection.magnitude == 0 && !context.HasJumped) rb.linearVelocity = Vector3.zero;
        }
        else
        {
            moveDirection = (orientation.Forward * inputManager.Y + orientation.Right * inputManager.X).normalized;
        }
    }

    // 在空中且按住前进时，让相机朝向参与加速，实现“空中转向”手感（可选）。
    private void ApplyAirCameraInfluence()
    {
        if(!playerSettings.allowAirCameraInfluence) return;

        if (!playerMovement.Grounded && inputManager.Y > 0.1f)
        {
            Vector3 wishdir;

            if (context.Camera != null)
            {
                Vector3 camForward = Vector3.ProjectOnPlane(context.Camera.forward, Vector3.up).normalized;
                Vector3 camRight = Vector3.ProjectOnPlane(context.Camera.right, Vector3.up).normalized;

                Vector3 cameraDir = camForward * inputManager.Y + camRight * inputManager.X;
                wishdir = cameraDir.normalized;
            }
            else
            {
                Vector3 orientDir = orientation.Forward * inputManager.Y + orientation.Right * inputManager.X;
                if (orientDir.sqrMagnitude < 0.0001f) orientDir = orientation.Forward;
                wishdir = orientDir.normalized;
            }

            float wishspeed = playerMovement.RunSpeed;
            AirAccelerate(wishdir, wishspeed, playerSettings.airCameraInfluenceAmount);
        }
    }

    // 空中加速：沿给定方向加速到目标速度，仅在水平面进行，不改变垂直速度。
    private void AirAccelerate(Vector3 dir, float speed, float accel)
    {
        Vector3 vel = rb.linearVelocity;
        Vector3 velHoriz = new Vector3(vel.x, 0f, vel.z);

        float currentSpeed = Vector3.Dot(velHoriz, dir);
        float addSpeed = speed - currentSpeed;
        if (addSpeed <= 0f) return;

        float accelSpeed = accel * Time.fixedDeltaTime * speed;
        if (accelSpeed > addSpeed) accelSpeed = addSpeed;

        Vector3 newVel = vel + dir * accelSpeed;
        rb.linearVelocity = new Vector3(newVel.x, vel.y, newVel.z);
    }

    // 根据当前移动状态派发 Idle/Moving 及其切换事件。
    private void CallEvents()
    {
        bool isMoving = moveDirection.magnitude > .1f;

        if (isMoving && !wasMovingLastFrame)
        {
            // Transition Idle -> Moving
            playerEvents.Events.OnIdleToMove?.Invoke();
        }
        else if (!isMoving && wasMovingLastFrame)
        {
            // Transition Moving -> Idle
            playerEvents.Events.OnMovingToIdle?.Invoke();
        }


        if (moveDirection.magnitude > .1f)
        {
            playerEvents.Events.OnMoving?.Invoke();
            playerSettings.events.OnMoving?.Invoke();
        }
        else
        {
            playerEvents.Events.OnIdle?.Invoke();
            playerSettings.events.OnIdle?.Invoke();
        }
        wasMovingLastFrame = isMoving;
    }


    /// <summary>
    /// Limits diagonal velocity
    /// </summary>
    // 限制对角线速度：将水平速度裁剪到当前加权最大速度，防止斜向移动超速。
    private void LimitDiagonalVelocity()
    {
        Vector3 horizontalVelocity = new Vector3(rb.linearVelocity.x, 0, rb.linearVelocity.z);
        float currentWeightedSpeed = playerMovement.CurrentSpeed * playerMultipliers.WeightMultiplier.Value;
        if (horizontalVelocity.magnitude > currentWeightedSpeed)
        {
            horizontalVelocity = horizontalVelocity.normalized * currentWeightedSpeed;
            rb.linearVelocity = new Vector3(horizontalVelocity.x, rb.linearVelocity.y, horizontalVelocity.z);
        }
    }

    /// <summary>
    /// Find the velocity relative to where the player is looking
    /// Useful for vectors calculations regarding movement and limiting movement
    /// </summary>
    /// <returns></returns>
    // 将世界速度转换到玩家朝向的本地空间，返回其水平 X/Z 分量，便于摩擦与限速计算。
    private Vector2 FindVelRelativeToLook()
    {
        // Convert velocity to local space relative to the player's look direction
        Vector3 localVel = Quaternion.Euler(0, -orientation.Yaw, 0) * rb.linearVelocity;
        return new Vector2(localVel.x, localVel.z);
    }

    /// <summary>
    /// Get the direction of movement in a slope
    /// </summary>
    /// <returns></returns>
    // 基于朝向与输入组合后投影到坡面法线平面，得到沿坡面的移动方向。
    private Vector3 GetSlopeDirection()
    {
        // Use the ground raycast normal recorded in the shared context
        return Vector3.ProjectOnPlane(orientation.Forward * inputManager.Y + orientation.Right * inputManager.X, context.SlopeHit.normal).normalized;
    }

    /// <summary>
    /// Add friction force to the player when it�s not airborne
    /// Please note that it counters movement, since it goes in the opposite direction to velocity
    /// </summary>
    // 施加与速度反向的摩擦力，用于抵消滑动、提升停步手感；滑铲时可改用独立摩擦。
    private void FrictionForce(float x, float y, Vector2 mag)
    {
        // Prevent from adding friction on an airborne body
        if (!playerMovement.Grounded || inputManager.Jumping || context.HasJumped) return;

        Vector3 horizontalVel = new Vector3(rb.linearVelocity.x, 0, rb.linearVelocity.z);
        bool isCrouchSliding = playerMovement.IsCrouching && horizontalVel.magnitude >= playerMovement.CrouchSpeed;

        // If crouch-sliding and sliding friction is disabled, skip friction here. Sliding behaviour handles deceleration.
        if (isCrouchSliding && !playerSettings.applyFrictionForceOnSliding) return;

        // 根据是否滑铲选择不同的摩擦系数。
        float friction = isCrouchSliding ? playerSettings.slideFrictionForceAmount : playerSettings.controlsResponsiveness;

        // Counter movement ( Friction while moving )
        float frictionDeltaVScale = playerSettings.acceleration * Time.fixedDeltaTime * friction / rb.mass;

        // 当分速度超过阈值且输入与该方向速度不一致（松手或反向）时，沿右向量施加摩擦。
        if (Math.Abs(mag.x) > frictionThreshold && Math.Abs(x) < 0.5f || (mag.x < -frictionThreshold && x > 0) || (mag.x > frictionThreshold && x < 0))
        {
            float rawDeltaV = frictionDeltaVScale * Time.fixedDeltaTime * -mag.x;
            // friction can stop velocity but must not reverse its sign
            rawDeltaV = Mathf.Clamp(rawDeltaV, -Mathf.Abs(mag.x), Mathf.Abs(mag.x));
            rb.linearVelocity += orientation.Right * rawDeltaV;
        }
        // 同理，对前后方向（Forward 轴）的分速度施加摩擦。
        if (Math.Abs(mag.y) > frictionThreshold && Math.Abs(y) < 0.05f || (mag.y < -frictionThreshold && y > 0) || (mag.y > frictionThreshold && y < 0))
        {
            float rawDeltaV = frictionDeltaVScale * Time.fixedDeltaTime * -mag.y;
            rawDeltaV = Mathf.Clamp(rawDeltaV, -Mathf.Abs(mag.y), Mathf.Abs(mag.y));
            rb.linearVelocity += orientation.Forward * rawDeltaV;
        }
    }
}

