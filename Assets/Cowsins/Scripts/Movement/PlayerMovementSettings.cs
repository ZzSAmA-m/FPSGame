// ----------------------------------------------------------------------
// PlayerMovementSettings.cs
// 作用：集中存放玩家移动系统的全部可序列化配置参数（速度、下蹲、滑铲、跳跃、
//       相机、瞄准辅助、体力、跑墙、蹬墙、冲刺、钩索、攀爬、脚步音效等），
//       并声明相关枚举、声音与事件等辅助类型。这些数据由 PlayerMovement 在运行时读取使用。
// ----------------------------------------------------------------------
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace cowsins
{
    [System.Serializable]
    /// <summary>
    /// 玩家移动设置数据类。将所有与移动相关的可调参数、枚举、声音与事件集中组织，
    /// 以 [System.Serializable] 形式暴露给 Inspector 编辑，并由 PlayerMovement 在运行时读取。
    /// </summary>
    public class PlayerMovementSettings
    {
        [System.Serializable]
        /// <summary>
        /// 移动事件配置内部类：集中声明移动系统各状态对应的一组 UnityEvent，供外部订阅。
        /// </summary>
        public class Events
        {
            // 移动系统相关的一组事件：待机/移动/冲刺/落地/出生/下蹲/滑铲/跳跃/跑墙/蹬墙/
            // 冲刺/钩索/攀爬等状态的开始、进行与结束事件。
            public UnityEvent OnIdle, OnMoving, OnSprint, OnLand, OnSpawn, OnCrouch, OnStopCrouch, OnSlideStart, OnJump, OnStartWallRun, OnStopWallRun, OnWallBounce,
                OnStartDash, OnDashing, OnEndDash, OnStartGrapple, OnGrappling, OnGrappleImpacted, OnStopGrapple, OnGrappleEnabled, OnStartClimb, OnClimbing, OnEndClimb;
        }

        [System.Serializable]
        /// <summary>
        /// 定向跳跃方式枚举：决定跳跃时“定向跳跃”的方向如何确定。
        /// None = 无定向跳跃；InputBased = 按玩家输入方向；ForwardMovement = 按角色前进方向。
        /// </summary>
        public enum DirectionalJumpMethod // Different methods to determine the jump method to apply
        {
            // None=无；InputBased=基于输入方向；ForwardMovement=基于前进方向。
            None, InputBased, ForwardMovement
        }

        [System.Serializable]
        /// <summary>
        /// 跑墙结束方式枚举：决定跑墙何时停止。None = 不限时（手动/其他条件结束）；Timer = 按时长结束。
        /// </summary>
        public enum CancelWallRunMethod // Different methods to determine when wallrun should stop
        {
            // None=无时限；Timer=按 wallRunDuration 定时结束。
            None, Timer
        }

        [System.Serializable]
        /// <summary>
        /// 冲刺方式枚举：决定冲刺方向如何确定。
        /// ForwardAlways = 始终向前；InputBased = 按输入方向；Free = 自由方向。
        /// </summary>
        public enum DashMethod // Different methods to determine the jump method to apply
        {
            // ForwardAlways=始终向前；InputBased=基于输入方向；Free=自由方向。
            ForwardAlways, InputBased, Free
        }

        [System.Serializable]
        /// <summary>
        /// 钩索行为方式枚举：Linear = 直线拉向锚点；Swing = 物理摆荡；Combined = 两者结合。
        /// </summary>
        public enum GrapplingHookMethod
        {
            // Linear=直线拉拽；Swing=摆荡；Combined=拉拽+摆荡结合。
            Linear, Swing, Combined
        }

        /// <summary>
        /// 爬梯移动模式枚举：决定玩家在梯子上的上下移动如何受输入与视角影响。
        /// WSOnly = 仅 W/S 上下；LookBased = 按视角方向；Combined = 两者都支持。
        /// </summary>
        public enum LadderMovementMode
        {
            WSOnly,        // W -> up, S -> down
            LookBased,     // W -> up if looking up, down if looking down
            Combined       // Both behaviors allowed: W/S + Look direction
        }

        [System.Serializable]
        /// <summary>
        /// 移动相关音效配置内部类：集中存放跳跃、落地、钩索、下蹲、滑铲、冲刺等动作的音效片段。
        /// </summary>
        public class Sounds
        {
            // 各动作对应的音效片段。
            public AudioClip jumpSFX, landSFX, startGrappleSFX, grappleImpactSFX, startCrouchSFX, stopCrouchSFX, slideSFX, dashSFX;
        }

        [System.Serializable]
        /// <summary>
        /// 单条地面脚步音效条目：将某个地面图层与其对应的脚步音效集合关联起来。
        /// </summary>
        public class FootstepSoundEntry
        {
            // 该条目对应的地面图层名称。
            public string layerName;
            // 该地面图层对应的脚步音效集合。
            public AudioClip[] sounds;

            // 缓存的图层索引（-1 表示未缓存），用于运行时快速匹配，Inspector 中隐藏。
            [HideInInspector]
            public int cachedLayerIndex = -1;
        }

        [System.Serializable]
        /// <summary>
        /// 脚步音效集合：包含默认脚步音效，以及按地面图层区分的多种表面音效条目。
        /// </summary>
        public class FootStepsSounds
        {
            // 默认脚步音效（未匹配到特定表面时使用）。
            public AudioClip[] defaultStep;

            // 按图层区分的表面脚步音效列表。
            public List<FootstepSoundEntry> surfaceSounds = new List<FootstepSoundEntry>();
        }


        // ==================== 可分配引用（ASSIGNABLES） ====================
        // ---------------- ASSIGNABLES ---------------- //

        [Tooltip("This is where the parent of your camera should be attached.")] public Transform playerCam;
        [Tooltip("Handles the field of view of the camera.")] public CameraFOVManager cameraFOVManager;
        [Tooltip("Displays speed lines effects at certain speed")] public bool useSpeedLines;
        [Tooltip("Speed lines particle system.")] public ParticleSystem speedLines;
        [Min(0), Tooltip("Speed Lines will only be shown above this speed.")] public float minSpeedToUseSpeedLines;
        [Range(.1f, 2), Tooltip("Being 1 = default, amount of speed lines displayed.")] public float speedLinesAmount = 1;


        // ==================== 基础移动（MOVEMENT） ====================
        // ---------------- MOVEMENT ---------------- //

        [Tooltip("Enable this to instantly run without needing to press the sprint button down.")] public bool autoRun;

        [Tooltip("If false, hold to sprint, and release to stop sprinting.")] public bool alternateSprint;

        [Tooltip("If false, hold to crouch, and release to uncrouch.")] public bool alternateCrouch;

        [Tooltip("If true: Speed while running backwards = runSpeed." +
        "       if false: Speed while running backwards = walkSpeed")]
        public bool canRunBackwards;

        [Tooltip("If true: Speed while running sideways = runSpeed." +
        "       if false: Speed while running sideways = walkSpeed")]
        public bool canRunSideways;

        [Tooltip("If true: Speed while shooting = runSpeed." +
       "       if false: Speed while shooting = walkSpeed")]
        public bool canRunWhileShooting;

        [Tooltip("Player deceleration from running speed to walking")] public float loseSpeedDeceleration;

        [Tooltip("Capacity to gain speed.")] public float acceleration = 4500;

        // 奔跑速度与行走速度（米/秒，最小值 0.01）。
        [Min(0.01f)] public float runSpeed, walkSpeed;

        [Min(0.01f), Tooltip("Max speed the player can reach. Velocity is clamped by this value.")] public float maxSpeedAllowed = 40;

        [Tooltip("How much control you own while you are not grounded. Being 0 = no control of it, 1 = Full control.")]
        [Range(0, 1)] public float controlAirborne = .5f;

        [Tooltip("Every object with this layer will be detected as ground, so you will be able to walk on it")] public LayerMask whatIsGround;

        [Tooltip("Distance from the bottom of the player to detect ground"), Min(0)] public float groundCheckDistance;

        [Range(0, 1f), Tooltip("Previously named Friction Force Amount (<1.3.6). Controls the snappiness of the character. The higher, the more responsive.")]
        public float controlsResponsiveness = 0.175f;

        // ==================== 下蹲设置（CROUCH） ====================
        // ---------------- CROUCH SETTINGS ---------------- //

        [Tooltip("Set to true to enable the player to crouch")] public bool allowCrouch;
        [Min(0.01f)] public float crouchSpeed;
        [Min(0.01f)] public float crouchTransitionSpeed;
        [Tooltip("Distance to detect a roof. If an obstacle is detected within this distance, the player will not be able to uncrouch")] public float roofCheckDistance = 3.5f;
        [Tooltip("Turn this on to allow the player to crouch while jumping")] public bool allowCrouchWhileJumping;


        // ==================== 滑铲设置（SLIDING） ====================
        // ---------------- SLIDING SETTINGS ---------------- //

        [Tooltip("When true, player will be allowed to slide.")] public bool allowSliding;

        [Tooltip("Force added on sliding.")] public float slideForce = 400;

        [Tooltip("If true, the player will be able to move while sliding.")] public bool allowMoveWhileSliding;

        [Tooltip("If false, the player slides without friction until the slide ends.")] public bool applyFrictionForceOnSliding;

        [Range(0, 1f), Tooltip("Force applied to counter movement when sliding")] public float slideFrictionForceAmount;
    [Tooltip("Duration (seconds) of an automatic slide. Slide will also end when speed drops below stop threshold.")]
    public float slideDuration = 1.0f;

    [Tooltip("Minimum horizontal speed under which the slide ends.")]
    public float slideStopSpeed = 1.5f;

    [Range(0, 1f), Tooltip("Multiplier applied to player steering (input influence) while sliding. 0 = no steering, 1 = full steering")] public float slideSteerMultiplier = 0.35f;
    [Tooltip("Duration (in seconds) over which the initial slide boost is applied. Use a small value like 0.08-0.18 for a responsive, non-teleporting start.")]
    public float slideBoostDuration = 0.12f;


        // ==================== 跳跃设置（JUMP） ====================
        // ---------------- JUMP SETTINGS ---------------- //

        [Tooltip("Enable this if your player can jump.")] public bool allowJump;

        [Tooltip("Amount of jumps you can do without touching the ground"), Min(1)] public int maxJumps;

        [Tooltip("Gains jump amounts when wallrunning.")] public bool resetJumpsOnWallrun;

        [Tooltip("Gains jump amounts when wallrunning.")] public bool resetJumpsOnWallBounce;

        [Tooltip("Gains jump amounts when grapple starts.")] public bool resetJumpsOnGrapple;

        [Tooltip("Double jump will reset fall damage, only if your player controller is optable to take fall damage")] public bool doubleJumpResetsFallDamage;

        [Tooltip("Method to apply on jumping when the player is not grounded, related to the directional jump")] public DirectionalJumpMethod directionalJumpMethod;

        [Tooltip("Force applied on an object in the direction of the directional jump")] public float directionalJumpForce;

        [Tooltip("The higher this value is, the higher you will get to jump.")] public float jumpForce = 550f;

        [Tooltip(" Allow the player to jump mid-air")] public bool canJumpWhileCrouching;

        [Tooltip("Interval between jumping"), Min(.25f)] public float jumpCooldown = .25f;

        // 是否启用“土狼时间”（Coyote Time，离开平台后仍可短暂起跳）。
        public bool canCoyote;

        [Range(0, .3f), Tooltip("Coyote jump allows users to perform more satisfactory and responsive jumps, especially when jumping off surfaces")] public float coyoteJumpTime;


        // ==================== 相机设置（CAMERA） ====================
        // ---------------- CAMERA SETTINGS ---------------- //

        [Tooltip("Maximum Vertical Angle for the camera"), Range(20, 89.7f)] public float maxCameraAngle = 89.7f;

        [Tooltip("Horizontal sensitivity (X Axis)")] public float sensitivityX = 4;

        [Tooltip("Vertical sensitivity (Y Axis)")] public float sensitivityY = 4;

        // 是否反转鼠标 Y 轴视角。
        public bool invertYSensitivty = false;

        [Tooltip("Horizontal sensitivity (X Axis) using controllers")] public float controllerSensitivityX = 35;

        [Tooltip("Vertical sensitivity (Y Axis) using controllers")] public float controllerSensitivityY = 35;

        // 是否反转手柄 Y 轴视角。
        public bool invertYControllerSensitivty = false;

        [Range(.1f, 1f), Tooltip("Sensitivity will be multiplied by this value when aiming")] public float aimingSensitivityMultiplier = .4f;

        [Tooltip("Default field of view of your camera"), Range(1, 179)] public float normalFOV;

        [Tooltip("Running field of view of your camera"), Range(1, 179)] public float runningFOV;

        [Tooltip("Wallrunning field of view of your camera"), Range(1, 179)] public float wallrunningFOV;

        [Tooltip("Amount of field of view that will be added to your camera when dashing."), Range(-179, 179)] public float fovToAddOnDash;

        [Tooltip("Fade Speed - Start Transition for the field of view")] public float fadeFOVAmount;

        [Range(-30, 30), Tooltip("Rotation of the camera when sliding. The rotation direction is defined by the sign of the value.")]
        public float slidingCameraTiltAmount;

        [Range(0, 30), Tooltip("Rotation of the camera when wall running. The rotation direction gets automatically adjusted by FPS Engine.")]
        public float wallrunCameraTiltAmount;

        // 攀爬时是否允许垂直视角观察。
        public bool allowVerticalLookWhileClimbing = true;

        [Tooltip("Speed of the tilt camera movement. This is essentially used for wall running")] public float cameraTiltTransitionSpeed;

        // ==================== 瞄准辅助设置（AIM ASSIST） ====================
        // ---------------- AIM ASSIST SETTINGS ---------------- //

        [Tooltip("Determine wether to apply aim assist or not.")] public bool applyAimAssist;
        [Min(.1f)] public float maximumDistanceToAssistAim;
        [Tooltip("Snapping speed.")] public float aimAssistSpeed;
        [Tooltip("Only assist when aiming within this cone (degrees)")]public float aimAssistActivationAngle = 15f;
        [Tooltip("How long to stick to a target (in seconds)")] public float targetLockDuration = 0.3f;
        [Tooltip("Require Aiming Down Sights for aim assist")] public bool assistOnlyWhenAiming = false;
        [Tooltip("Require Weapon for aim assist")] public bool assistOnlyWithWeapons = false;
        [Tooltip("Distance based strength falloff")] public AnimationCurve aimAssistFalloffCurve = AnimationCurve.Linear(0, 1, 1, 0);
        [Tooltip("0 = center of collider, 1 = top, 0.4 = upper chest")] public float aimAssistHeightMultiplier = 0.4f;
        [Tooltip("Fallback height offset when no collider found")] public float aimAssistDefaultHeightOffset = 1.4f;


        // ==================== 体力设置（STAMINA） ====================
        // ---------------- STAMINA SETTINGS ---------------- //

        [Tooltip("You will lose stamina on performing actions when true.")] public bool usesStamina;

        [Tooltip("Minimum stamina required to being able to run again.")] public float minStaminaRequiredToRun;

        [Tooltip("Max amount of stamina.")] public float maxStamina;

        [Min(1), Tooltip("Stamina Regeneration Speed")] public float staminaRegenMultiplier;

        [Tooltip("Amount of stamina lost on jumping.")]
        public float staminaLossOnJump;

        [Tooltip("Amount of stamina lost on sliding.")]
        public float staminaLossOnSlide;

        [Tooltip("Amount of stamina lost on dashing.")]
        public float staminaLossOnDash;

        [Tooltip("Our Slider UI Object. Stamina will be shown here.")]
        public Slider staminaSlider;

        [Tooltip("If Stamina bar is filled up entirely, disable it. It will reappear as soon as you lose stamina as well")]
        public bool staminaSliderFadesOutIfFull;

        // ==================== 空中相机影响（SOURCE 风格） ====================
        // ---------------- AIR CAMERA INFLUENCE ( SOURCE LIKE ) ---------------- //
        [Tooltip("If enabled, camera look will influence movement while airborne. Recommended only for source-like movement styles")] public bool allowAirCameraInfluence = false;
        [Tooltip("Camera influence amount while airborne"), Min(0)] public float airCameraInfluenceAmount = 30;

        // ==================== 跑墙设置（WALL RUN） ====================
        // ---------------- WALL RUN SETTINGS ---------------- //

        [Tooltip("When enabled, it will allow the player to wallrun on walls")] public bool canWallRun;

        [Tooltip("Define wallrunnable wall layers. By default, this is set to the same as whatIsGround.")] public LayerMask whatIsWallRunWall;

        [Tooltip("When enabled, gravity will be applied on the player while wallrunning. If disabled, the player won�t lose height while wallrunning.")] public bool useGravity;

        [Min(0), Tooltip("Since we do not want to apply all the gravity force directly to the player, we shall define the force that will counter gravity. This force goes in the opposite direction from gravity.")]
        public float wallrunGravityCounterForce;

        [Min(0), Tooltip("Maximum speed reachable while wall running.")] public float maxWallRunSpeed;

        [Min(0), Tooltip("When wall jumping, force applied on the X axis, relative to the normal of the wall.")] public float normalWallJumpForce;

        [Min(0), Tooltip("When wall jumping, force applied on the Y axis.")] public float upwardsWallJumpForce;

        [Min(0), Tooltip("Impulse applied on the player when wall run is cancelled. This results in a more satisfactory movement. Note that this force goes in the direction of the normal of the wall the player is wall running.")]
        public float stopWallRunningImpulse;

        [Min(0), Tooltip("Minimum height above ground (in units) required to being able to start wall run. Wall run motion will be cancelled for heights below this.")]
        public float wallMinimumHeight;

        [Min(.1f), Tooltip("Duration of wall run for cancelWallRunMethod = TIMER.")] public float wallRunDuration;

        [Tooltip("Method to determine length of wallRun.")] public CancelWallRunMethod cancelWallRunMethod;


        // ==================== 蹬墙设置（WALL BOUNCE） ====================
        // ---------------- WALL BOUNCE SETTINGS ---------------- //

        [Tooltip("When enabled, it will allow the player to wall bounce on walls.")] public bool canWallBounce;

        [Tooltip("Force applied to the player on wall bouncing. Note that this force is applied on the direction of the reflection of both the player movement and the wall normal.")]
        public float wallBounceForce;

        [Tooltip("Force applied on the player on wall bouncing ( Y axis � Vertical Force ).")]
        public float wallBounceUpwardsForce;

        [Range(0.1f, 2), Tooltip("maximum Distance to detect a wall you can bounce on. This will use the same layer as wall run walls.")]
        public float oppositeWallDetectionDistance = 1;



        // ==================== 冲刺设置（DASH） ====================
        // ---------------- DASH SETTINGS ---------------- //

        [Tooltip("When enabled, it will allow the player to perform dashes.")] public bool canDash;

        [Tooltip("Method to determine how the dash will work")] public DashMethod dashMethod;

        [Tooltip("When enabled, it will allow the player to perform dashes.")] public bool infiniteDashes;

        [Min(1), Tooltip("maximum ( initial ) amount of dashes. Dashes will be regenerated up to �amountOfDashes�, you won�t be able to gain more dashes than this value, check dash Cooldown.")]
        public int amountOfDashes = 3;

        [Min(.1f), Tooltip("Time to regenerate a dash on performing a dash motion.")]
        public float dashCooldown;

        [Tooltip("When enabled, player will not receive damage.")] public bool damageProtectionWhileDashing;

        [Tooltip("force applied when dashing. Note that this is a constant force, not an impulse, so it is being applied while the dash lasts.")]
        public float dashForce;

        [Range(.1f, .5f), Tooltip("Duration of the ability ( dash ).")]
        public float dashDuration;

        [Tooltip("When enabled, it will allow the player to shoot while dashing.")] public bool canShootWhileDashing;


        // ==================== 钩索设置（GRAPPLE） ====================
        // ---------------- GRAPPLE SETTINGS ---------------- //

        [Tooltip("If true, allows the player to use a customizable grappling hook.")] public bool allowGrapple;

        [Tooltip("Maximum distance allowed for the player to begin a grapple action")] public float maxGrappleDistance = 50;

        [Tooltip("Defines the grappling hook's behavior: 'Linear' pulls the player directly to the point, while 'Swing' allows physics-based swinging.")] public GrapplingHookMethod grapplingHookMethod;

        [Tooltip("Length of the grapple rope once connected (used for swinging)")] public float grappleRopeLength = 10f;

        [Tooltip("Once the grapple has finished, time to enable it again.")] public float grappleCooldown = 1;

        [Tooltip("If the distance between the grapple end point is equal or less than this value ( in Unity units ), the grapple will break.")] public float distanceToBreakGrapple = 2;

        [Tooltip("Force applied to the player on grappling.")] public float grappleForce = 400;

        [Tooltip("Grappling Hook only: influences player movement while swinging based on camera direction.")] public float cameraInfluence;

        [Tooltip("Combined mode only: gentle pull force toward the anchor. Keep this LOW (15-30) so gravity has time to build a swing arc. High values pull you straight in with no pendulum")] public float combinedGrapplePullForce = 20f;

        [Tooltip("Combined mode only: maximum speed during the grapple swing. Raise this to allow more slingshot momentum on release")] public float combinedGrappleMaxSpeed = 55f;

        [Tooltip("Combined mode only: speed in units/second at which the rope reels in toward the anchor.")] public float combinedGrappleReelSpeed = 6f;

        [Tooltip("Combined mode only: minimum lateral velocity guaranteed at grapple attachment. Ensures the arc forms immediately even when stationary or hooking a wall to the side")] public float combinedGrappleMinSwingSpeed = 10f;

        [Tooltip("Spring Force applied to the rope on grappling.")] public float grappleSpringForce = 4.5f;

        [Tooltip("Damper applied to reduce ropes� oscillation.")] public float grappleDamper = 7f;

        [Tooltip("Time that takes the rope to draw from start to end."), Header("Grapple Hook Effects")] public float drawDuration = .2f;

        [Tooltip("Amount of vertices the rope has. The bigger this value, the better it will look, but the less performant the effect will be.")] public int ropeResolution = 20;

        [Tooltip("Size of the wave effect on grappling. This effect is based on sine.")] public float waveAmplitude = 1.4f;

        [Tooltip("Speed to decrease the wave amplitude over time.")] public float waveAmplitudeMitigation = 6;


        // ==================== 攀爬设置（CLIMB） ====================
        // ---------------- CLIMB SETTINGS ---------------- //

        [Tooltip("If true, allows the player to use a customizable grappling hook.")] public bool canClimb = true;

        // 爬梯移动模式（见 LadderMovementMode 枚举）。
        public LadderMovementMode ladderMovementMode = LadderMovementMode.Combined;

        // 检测梯子的最大距离。
        [Min(0)] public float maxLadderDetectionDistance = 1;

        // 攀爬速度。
        [Min(0)] public float climbSpeed = 15;

        // 爬到梯顶时向上施加的力。
        [Min(0)] public float topReachedUpperForce = 10;

        // 爬到梯顶时向前施加的力。
        [Min(0)] public float topReachedForwardForce = 10;

        // 攀爬时是否隐藏武器。
        public bool hideWeaponWhileClimbing = true;


        // ==================== 脚步音效设置（FOOTSTEPS） ====================
        // ---------------- FOOTSTEPS SETTINGS ---------------- //

        // 脚步音效集合（含默认音效与按图层的表面音效）。
        public FootStepsSounds footstepSounds;

        [Tooltip("Volume of the AudioSource.")] public float footstepVolume;

        [Tooltip("Play speed of the footsteps."), Range(.1f, .95f)] public float footstepSpeed;


        // ==================== 其他设置（OTHER） ====================
        // ---------------- OTHER SETTINGS ---------------- //

        // 移动相关音效配置。
        public Sounds sounds;

        // 移动相关事件配置。
        public Events events;
    }
}
