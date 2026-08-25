// =====================================================================
// CameraEffects.cs：运行时相机效果组件，负责叠加多种第一人称视角效果。
// 包含移动倾斜、头部摆动、呼吸效果、落地抖动、射击抖动等，并在 Update 中持续更新。
// 通过 PlayerDependencies 注入玩家移动状态、控制与武器事件等依赖。
// =====================================================================
using UnityEngine;
using System.Collections;

namespace cowsins
{
    /// <summary>
    /// 相机效果组件：叠加倾斜、头部摆动、呼吸与各类相机抖动效果。
    /// </summary>
    public class CameraEffects : MonoBehaviour
    {
        // 玩家相机引用，倾斜/摆动/呼吸等效果都作用于此。
        [SerializeField, Header("SHARED REFERENCES")] private Transform playerCamera;
        // 相机抖动作用的目标 Transform（用于独立叠加抖动偏移）。
        [SerializeField] private Transform camShakeTarget;

        // 倾斜效果的插值速度。
        [SerializeField, Header("TILT")] private float tiltSpeed;
        // 倾斜效果的幅度。
        [SerializeField] private float tiltAmount;

        // 头部摆动的最大幅度。
        [SerializeField, Tooltip("Maximum Head Bob"), Header("HEAD BOB")] private float headBobAmplitude = 0.2f;
        // 头部摆动的速度（频率）。
        [SerializeField, Tooltip("Speed to reach the Maximum Head Bob ( headBobAmplitude)")] private float headBobFrequency = 2f;
        // 蹲伏时头部摆动幅度的缩放系数（0~1）。
        [SerializeField, Range(0,1)] private float headBobCrouchMultiplier;

        // 呼吸效果的最大幅度。
        [SerializeField, Tooltip("Maximum Breathing Amount"), Header("BREATHING EFFECT")] private float breathingAmplitude = 0.2f;
        // 呼吸速度（频率）。
        [SerializeField, Tooltip("Breathing Speed")] private float breathingFrequency = 2f;
        // 是否对呼吸效果附加旋转。
        [SerializeField, Tooltip("Enables Rotation for the Breathing Effect")] private bool applyBreathingRotation;




        // 落地抖动的强度。
        [SerializeField, Header("LAND CAMERA SHAKE")] private float landShakeIntensity;
        // 落地抖动的持续时间。
        [SerializeField] private float landShakeDuration;

        // Camera Shake
        // 相机抖动创伤值（0~1），值越大抖动越强。
        private float trauma;
        // 创伤值属性：读取原始值，写入时自动钳制到 [0,1]。
        public float Trauma { get { return trauma; } set { trauma = Mathf.Clamp01(value); } }

        // 抖动时间推进的乘方系数。
        private float power = 16;
        // 抖动位移幅度系数。
        private float movementAmount = 0.8f;
        // 抖动旋转幅度系数。
        private float rotationAmount = 17f;

        // 抖动在 Z 轴（深度）方向的衰减系数。
        private float traumaDepthMag = 0.6f;
        // 创伤值的衰减速度。
        private float traumaDecay = 1.3f;

        // 抖动噪声采样所用的时间计数器。
        float timeCounter = 0;

        // 落地抖动协程引用（重复触发时先停止旧协程）。
        private Coroutine landingShakeRoutine;

        // 以下引用通过 Initialize 注入，用于读取玩家状态与订阅事件。
        // 玩家移动状态提供者（读取速度、蹲伏、跳跃等状态）。
        private IPlayerMovementStateProvider player; // IPlayerMovementStateProvider is implemented in PlayerMovement.cs
        // 玩家控制提供者（判断当前是否可控）。
        private IPlayerControlProvider playerControlProvider; // IPlayerControlProvider is implemented in PlayerControl.cs
        // 武器事件提供者（订阅射击抖动事件）。
        private IWeaponEventsProvider weaponEvents; // IWeaponEventsProvider is implemented in WeaponController.cs
        // 玩家刚体引用。
        private Rigidbody rb;
        // 输入管理器引用（读取移动/跳跃输入）。
        private InputManager inputManager;

