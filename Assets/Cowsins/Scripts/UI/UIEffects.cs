using System.Collections;
using UnityEngine;

// ===========================================================================================
// UIEffects.cs —— HUD 摆动 / 倾斜 / 跳跃动效组件
// 职责：为玩家 HUD（如准星所在 UI）添加跟随鼠标 / 手柄的摆动（Sway）与倾斜（Tilt），
//       以及在跳跃 / 落地时播放的位移与旋转动画，让 HUD 更具动感。
// ===========================================================================================
namespace cowsins
{
    // UI 动效组件：把摆动、倾斜与跳跃动效叠加到 HUD 的本地变换（位置与旋转）上。
    /// <summary>
    /// Contains a collection of useful UI Effects, from Sway, to Tilt or Jump Motion.
    /// This component can be found on "UIEffects" Object, inside PlayerUI Prefab.
    /// </summary>
    public class UIEffects : MonoBehaviour
    {
        // amount：摆动的基础位置偏移量
        [SerializeField, Title("UI SWAY"), Header("Position")] private float amount = 0.02f;
        // maxAmount：摆动的最大位置偏移
        [SerializeField] private float maxAmount = 0.06f;
        // smoothAmount：摆动位置的平滑（插值）速度
        [SerializeField] private float smoothAmount = 6f;

        // tiltAmount：倾斜的基础角度
        [SerializeField, Header("Tilt")] private float tiltAmount = 4f;
        // maxTiltAmount：倾斜的最大角度
        [SerializeField] private float maxTiltAmount = 5f;
        // smoothTiltAmount：倾斜旋转的平滑（插值）速度
        [SerializeField] private float smoothTiltAmount = 12f;

        // jumpMotion：跳跃（起跳）动效曲线
        [SerializeField, Title("JUMP MOTION", upMargin = 10)] private AnimationCurve jumpMotion;
        // groundedMotion：落地动效曲线
        [SerializeField] private AnimationCurve groundedMotion;
        // distance：跳跃动效的位移距离
        [SerializeField] private float distance;
        // rotationAmount：跳跃动效的旋转角度
        [SerializeField] private float rotationAmount;
        // evaluationSpeed：动效曲线评估速度
        [SerializeField, Min(1)] private float evaluationSpeed;

        // 初始本地位置（动效叠加的基准）
        private Vector3 initialPosition;
        // 初始本地旋转
        private Quaternion initialRotation;

        // 当前帧的水平输入（鼠标 / 手柄）
        private float InputX;
        // 当前帧的垂直输入
        private float InputY;

        // 摆动产生的位置偏移
        private Vector3 swayPositionOffset = Vector3.zero;
        // 摆动产生的旋转偏移
        private Quaternion swayRotationOffset = Quaternion.identity;

        // 跳跃动效产生的位置偏移
        private Vector3 jumpPositionOffset = Vector3.zero;
        // 跳跃动效产生的旋转偏移
        private Quaternion jumpRotationOffset = Quaternion.identity;

        // 跳跃 / 落地动效协程引用
        private Coroutine jumpMotionCoroutine;

        private IPlayerControlProvider playerControl; // Reference to PlayerControl.cs ( IPlayerControlProvider is implemented in PlayerControl.cs )
        // 输入管理器：读取鼠标 / 手柄输入
        private InputManager inputManager;

        // 初始化：记录初始本地位置与旋转，作为后续动效叠加的基准。
        private void Start()
        {
            initialPosition = transform.localPosition;
            initialRotation = transform.localRotation;
        }

        // 玩家移动事件接口：用于订阅跳跃 / 落地事件
        private IPlayerMovementEventsProvider playerEvents;
        
        /// <summary>
        /// 初始化：从 PlayerDependencies 获取玩家控制、输入管理器与移动事件接口，
        /// 并订阅跳跃 / 落地事件以触发对应动效。
        /// </summary>
        public void Initialize(PlayerDependencies playerDependencies)
        {
            playerControl = playerDependencies.PlayerControl;
            inputManager = playerDependencies.InputManager;
            playerEvents = playerDependencies.PlayerMovementEvents;

            // Listen to PlayerMovement events when Jumping and landing to handle Jump Motion accordingly.
            playerEvents.Events.OnJump.AddListener(OnJump);
            playerEvents.Events.OnLand.AddListener(OnLand);
        }

