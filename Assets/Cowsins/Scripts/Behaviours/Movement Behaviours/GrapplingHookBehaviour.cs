// ============================================================
// GrapplingHookBehaviour（钩爪/抓钩行为）
// 负责玩家的钩爪：从相机向前射线检测锚点，通过弹簧关节或组合模式
// 施加拉拽力，绘制带波浪效果的绳索（LineRenderer），并管理冷却与退出。
// 支持线性、摆动、组合三种钩爪模式。
// ============================================================
using cowsins;
using UnityEngine;
using System.Collections;

/// <summary>处理钩爪：锚点检测、拉力施加、绳索绘制、冷却管理与三种钩爪模式。</summary>
public class GrapplingHookBehaviour
{
    // 移动上下文：聚合刚体、依赖、设置与相机等共享数据。
    private MovementContext context;
    // 玩家刚体，钩爪拉拽力与速度限制直接作用于它。
    private Rigidbody rb;
    // 玩家移动状态提供者：暴露朝向等状态。
    private IPlayerMovementStateProvider playerMovement;
    // 玩家控制提供者：判断当前是否可控制以允许发射钩爪。
    private IPlayerControlProvider playerControl;
    // 移动事件提供者：派发钩爪开始等事件。
    private IPlayerMovementEventsProvider playerEvents;
    // 武器引用提供者：提供主相机与武器（用于发射点与射线）。
    private IWeaponReferenceProvider weaponReference;

    // 玩家朝向：提供 Forward/Right 向量。
    private PlayerOrientation orientation => playerMovement.Orientation;

    // 钩爪是否可用（冷却结束后恢复）。
    // 钩爪是否可用（冷却结束后恢复）。
    public bool grappleEnabled = true;
    // 连接玩家与锚点的弹簧关节（线性/摆动模式）。
    public SpringJoint joint;
    public Vector3 grapplePoint;
    // 用于绘制绳索的 LineRenderer。
    public LineRenderer grappleRenderer;

    // 可钩取的物理层掩码。
    private LayerMask whatIsGround;

    // 移动设置：钩爪力度、距离、模式、绳索参数等来源。
    private PlayerMovementSettings playerSettings; 
    // 相机 Transform，用于钩爪射线与组合模式的相机转向。
    private Transform playerCam;

    // 绳索绘制进度计时。
    private float elapsedTime;

    // 钩爪是否已命中并开始施加力。
    private bool grappleImpacted = false;
    // 绳索波浪效果的当前振幅（逐渐衰减）。
    private float currentWaveAmplitude = 0;

    // 冷却协程引用，避免重复启动。
    private Coroutine cooldownCoroutine;
    // 用于启动协程的 MonoBehaviour。
    private MonoBehaviour CoroutineRunner;

    // 是否正在钩爪中。
    private bool isGrappling;
    // 组合模式下的初始绳长。
    private float _initialDistance;
    // 组合模式下当前绳长（每帧收短）。
    private float _currentRopeLength;

    /// <summary>构造函数：缓存依赖引用并获取绳索 LineRenderer；禁止钩爪时销毁渲染器。</summary>
    /// <param name="context">聚合刚体、依赖、设置与相机的移动上下文。</param>
    public GrapplingHookBehaviour(MovementContext context)
    {
        this.context = context;
        this.rb = context.Rigidbody;
        this.playerMovement = context.Dependencies.PlayerMovementState;
        this.playerControl = context.Dependencies.PlayerControl;
        this.playerEvents = context.Dependencies.PlayerMovementEvents;
        this.weaponReference = context.Dependencies.WeaponReference;

        this.playerSettings = context.Settings;
        this.grappleRenderer = context.Transform.GetComponent<LineRenderer>();
        this.whatIsGround = context.WhatIsGround;
        this.CoroutineRunner = context.Transform.GetComponent<MonoBehaviour>();    
        this.playerCam = context.Camera;

        if (!playerSettings.allowGrapple) Object.Destroy(grappleRenderer);
    }
    /// <summary>尝试发射钩爪：从相机向前射线检测可钩取表面，命中后进入钩爪状态。</summary>
    public void Enter()
    {
        if (!grappleEnabled || isGrappling || !playerControl.IsControllable || weaponReference.MainCamera == null) return;
        RaycastHit hit;

        if (Physics.Raycast(weaponReference.MainCamera.transform.position, weaponReference.MainCamera.transform.forward, out hit, playerSettings.maxGrappleDistance, whatIsGround))
        {
            isGrappling = true;
            grapplePoint = hit.point;

            playerSettings.events.OnStartGrapple?.Invoke();
            if (playerSettings.sounds.startGrappleSFX)
                SoundManager.Instance.PlaySound(playerSettings.sounds.startGrappleSFX, 0, 0, false);

            playerEvents.Events.OnGrappleStart?.Invoke();
        }
    }

