// 文件：PlayerMovement.cs
// 作用：玩家移动组件：负责初始化并协调所有移动行为（基础移动、视角、跳跃、蹲伏滑铲、冲刺、
//       墙跳、爬梯、墙跑、钩索、体力、脚步、速度线等），维护移动上下文，并实现移动相关接口。
/// <summary>
/// This script belongs to cowsins™ as a part of the cowsins´ FPS Engine. All rights reserved. 
/// </summary>
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace cowsins
{
    // Add a rigidbody if needed, PlayerMovement.cs requires a rigidbody to work 
    [RequireComponent(typeof(Rigidbody))]
    //[RequireComponent(typeof(____))] Player Movement also requires a non trigger collider. Attach your preffered collider method
    /// <summary>
    /// 玩家移动组件：初始化并协调所有移动行为，维护移动上下文，并实现移动状态/事件接口。
    /// </summary>
    public class PlayerMovement : MonoBehaviour, IPlayerMovementStateProvider, IPlayerMovementEventsProvider
    {
        #region Settings
        // 移动事件集合（懒初始化）。
        public PlayerMovementEvents Events { get; private set; } = new PlayerMovementEvents();

        // 移动相关设置对象（速度、体力、视野等）。
        public PlayerMovementSettings playerSettings = new PlayerMovementSettings();

        #endregion

        #region IPlayerMovementProvider

        // We need to satisfy the required interfaces
        // 玩家朝向（位置 + 旋转），读写委托给私有字段 orientation。
        public PlayerOrientation Orientation { get { return orientation; } set { orientation = value; } }
        // 是否空闲：水平速度（忽略 Y 轴）小于 0.1 时视为空闲。
        public bool IsIdle => new Vector3(rb.linearVelocity.x, 0, rb.linearVelocity.z).magnitude < .1f;
        // 当前速度。
        public float CurrentSpeed { get; set; }
        // 奔跑速度（来自设置）。
        public float RunSpeed => playerSettings.runSpeed;
        // 走路速度（来自设置）。
        public float WalkSpeed => playerSettings.walkSpeed;
        // 蹲伏速度（来自设置）。
        public float CrouchSpeed => playerSettings.crouchSpeed;
        // 是否着地。
        public bool Grounded { get; set; }
        // 是否蹲伏。
        public bool IsCrouching { get; set; }
        // 是否滑铲。
        public bool IsSliding { get; set; }  
        // 是否攀爬。
        public bool IsClimbing { get; set; }
        // 是否墙跑。
        public bool IsWallRunning { get; set; }
        // 是否冲刺。
        public bool IsDashing { get; set; }
        // 冲刺期间是否可射击。
        public bool CanShootWhileDashing => playerSettings.canShootWhileDashing;
        // 冲刺期间是否有伤害保护。
        public bool DamageProtectionWhileDashing => playerSettings.damageProtectionWhileDashing;
        // 正常视野角度。
        public float NormalFOV => playerSettings.normalFOV;
        // 视野渐变增量。
        public float FadeFOVAmount => playerSettings.fadeFOVAmount;
        // 墙跑视野角度。
        public float WallRunningFOV => playerSettings.wallrunningFOV;
        // 奔跑视野角度。
        public float RunningFOV => playerSettings.runningFOV;
        // 冲刺是否采用切换模式。
        public bool AlternateSprint => playerSettings.alternateSprint;
        // 蹲伏是否采用切换模式。
        public bool AlternateCrouch => playerSettings.alternateCrouch;

        // 内部保存的玩家朝向（默认位置为原点、旋转为单位四元数）。
        private PlayerOrientation orientation = new PlayerOrientation(Vector3.zero, Quaternion.identity);

        #endregion

        #region Internal References

        // 玩家依赖组件集合。
        private PlayerDependencies playerDependencies;
        // 玩家乘数（影响移动/速度等）。
        private PlayerMultipliers playerMultipliers;
        // 刚体引用。
        private Rigidbody rb;
        // 胶囊碰撞体引用。
        private CapsuleCollider playerCapsuleCollider;
        // 玩家状态机。
        private PlayerStates playerStates;
        // 输入管理器引用。
        private InputManager inputManager;

        #endregion

        #region Others

        // 额外施加的下落重力（让下落手感更“实”）。
        private const float extraGravityForce = 30.19f;
        // 是否在场景视图显示胶囊体地面检测的可视化调试信息。
        public bool showCapsuleGroundCheckDebugInfo = false;

        #endregion

        #region Behaviours
        // 移动上下文（打包所有移动所需引用与状态）。
        public MovementContext movementContext { get; private set; }
        // 体力行为。
        public StaminaBehaviour staminaBehaviour { get; private set; }
        // 地面检测行为。
        public GroundDetectionBehaviour groundDetectionBehaviour { get; private set; }
        // 速度线（冲刺特效）行为。
        public SpeedLinesBehaviour speedLinesBehaviour { get; private set; }
        // 脚步音效行为。
        public FootstepsBehaviour footstepsBehaviour { get; private set; }
        // 速度处理行为。
        public VelocityHandlerBehaviour velocityHandlerBehaviour { get; private set; }
        // 基础移动行为。
        public BasicMovementBehaviour basicMovementBehaviour { get; private set; }
        // 视角控制行为。
        public CameraLookBehaviour cameraLookBehaviour { get; private set; }
        // 跳跃行为。
        public JumpBehaviour jumpBehaviour { get; private set; }
        // 蹲伏滑铲行为。
        public CrouchSlideBehaviour crouchSlideBehaviour { get; private set; }
        // 冲刺行为。
        public DashBehaviour dashBehaviour { get; private set; }
        // 墙跳（反弹）行为。
        public WallBounceBehaviour wallBounceBehaviour { get; private set; }
        // 爬梯行为。
        public ClimbLadderBehaviour climbLadderBehaviour { get; private set; }
        // 墙跑行为。
        public WallRunBehaviour wallRunBehaviour { get; private set; }
        // 钩索行为。
        public GrapplingHookBehaviour grapplingHookBehaviour { get; private set; }

        #endregion

        #region Basic
        // 启用时订阅重生事件：重生时调用 TeleportPlayer 传送玩家。
        private void OnEnable() => Events.OnRespawn.AddListener(TeleportPlayer);

        // 禁用时退订重生事件，避免残留回调。
        private void OnDisable() => Events.OnRespawn.RemoveListener(TeleportPlayer);

        // 销毁时释放各行为持有的资源（Dispose）。
        private void OnDestroy()
        {
            cameraLookBehaviour?.Dispose();
            staminaBehaviour?.Dispose();
            jumpBehaviour?.Dispose();
            footstepsBehaviour?.Dispose();
            wallRunBehaviour?.Dispose();
            crouchSlideBehaviour?.Dispose();
            velocityHandlerBehaviour?.Dispose();
            groundDetectionBehaviour?.Dispose();
            speedLinesBehaviour?.Dispose();
        }

        // 启动时：获取依赖、配置刚体、触发生成事件、设置输入模式，并初始化所有移动行为。
        private void Start()
        {
            GetDependencies();
            ConfigureRigidbody();
            playerSettings.events.OnSpawn.Invoke();

            inputManager.SetPlayerInputModes(playerSettings);

            InitializeBehaviours();
        }

        // 每帧更新地面检测与跳跃行为。
        private void Update()
        {
            groundDetectionBehaviour?.Tick();
            jumpBehaviour?.Tick();
        }

        // 物理帧更新：施加额外重力、限制最大速度、更新体力行为。
        private void FixedUpdate()
        {
            if (playerDependencies.PlayerStats.IsDead) return;

            // Added Gravity, only if we are not climbing to prevent unvoluntary sliding
            // 仅在不攀爬且不处于斜坡时额外施加向下的重力，避免攀爬/斜坡时意外下滑。
            if (!IsClimbing && !movementContext.IsPlayerOnSlope) rb.AddForce(Vector3.down * extraGravityForce, ForceMode.Acceleration);

            // 限制速度不超过设置的最大允许速度。
            if (!rb.isKinematic && rb.linearVelocity.magnitude > playerSettings.maxSpeedAllowed) rb.linearVelocity = Vector3.ClampMagnitude(rb.linearVelocity, playerSettings.maxSpeedAllowed);

            staminaBehaviour?.Tick();
        }

        // 获取脚本运行所需的全部依赖组件（刚体、状态、乘数、碰撞体、输入等），并对缺失的引用输出警告。
        /// <summary>
        /// Basically find everything the script needs to work
        /// </summary>
        private void GetDependencies()
        {
            playerDependencies = GetComponent<PlayerDependencies>();
            rb = GetComponent<Rigidbody>();
            playerStates = GetComponent<PlayerStates>();
            playerMultipliers = GetComponent<PlayerMultipliers>();
            playerCapsuleCollider = GetComponent<CapsuleCollider>();
            inputManager = playerDependencies.InputManager;

            if (playerSettings.playerCam == null) CowsinsUtilities.LogWarning("PlayerCam is null in Player > PlayerMovement > Assignables. Skipping Camera Look", this);
            if (playerSettings.cameraFOVManager == null) CowsinsUtilities.LogWarning("CameraFOVManager is null in Player > PlayerMovement > Assignables.", this);
            if (playerSettings.useSpeedLines && playerSettings.speedLines == null) 
                CowsinsUtilities.LogWarning("SpeedLines Particle Effect is null in Player > PlayerMovement > Assignables.", this);
            if (playerSettings.usesStamina && playerSettings.staminaSlider == null)
                CowsinsUtilities.LogWarning("Stamina Slider is null in Player > PlayerMovement > Stamina. Skipping Stamina UI", this);
            if (!playerSettings.usesStamina && playerSettings.staminaSlider != null)
                playerSettings.staminaSlider.gameObject.SetActive(false);
        }

        // 配置刚体：冻结旋转、使用连续动态碰撞检测与插值，并将阻力交由代码手动控制。
        private void ConfigureRigidbody()
        {
            if (rb == null) return;

            rb.freezeRotation = true;

            // Use continuous dynamic for better collision detection
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            // Set interpolation for smooth movement
            rb.interpolation = RigidbodyInterpolation.Interpolate;

            // Ensure no drag, we handle friction manually
            rb.linearDamping = 0f;
            rb.angularDamping = 0.05f;
        }

        // 构建移动上下文并实例化所有移动行为对象。
        private void InitializeBehaviours()
        {
            movementContext = new MovementContext
            {
                Transform = this.transform,
                Rigidbody = rb,
                Capsule = playerCapsuleCollider,
                Camera = playerSettings.playerCam,
                WhatIsGround = playerSettings.whatIsGround,
                InputManager = inputManager,
                Settings = playerSettings,
                Dependencies = playerDependencies,
            };

            groundDetectionBehaviour = new GroundDetectionBehaviour(movementContext);
            basicMovementBehaviour = new BasicMovementBehaviour(movementContext);
            velocityHandlerBehaviour = new VelocityHandlerBehaviour(movementContext);
            cameraLookBehaviour = new CameraLookBehaviour(movementContext);
            jumpBehaviour = new JumpBehaviour(movementContext);
            crouchSlideBehaviour = new CrouchSlideBehaviour(movementContext);
            dashBehaviour = new DashBehaviour(movementContext);
            wallBounceBehaviour = new WallBounceBehaviour(movementContext);
            climbLadderBehaviour = new ClimbLadderBehaviour(movementContext);
            wallRunBehaviour = new WallRunBehaviour(movementContext);
            grapplingHookBehaviour = new GrapplingHookBehaviour(movementContext);
            staminaBehaviour = new StaminaBehaviour(movementContext);
            footstepsBehaviour = new FootstepsBehaviour(movementContext);
            speedLinesBehaviour = new SpeedLinesBehaviour(movementContext);
        }
        #endregion

        #region Collisions
        // 碰撞进入回调：忽略与武器物体的物理碰撞，避免武器与玩家胶囊体相互阻挡。
        void OnCollisionEnter(Collision collision)
        {
            if (collision.gameObject.CompareTag("Weapons"))
            {
                Physics.IgnoreCollision(collision.collider, playerCapsuleCollider);
            }
        }

        #endregion

        #region Utils

        // 将玩家传送到指定位置与朝向：设置刚体位置与相机旋转，可选重置体力与冲刺次数，最后恢复默认状态。
        /// <summary>
        /// Teleport the player to the specified position and rotation.
        /// </summary>
        /// <param name="position"></param>
        /// <param name="rotation"></param>
        public void TeleportPlayer(Vector3 position, Quaternion rotation, bool resetStamina, bool resetDashes)
        {
            rb.position = position;
            playerSettings.playerCam.rotation = rotation;

            if(resetStamina) staminaBehaviour?.ResetStamina();
            if(resetDashes) dashBehaviour?.ResetDashes();

            playerStates.ForceChangeState(playerStates._States.Default());
        }
        #endregion

#if UNITY_EDITOR
        // 在场景视图绘制胶囊体地面检测的可视化信息（可选，由 showCapsuleGroundCheckDebugInfo 控制）。
        private void OnDrawGizmos()
        {
            if (playerCapsuleCollider == null)
            {
                if (!TryGetComponent<CapsuleCollider>(out playerCapsuleCollider))
                    return;
            }

            // 根据胶囊碰撞体的中心、高度与半径计算用于地面检测的胶囊体两端点与半径。
            Vector3 center = playerCapsuleCollider.transform.TransformPoint(playerCapsuleCollider.center);
            float halfHeight = Mathf.Max(0, (playerCapsuleCollider.height * 0.5f) - playerCapsuleCollider.radius);
            float radius = playerCapsuleCollider.radius * 0.95f;

            Vector3 bottom = center - Vector3.up * halfHeight;
            Vector3 top = center + Vector3.up * halfHeight;

            // 将胶囊体沿地面检测距离向下偏移，得到实际检测的胶囊体范围。
            Vector3 castOffset = Vector3.down * playerSettings.groundCheckDistance;
            Vector3 bottomCast = bottom + castOffset;
            Vector3 topCast = top + castOffset;

            if(showCapsuleGroundCheckDebugInfo)
                DrawCapsule(bottomCast, topCast, radius, new Color(0f, 0f, 1f, 0.3f));
        }

        // 用 Gizmos 绘制一个胶囊体（上下两个球 + 四条侧棱），用于显示地面检测范围。
        private void DrawCapsule(Vector3 bottom, Vector3 top, float radius, Color color)
        {
            Gizmos.color = color;

            Gizmos.DrawWireSphere(bottom, radius);
            Gizmos.DrawWireSphere(top, radius);

            Gizmos.DrawLine(bottom + Vector3.forward * radius, top + Vector3.forward * radius);
            Gizmos.DrawLine(bottom - Vector3.forward * radius, top - Vector3.forward * radius);
            Gizmos.DrawLine(bottom + Vector3.right * radius, top + Vector3.right * radius);
            Gizmos.DrawLine(bottom - Vector3.right * radius, top - Vector3.right * radius);
        }

#endif
    }
}