        // 相机初始位置（用于空闲时平滑复位）。
        private Vector3 origPos;
        // 相机初始旋转（用于空闲时平滑复位）。
        private Quaternion origRot;

        /// <summary>
        /// 注入玩家依赖，订阅落地与射击抖动事件，并缓存 Rigidbody 与 InputManager。
        /// </summary>
        public void Initialize(PlayerDependencies playerDependencies)
        {
            player = playerDependencies.PlayerMovementState;
            playerControlProvider = playerDependencies.PlayerControl;
            weaponEvents = playerDependencies.WeaponEvents;
            rb = GetComponent<Rigidbody>();
            this.inputManager = playerDependencies.InputManager;

            playerDependencies.PlayerMovementEvents.Events.OnLand.AddListener(LandingShake);
            weaponEvents.Events.OnShootShake.AddListener(ShootShake);
        }

        // 启用时校验相机与抖动目标引用，并记录相机的初始位置与旋转。
        private void OnEnable()
        {
            if (playerCamera == null)
            {
                CowsinsUtilities.LogErrorFormat("No <b><color=cyan>PlayerCamera</color></b> reference found in CameraEffects. " +
                    "Please assign this reference accordingly to fix this error.");
                return;
            }

            if (camShakeTarget == null)
            {
                CowsinsUtilities.LogErrorFormat("No <b><color=cyan>CamShakeTarget</color></b> reference found in CameraEffects. " +
                    "Please assign this reference accordingly to fix this error.");
                return;
            }

            origPos = playerCamera.localPosition;
            origRot = playerCamera.localRotation;
        }
        // 每帧更新：玩家可控时依次叠加倾斜、头部摆动、呼吸与相机抖动效果。
        private void Update()
        {
            if (!playerControlProvider.IsControllable || playerCamera == null || camShakeTarget == null) return;

            UpdateTilt();

            UpdateHeadBob();
            UpdateBreathing();

            HandleCamShake();
        }

        // 更新移动倾斜：玩家移动时根据输入方向对相机施加倾斜。
        private void UpdateTilt()
        {
            if (player.CurrentSpeed == 0) return;

            Quaternion rot = CalculateTilt();
            playerCamera.localRotation = Quaternion.Lerp(playerCamera.localRotation, rot, Time.deltaTime * tiltSpeed);
        }

        // 根据输入方向计算倾斜目标旋转（欧拉角）。
        private Quaternion CalculateTilt()
        {
            float x = inputManager.X;
            float y = inputManager.Y;

            Vector3 vector = new Vector3(y, 0, -x).normalized * tiltAmount;

            return Quaternion.Euler(vector);
        }

        // 更新头部摆动：移动时对相机施加正弦摆动；空闲/跳跃时平滑复位。
        private void UpdateHeadBob()
        {
            if (player.IsIdle || inputManager.Jumping)
            {

                playerCamera.localPosition = Vector3.Lerp(playerCamera.localPosition, origPos, Time.deltaTime * 2f);
                playerCamera.localRotation = Quaternion.Lerp(playerCamera.localRotation, origRot, Time.deltaTime * 2f);
                return;
            }

            float angle = Time.timeSinceLevelLoad * headBobFrequency;
            float amplitude = player.IsCrouching ? headBobAmplitude * headBobCrouchMultiplier : headBobAmplitude;
            float distanceY = amplitude * Mathf.Sin(angle) / 400f;
            float distanceX = amplitude * Mathf.Cos(angle) / 100f;








            playerCamera.position = new Vector3(playerCamera.position.x, playerCamera.position.y + distanceY, playerCamera.position.z);
            playerCamera.Rotate(distanceX, 0, 0, Space.Self);
        }