    // 根据钩爪模式创建弹簧关节或初始化组合模式的绳长参数。
    private void ApplyGrappleForces()
    {
        float distanceFromAnchor = Vector3.Distance(context.Transform.position, grapplePoint);
        bool isCombined = playerSettings.grapplingHookMethod == PlayerMovementSettings.GrapplingHookMethod.Combined;

        if (!isCombined)
        {
            joint = context.Transform.gameObject.AddComponent<SpringJoint>();
            joint.autoConfigureConnectedAnchor = false;
            joint.connectedAnchor = grapplePoint;

            if (playerSettings.grapplingHookMethod == PlayerMovementSettings.GrapplingHookMethod.Linear)
            {
                joint.maxDistance = playerSettings.grappleRopeLength;
                joint.minDistance = playerSettings.grappleRopeLength * 0.25f;
            }
            else // Swing
            {
                joint.maxDistance = distanceFromAnchor;
                joint.minDistance = 0f;
            }

            joint.spring    = playerSettings.grappleSpringForce;
            joint.damper    = playerSettings.grappleDamper;
            joint.massScale = 4.5f;
        }
        else // Combined mode
        {
            joint = null;
            _currentRopeLength = distanceFromAnchor;
            _initialDistance   = distanceFromAnchor;
        }
    }

    /// <summary>每帧更新绳索绘制，并按模式施加拉拽力、收短绳长与速度限制。</summary>
    public void Tick() 
    {
        UpdateGrappleRenderer();

        if (!isGrappling || !grappleImpacted) return;

        Vector3 toGrapple = grapplePoint - context.Transform.position;
        float distance = toGrapple.magnitude;

        // 距离过近（已接近锚点）时结束钩爪。
        if (distance < playerSettings.distanceToBreakGrapple)
        {
            Exit();
            return;
        }

        Vector3 normalizedDirection = toGrapple.normalized;
        Vector3 force = normalizedDirection * playerSettings.grappleForce;

        // 线性模式：按距离缩放拉力并清零垂直速度。
        if (playerSettings.grapplingHookMethod == PlayerMovementSettings.GrapplingHookMethod.Linear)
        {
            // Scale force based on distance
            float distanceMultiplier = Mathf.Clamp(distance, 1f, 10f);
            force *= distanceMultiplier;

            // Reset vertical velocity to avoid bouncing
            Vector3 currentVelocity = rb.linearVelocity;
            currentVelocity.y = 0;
            rb.linearVelocity = currentVelocity;

            rb.AddForce(force, ForceMode.Force);
        }
        // 组合模式：摆动 + 收绳 + 相机转向 + 限速。
        else if (playerSettings.grapplingHookMethod == PlayerMovementSettings.GrapplingHookMethod.Combined)
        {
            // Shorten rope each frame
            _currentRopeLength = Mathf.Max(
                _currentRopeLength - playerSettings.combinedGrappleReelSpeed * Time.deltaTime,
                playerSettings.distanceToBreakGrapple);

            // Main Pendulum mechanism
            if (distance >= _currentRopeLength)
            {
                float outward = Vector3.Dot(rb.linearVelocity, -normalizedDirection);
                if (outward > 0f)
                    rb.linearVelocity += normalizedDirection * outward;
            }

            // Pull force toward anchor
            float distanceFade = Mathf.Clamp01((distance - playerSettings.distanceToBreakGrapple * 2f) / 6f);
            rb.AddForce(normalizedDirection * playerSettings.combinedGrapplePullForce * distanceFade, ForceMode.Acceleration);

            // Camera steering
            if (playerSettings.cameraInfluence > 0f && distanceFade > 0f)
            {
                Vector3 camTangential = Vector3.ProjectOnPlane(playerCam.forward, normalizedDirection);
                if (camTangential.sqrMagnitude > 0.001f)
                    rb.AddForce(camTangential.normalized * playerSettings.cameraInfluence, ForceMode.Acceleration);
            }

            // Speed cap
            float maxSpeed = playerSettings.combinedGrappleMaxSpeed;
            if (rb.linearVelocity.magnitude > maxSpeed)
                rb.linearVelocity = Vector3.Lerp(rb.linearVelocity, rb.linearVelocity.normalized * maxSpeed, Time.deltaTime * 4f);
        }
    }