        // 销毁时取消订阅跳跃 / 落地事件，避免悬空引用。
        private void OnDestroy()
        {
            if (playerEvents != null && playerEvents.Events != null)
            {
                playerEvents.Events.OnJump.RemoveListener(OnJump);
                playerEvents.Events.OnLand.RemoveListener(OnLand);
            }
        }

        // 每帧更新：玩家可控时计算摆动，随后统一应用组合变换。
        private void Update()
        {
            if (playerControl.IsControllable)
            {
                SimpleSway();
            }

            // 应用组合变换（初始 + 摆动 + 跳跃）
            ApplyCombinedTransform();
        }

        // 计算摆动：先读取输入，再据此更新摆动偏移。
        private void SimpleSway()
        {
            CalculateSway();
            CalculateSwayOffsets();
        }

        // 读取鼠标 / 手柄输入，计算当前帧的水平与垂直摆动输入值。
        private void CalculateSway()
        {
            InputX = -inputManager.Mousex / 10f - 2f * inputManager.Controllerx;
            InputY = -inputManager.Mousey / 10f - 2f * inputManager.Controllery;
        }

        // 根据输入计算摆动的位置与旋转偏移，先钳制范围再用插值平滑逼近目标值。
        private void CalculateSwayOffsets()
        {
            float moveX = Mathf.Clamp(InputX * amount, -maxAmount, maxAmount);
            float moveY = Mathf.Clamp(InputY * amount, -1f, 1f);
            Vector3 targetPos = new Vector3(moveX, moveY, 0f);

            swayPositionOffset = Vector3.Lerp(swayPositionOffset, targetPos, Time.deltaTime * smoothAmount);

            float tiltX = Mathf.Clamp(InputX * tiltAmount, -maxTiltAmount, maxTiltAmount);
            Quaternion targetRot = Quaternion.Euler(0f, 0f, tiltX);

            swayRotationOffset = Quaternion.Slerp(swayRotationOffset, targetRot, Time.deltaTime * smoothTiltAmount);
        }

        // 组合初始变换与摆动、跳跃偏移，并应用到本地变换上。
        private void ApplyCombinedTransform()
        {
            // 位置：初始位置 + 摆动偏移 + 跳跃偏移
            Vector3 combinedPosition = initialPosition + swayPositionOffset + jumpPositionOffset;

            // 旋转：初始旋转 × 摆动旋转 × 跳跃旋转（四元数相乘表示连续旋转）
            Quaternion combinedRotation = initialRotation * swayRotationOffset * jumpRotationOffset;

            transform.localPosition = combinedPosition;
            transform.localRotation = combinedRotation;
        }

        // 跳跃时触发：停止上一个动效协程，改为播放跳跃动效曲线。
        private void OnJump()
        {
            if (!isActiveAndEnabled) return;
            if (jumpMotionCoroutine != null) StopCoroutine(jumpMotionCoroutine);
            jumpMotionCoroutine = StartCoroutine(ApplyMotion(jumpMotion));
        }

        // 落地时触发：停止上一个动效协程，改为播放落地动效曲线。
        private void OnLand()
        {
            if (!isActiveAndEnabled) return;
            if (jumpMotionCoroutine != null) StopCoroutine(jumpMotionCoroutine);
            jumpMotionCoroutine = StartCoroutine(ApplyMotion(groundedMotion));
        }

        // 协程：按给定曲线在 0~1 间推进评估，实时更新跳跃 / 落地偏移，结束后复位偏移。
        private IEnumerator ApplyMotion(AnimationCurve motionCurve)
        {
            float motion = 0f;

            while (motion < 1f)
            {
                motion += Time.deltaTime * evaluationSpeed;
                float evaluatedMotion = motionCurve.Evaluate(motion);

                // Update the Jump Offsets
                jumpPositionOffset = new Vector3(0f, evaluatedMotion * distance, 0f);
                jumpRotationOffset = Quaternion.Euler(evaluatedMotion * rotationAmount, 0f, 0f);

                yield return null;
            }

            // Reset Jump Offsets after motion ends to avoid getting stuck
            jumpPositionOffset = Vector3.zero;
            jumpRotationOffset = Quaternion.identity;
        }
    }
}