// ============================================================
// WeaponSpecificEffects.cs —— 武器摆动（Sway）与下蹲倾斜（Crouch Tilt）特效
// 提供两种可选的武器摆动方案：Simple（简单位移 + 倾斜）与 PivotBased
// （绕枢轴点旋转），根据玩家鼠标 / 手柄输入产生武器滞后摆动。
// 此外还处理下蹲时的武器倾斜动画，并在瞄准 / 收枪切换时协同处理。
// 文件末尾附带仅在编辑器下生效的自定义 Inspector 绘制代码。
// ============================================================
/// <summary>
/// This script belongs to cowsins™ as a part of the cowsins´ FPS Engine. All rights reserved. 
/// </summary>
using cowsins;
using UnityEngine;
using System.Collections;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace cowsins
{
    /// <summary>武器摆动与下蹲倾斜特效：依据输入驱动武器的位移 / 旋转摆动，并处理下蹲时的倾斜动画。</summary>
    public class WeaponSpecificEffects : MonoBehaviour
    {
        #region WeaponSway
        #region shared
        [System.Serializable]
        /// <summary>摆动实现方式枚举：Simple（简单位移 + 倾斜）或 PivotBased（绕枢轴旋转）。</summary>
        public enum SwayMethod
        {
            Simple, PivotBased
        }
        // 当前选用的摆动方式（在 Inspector 中配置）。
        public SwayMethod swayMethod;
        // 摆动方法委托：指向具体的摆动实现（SimpleSway 或 PivotSway）。
        public delegate void Sway();

        // 当前生效的摆动方法委托实例，Update 中通过它调用对应实现。
        public Sway sway;
        #endregion
        #region simple
        [Header("Position")]
        // 简单摆动（Simple）的位移强度基数。
        [SerializeField] private float amount = 0.02f;

        // 简单摆动（Simple）的最大位移上限。
        [SerializeField] private float maxAmount = 0.06f;

        // 简单摆动（Simple）位移的平滑插值速度。
        [SerializeField] private float smoothAmount = 6f;


        [Header("Tilting")]
        // 简单摆动（Simple）的倾斜（旋转）强度基数。
        [SerializeField] private float tiltAmount = 4f;

        // 简单摆动（Simple）的最大倾斜角度上限。
        [SerializeField] private float maxTiltAmount = 5f;

        // 简单摆动（Simple）倾斜的平滑插值速度。
        [SerializeField] private float smoothTiltAmount = 12f;

        // 简单摆动模式下记录的初始本地位置基准。
        private Vector3 initialPosition;

        // 简单摆动模式下记录的初始本地旋转基准。
        private Quaternion initialRotation;

        // 计算后的水平输入（鼠标 + 手柄综合），用于驱动摆动。
        private float InputX;

        // 计算后的垂直输入（鼠标 + 手柄综合），用于驱动摆动。
        private float InputY;

        // 玩家状态倍率：瞄准时为 5，用于削弱瞄准状态下的摆动幅度。
        private float playerMultiplier;
        #endregion
        #region pivotBased
        // 枢轴点：PivotBased 摆动模式下武器绕其旋转的参考点。
        [SerializeField] private Transform pivot;

        // PivotBased 摆动的平滑速度。
        [SerializeField] private float swaySpeed;

        // PivotBased 摆动在 X/Y 方向的位移幅度。
        [SerializeField] private Vector2 swayMovementAmount;

        // PivotBased 摆动在水平 / 垂直方向的旋转幅度。
        [SerializeField] private Vector2 swayRotationAmount;

        // PivotBased 摆动的倾斜（翻滚）幅度。
        [SerializeField] private float swayTiltAmount;
        #endregion
        #endregion

        #region CrouchTilt
        // 下蹲倾斜的目标旋转角与目标位置偏移。
        [SerializeField] private Vector3 tiltRot, tiltPosOffset;
        // 下蹲倾斜动画的平滑速度。
        [SerializeField] private float tiltSpeed = 8f;

        // 下蹲倾斜前的原始本地位置基准。
        private Vector3 originalLocalPos;
        // 下蹲倾斜前的原始本地旋转基准。
        private Quaternion originalLocalRot;
        // 当前下蹲倾斜动画的协程引用。
        private Coroutine tiltCoroutine;

        #endregion

        // 玩家依赖容器：聚合各系统引用，用于初始化本组件。
        private PlayerDependencies playerDependencies;
        // 玩家移动状态提供者：获取是否处于下蹲状态。
        private IPlayerMovementStateProvider playerMovement; // IPlayerMovementStateProvider is implemented in PlayerMovement.cs
        // 武器行为提供者：获取是否正在瞄准。
        private IWeaponBehaviourProvider weaponController; // IWeaponBehaviourProvider is implemented in WeaponController.cs
        // 玩家控制状态提供者：判断玩家当前是否可控。
        private IPlayerControlProvider playerControl; // IPlayerControlProvider is implemented in PlayerControl.cs
        // 输入管理器：读取鼠标与手柄输入以驱动摆动。
        private InputManager inputManager;
        // 初始化：根据摆动方式设置初始基准与委托；记录下蹲倾斜用的原始本地变换。
        private void Start()
        {
            // Sway Set-up
            if (swayMethod == SwayMethod.Simple)
            {
                initialPosition = transform.localPosition;
                initialRotation = transform.localRotation;
                sway = SimpleSway;
            }
            else
            {
                sway = PivotSway;
            }

            // Crouch Tilt Set-up
            originalLocalRot  = transform.localRotation;
            originalLocalPos = transform.localPosition;
        }

        /// <summary>注入玩家依赖并订阅下蹲、取消下蹲、收枪、瞄准开始 / 结束等事件。</summary>
        /// <param name="playerDependencies">聚合玩家各系统引用的依赖容器。</param>
        public void Initialize(PlayerDependencies playerDependencies)
        {
            playerMovement = playerDependencies.PlayerMovementState;
            weaponController = playerDependencies.WeaponBehaviour;
            playerControl = playerDependencies.PlayerControl;
            inputManager = playerDependencies.InputManager;

            playerDependencies.PlayerMovementEvents.Events.OnCrouchStart.AddListener(HandleCrouch);
            playerDependencies.PlayerMovementEvents.Events.OnCrouchStop.AddListener(HandleUnCrouch);
            playerDependencies.WeaponEvents.Events.OnUnholster.AddListener(HandleCrouchOnUnholster);
            playerDependencies.WeaponEvents.Events.OnAimStart.AddListener(OnAimStart);
            playerDependencies.WeaponEvents.Events.OnAimStop.AddListener(OnAimStop);
        }

        // 每帧更新：玩家不可控时直接返回；否则调用当前摆动方法（空安全调用）。
        private void Update()
        {
            if (!playerControl.IsControllable) return;
            sway?.Invoke();
        }
        #region Weapon Sway Methods

        // 简单摆动流程：依次计算输入、移动武器、倾斜武器。
        private void SimpleSway()
        {
            CalculateSway();
            MoveSway();
            TiltSway();
        }
        // 计算摆动输入：综合鼠标与手柄输入得到 InputX / InputY，并根据瞄准状态设置摆动倍率。
        private void CalculateSway()
        {
            InputX = -inputManager.Mousex / 10 - 2 * inputManager.Controllerx;
            InputY = -inputManager.Mousey / 10 - 2 * inputManager.Controllery;

            if (weaponController.IsAiming) playerMultiplier = 5f;
            else playerMultiplier = 1f;
        }

        // 根据输入对武器施加位移摆动：限定幅度后平滑插值到目标位置。
        private void MoveSway()
        {
            float moveX = Mathf.Clamp(InputX * amount, -maxAmount, maxAmount) / playerMultiplier;
            float moveY = Mathf.Clamp(InputY * amount, -1, 1) / playerMultiplier;

            Vector3 finalPosition = new Vector3(moveX, moveY, 0);

            transform.localPosition = Vector3.Lerp(transform.localPosition, finalPosition + initialPosition, Time.fixedDeltaTime * smoothAmount * playerMultiplier);
        }

        // 根据水平输入对武器施加倾斜旋转：限定角度后平滑插值到目标旋转。
        private void TiltSway()
        {
            float moveX = Mathf.Clamp(InputX * tiltAmount, -maxTiltAmount, maxTiltAmount) / playerMultiplier;

            Quaternion finalRotation = Quaternion.Euler(0, 0, moveX);

            transform.localRotation = Quaternion.Lerp(transform.localRotation, finalRotation * initialRotation, Time.fixedDeltaTime * smoothTiltAmount * playerMultiplier);
        }

        // 枢轴摆动流程：依次处理位置摆动与旋转摆动。
        private void PivotSway()
        {
            HandleSwayLocation();
            HandleSwayRotation();
        }
        // 处理枢轴旋转摆动：绕枢轴做水平 / 垂直旋转，并叠加倾斜（翻滚）旋转。
        private void HandleSwayRotation()
        {
            var right = Camera.main.transform.right;
            right.y = 0f;
            right.Normalize();

            // HANDLE HORIZONTAL ROTATION
            transform.RotateAround(pivot.position, new Vector3(0, 1, 0), Time.fixedDeltaTime * swayRotationAmount.x * -inputManager.Mousex);
            // HANDLE VERTICAL ROTATION
            transform.RotateAround(pivot.position, right, Time.fixedDeltaTime * swayRotationAmount.y * inputManager.Mousey);
            // HANDLE TILT ROTATION
            Quaternion swayRot = Quaternion.Lerp(transform.localRotation,
                Quaternion.Euler(new Vector3(transform.localRotation.eulerAngles.x, transform.localRotation.eulerAngles.y, inputManager.Mousex * swayTiltAmount)),
                Time.deltaTime * swaySpeed);

            swayRot = Quaternion.Lerp(transform.localRotation, Quaternion.Euler(Vector3.zero), Time.deltaTime * swaySpeed);

            transform.localRotation = swayRot;
        }

        // 处理枢轴位置摆动：根据鼠标输入计算并平滑插值武器的本地位置。
        private void HandleSwayLocation()
        {
            Vector3 finalPosition = new Vector3(-inputManager.Mousex, inputManager.Mousey, 0) / 100;
            finalPosition.x = Mathf.Clamp(finalPosition.x, -1, 1) * swayMovementAmount.x;
            finalPosition.y = Mathf.Clamp(finalPosition.y, -1, 1) * swayMovementAmount.y;

            transform.localPosition = Vector3.Lerp(transform.localPosition, finalPosition, swaySpeed * Time.deltaTime);
        }

        #endregion

        #region Crouch Tilt Methods

        // 收枪事件回调：若玩家处于下蹲状态则触发下蹲倾斜（参数仅用于匹配事件签名）。
        private void HandleCrouchOnUnholster(bool prop, bool prop2)
        {
            if (playerMovement.IsCrouching) HandleCrouch();
        }
        // 下蹲开始回调：非瞄准状态下启动下蹲倾斜动画。
        private void HandleCrouch()
        {
            if (!weaponController.IsAiming)
                StartCrouchTilt(tiltRot, originalLocalPos + tiltPosOffset);
        }

        // 取消下蹲回调：将武器平滑恢复到原始本地旋转与位置。
        private void HandleUnCrouch()
        {
            StartCrouchTilt(originalLocalRot.eulerAngles, originalLocalPos);
        }

        // 瞄准开始回调：若处于下蹲则恢复原始姿态（避免瞄准时保留下蹲倾斜）。
        private void OnAimStart(float fov)
        {
            if (playerMovement.IsCrouching)
                StartCrouchTilt(originalLocalRot.eulerAngles, originalLocalPos);
        }

        // 瞄准结束回调：若仍处于下蹲则重新应用下蹲倾斜。
        private void OnAimStop()
        {
            if (playerMovement.IsCrouching)
                StartCrouchTilt(tiltRot, originalLocalPos + tiltPosOffset);
        }

        // 启动下蹲倾斜：停止旧协程并开启新的倾斜插值协程。
        // 参数 targetRot：目标旋转（欧拉角）；targetPos：目标位置。
        private void StartCrouchTilt(Vector3 targetRot, Vector3 targetPos)
        {
            if (!gameObject.activeInHierarchy) return;

            if (tiltCoroutine != null) StopCoroutine(tiltCoroutine);
            tiltCoroutine = StartCoroutine(TiltRoutine(targetRot, targetPos));
        }

        // 下蹲倾斜协程：平滑插值武器到目标旋转 / 位置，直到角度与距离足够接近才结束。
        private IEnumerator TiltRoutine(Vector3 targetRotation, Vector3 targetPosition)
        {
            Quaternion targetQuat = Quaternion.Euler(targetRotation);

            while (Quaternion.Angle(transform.localRotation, targetQuat) > 0.1f ||
                   Vector3.Distance(transform.localPosition, targetPosition) > 0.01f)
            {
                transform.localRotation = Quaternion.Lerp(transform.localRotation, targetQuat, Time.deltaTime * tiltSpeed);
                transform.localPosition = Vector3.Lerp(transform.localPosition, targetPosition, Time.deltaTime * tiltSpeed);
                yield return null;
            }
        }


        #endregion

    }
#if UNITY_EDITOR
    [CustomEditor(typeof(WeaponSpecificEffects))]
    /// <summary>WeaponSpecificEffects 的自定义 Inspector：根据摆动方式分组显示各项参数。</summary>
    public class WeaponSpecificEffectsEditor : Editor
    {
        /// <summary>绘制自定义 Inspector：按 Simple / PivotBased 摆动与下蹲倾斜分组显示序列化属性。</summary>
        override public void OnInspectorGUI()
        {
            serializedObject.Update();
            var myScript = target as WeaponSpecificEffects;

            EditorGUILayout.LabelField("WEAPON SWAY", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("swayMethod"));
            EditorGUILayout.Space(10f);

            if (myScript.swayMethod == WeaponSpecificEffects.SwayMethod.Simple)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.LabelField("POSITION");
                EditorGUILayout.PropertyField(serializedObject.FindProperty("amount"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("maxAmount"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("smoothAmount"));
                EditorGUILayout.LabelField("ROTATION");
                EditorGUILayout.PropertyField(serializedObject.FindProperty("tiltAmount"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("maxTiltAmount"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("smoothTiltAmount"));
                EditorGUI.indentLevel--;
            }
            else
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(serializedObject.FindProperty("pivot"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("swaySpeed"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("swayMovementAmount"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("swayRotationAmount"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("swayTiltAmount"));
                EditorGUI.indentLevel--;

            }
            EditorGUILayout.Space(15f);
            EditorGUILayout.LabelField("CROUCH TILT", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("tiltRot"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("tiltPosOffset"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("tiltSpeed"));
            EditorGUILayout.Space(5f);

            serializedObject.ApplyModifiedProperties();

        }
    }
#endif

}