    /// <summary>结束钩爪：禁用钩爪、清空绳索、销毁关节并启动冷却协程。</summary>
    public void Exit() 
    {
        if (!isGrappling) return;
        isGrappling = false;
        grappleEnabled = false;


        if (cooldownCoroutine != null)
        {
            CoroutineRunner.StopCoroutine(cooldownCoroutine);
            cooldownCoroutine = null;
        }

        cooldownCoroutine = CoroutineRunner.StartCoroutine(GrappleCooldownRoutine());

        grappleRenderer.positionCount = 0; // Reset the quality/resolution of the rope
        if (joint != null) UnityEngine.Object.Destroy(joint);

        playerSettings.events.OnStopGrapple?.Invoke();// Perform custom methods
    }

    /// <summary>更新绳索 LineRenderer：渐进绘制、施加波浪效果并触发命中事件。</summary>
    public void UpdateGrappleRenderer()
    {
        if (isGrappling)
        {
            playerSettings.events.OnGrappling?.Invoke(); // Perform custom methods
            int ropeResolution = playerSettings.ropeResolution;

            // 首次绘制时初始化绳索分辨率与起点。
            if (grappleRenderer.positionCount == 0)
            {
                // Initial setup when starting to grapple
                grappleRenderer.positionCount = ropeResolution;
                for (int i = 0; i < ropeResolution; i++)
                {
                    grappleRenderer.SetPosition(i, context.Transform.position);
                    currentWaveAmplitude = playerSettings.waveAmplitude;
                }
                elapsedTime = 0f;
                grappleImpacted = false;
            }

            // Update the line renderer progressively
            elapsedTime += Time.deltaTime;
            if (elapsedTime >= playerSettings.drawDuration && !grappleImpacted)
            {
                if (playerSettings.sounds.grappleImpactSFX)
                    SoundManager.Instance.PlaySound(playerSettings.sounds.grappleImpactSFX, 0, 0, false);
                playerSettings.events.OnGrappleImpacted?.Invoke();
                grappleImpacted = true;
                ApplyGrappleForces();
            }
            float t = Mathf.Clamp01(elapsedTime / playerSettings.drawDuration);

            currentWaveAmplitude -= Time.deltaTime * playerSettings.waveAmplitudeMitigation;
            currentWaveAmplitude = Mathf.Clamp(currentWaveAmplitude, 0, 1.4f);
            Vector3 startPos = weaponReference.Id != null && weaponReference.Weapon != null && weaponReference.Weapon.grapplesFromTip ?
                weaponReference.Id.FirePoint[0].position : context.Transform.position;
            Vector3 endPos = Vector3.Lerp(context.Transform.position, grapplePoint, t);
            Vector3[] points = new Vector3[ropeResolution];
            points[0] = startPos;
            points[ropeResolution - 1] = endPos;

            // Apply wave effect to the rope
            for (int i = 1; i < ropeResolution - 1; i++)
            {

                float waveOffset = (float)i / (ropeResolution - 1);
                // Using sine function for oscillation
                float yOffset = Mathf.Sin(Time.time * 60 + waveOffset * Mathf.PI * 2f) * currentWaveAmplitude;
                Vector3 pointPos = Vector3.Lerp(startPos, endPos, waveOffset);
                pointPos.y += yOffset;
                points[i] = pointPos;
            }

            grappleRenderer.SetPositions(points);
        }
        else
        {
            // Stop grappling
            grappleRenderer.positionCount = 0;
        }
    }

    // 冷却结束后重新启用钩爪并派发事件。
    private void EnableGrapple()
    {
        grappleEnabled = true;
        playerSettings.events.OnGrappleEnabled?.Invoke();
    }
    // 协程：等待冷却时间后重新启用钩爪。
    private IEnumerator GrappleCooldownRoutine()
    {
        yield return new WaitForSeconds(playerSettings.grappleCooldown);
        EnableGrapple();
    }
}