        // 更新呼吸效果：以正弦波上下移动相机，可选地附加旋转。
        private void UpdateBreathing()
        {
            float angle = Time.timeSinceLevelLoad * breathingFrequency;
            float distance = breathingAmplitude * Mathf.Sin(angle) / 400f;
            float distanceRot = breathingAmplitude * Mathf.Cos(angle) / 100f;

            playerCamera.position = new Vector3(playerCamera.position.x, playerCamera.position.y + distance, playerCamera.position.z);

            if (applyBreathingRotation)
            {
                playerCamera.Rotate(distanceRot, 0, 0, Space.Self);
            }
        }

        #region CameraShake
        // 基于 Perlin 噪声生成 [-1,1] 区间的伪随机浮点值。
        private float GetFloat(float seed) { return (Mathf.PerlinNoise(seed, timeCounter) - 0.5f) * 2f; }

        // 生成三维噪声向量（Z 轴乘以 traumaDepthMag 衰减）。
        private Vector3 GetVec3() { return new Vector3(GetFloat(1), GetFloat(10), GetFloat(100) * traumaDepthMag); }

        // 处理相机抖动：创伤值大于 0 时生成噪声位移/旋转，否则平滑复位到默认姿态。
        private void HandleCamShake()
        {
            if (Trauma > 0)
            {
                timeCounter += Time.deltaTime * Mathf.Pow(Trauma, 0.3f) * power;

                Vector3 newPos = GetVec3() * movementAmount * Trauma;
                camShakeTarget.localPosition = newPos;

                camShakeTarget.localRotation = Quaternion.Euler(newPos * rotationAmount);

                Trauma -= Time.deltaTime * traumaDecay * (Trauma + 0.3f);
            }
            else
            {
                //lerp back towards default position and rotation once shake is done
                Vector3 newPos = Vector3.Lerp(camShakeTarget.localPosition, Vector3.zero, Time.deltaTime);
                camShakeTarget.localPosition = newPos;
                camShakeTarget.localRotation = Quaternion.Euler(newPos * rotationAmount);
            }
        }

        /// <summary>
        /// 设置创伤值并覆盖抖动的各项系数（用于自定义抖动效果）。
        /// </summary>
        public void Shake(float amount, float _power, float _movementAmount, float _rotationAmount)
        {
            Trauma = amount;
            power = _power;
            movementAmount = _movementAmount;
            rotationAmount = _rotationAmount;
        }

        /// <summary>
        /// 叠加一次射击抖动：增加创伤值并使用射击预设的抖动系数。
        /// </summary>
        public void ShootShake(float amount)
        {
            Trauma += amount;
            power = 20;
            movementAmount = .8f;
            rotationAmount = 17f;
        }

        /// <summary>
        /// 叠加一次爆炸抖动：创伤增量随距离衰减，并使用更强烈的抖动系数。
        /// </summary>
        public void ExplosionShake(float distance)
        {
            Trauma += 10f / distance;
            power = 30;
            movementAmount = 1f;
            rotationAmount = 30f;
        }

        /// <summary>
        /// Triggers a vertical shake to simulate landing impact.
        /// </summary>
        public void LandingShake()
        {
            if (landingShakeRoutine != null) StopCoroutine(landingShakeRoutine);
            landingShakeRoutine = StartCoroutine(LandingShakeRoutine(landShakeIntensity, landShakeDuration));
        }

        // 落地抖动协程：产生先下后上、随指数衰减的垂直位移，并在结束时复位。
        private IEnumerator LandingShakeRoutine(float intensity, float duration)
        {
            if (playerCamera == null) yield return null;

            float elapsed = 0f;
            Vector3 startPos = playerCamera.localPosition;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float normalized = elapsed / duration;

                // Quick "down then up" bounce curve
                float curve = Mathf.Sin(normalized * Mathf.PI);
                // Exponential decay
                float decay = 1f - (normalized * normalized);
                float displacement = curve * decay * -intensity;

                // Apply displacement in world space
                Vector3 worldDisplacement = Vector3.up * displacement;
                Vector3 localDisplacement = playerCamera.parent.InverseTransformDirection(worldDisplacement);

                playerCamera.localPosition = startPos + localDisplacement;

                yield return null;
            }

            // Ensure reset
            playerCamera.localPosition = startPos;
        }

        #endregion
    }
}
