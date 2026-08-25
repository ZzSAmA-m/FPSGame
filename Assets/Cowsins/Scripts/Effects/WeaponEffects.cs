// ============================================================
// WeaponEffects.cs —— 武器视效系统：行走摆动与跳跃 / 落地动效
// 负责在武器挂载点（weaponEffectsTransform）上叠加两类程序化视效：
// 1) 行走 / 奔跑摆动（Bobbing）：基于输入与移动速度的正余弦位移和旋转；
// 2) 跳跃 / 落地动效（Jump Motion）：按 AnimationCurve 采样产生竖直位移与俯仰旋转。
// 两类效果分别经平滑插值后叠加，最终写入挂载点的本地位置与本地旋转。
// 文件末尾附带仅在编辑器下生效的自定义 Inspector 绘制代码。
// ============================================================
/// <summary>
/// This script belongs to cowsins� as a part of the cowsins� FPS Engine. All rights reserved. 
/// </summary>
using UnityEngine;
using System.Collections;
#if UNITY_EDITOR
using UnityEditor;
#endif
namespace cowsins
{
    /// <summary>武器视效组件：将行走摆动与跳跃/落地动效叠加到武器挂载点，营造逼真的武器动态。</summary>
    public class WeaponEffects : MonoBehaviour
    {
        #region Shared References
        // 武器视效挂载点：所有摆动 / 跳跃偏移最终作用于该 Transform 的本地变换。
        [SerializeField] private Transform weaponEffectsTransform;

        // 玩家控制状态提供者：用于判断玩家当前是否可控（如死亡 / 菜单状态下跳过视效）。
        private IPlayerControlProvider playerControlProvider; // IPlayerStatsProvider is implemented in PlayerStats.cs
        // 玩家移动状态提供者：获取是否落地、是否墙跑、当前速度等移动状态。
        private IPlayerMovementStateProvider playerMovementProvider; // IPlayerMovementStateProvider is implemented in PlayerMovement.cs
        // 武器行为提供者：获取是否正在瞄准等武器状态。
        private IWeaponBehaviourProvider weaponController; // IWeaponBehaviourProvider is implemented in WeaponController.cs
        // 玩家刚体：用于读取移动速度以驱动摆动相位递增。
        private Rigidbody rb;
        // 输入管理器：读取 X / Y 移动输入，用于计算摆动与倾斜。
        private InputManager inputManager;

        // 挂载点初始本地位置，作为所有位移偏移叠加的基准。
        private Vector3 basePosition;
        // 挂载点初始本地旋转，作为所有旋转偏移叠加的基准。
        private Quaternion baseRotation;
        // 经过平滑插值后的当前摆动位移 / 当前跳跃位移。
        private Vector3 currentBobPos, currentJumpPos;
        // 经过平滑插值后的当前摆动旋转 / 当前跳跃旋转。
        private Quaternion currentBobRot, currentJumpRot;
        #endregion

        #region Bob Settings
        [Header("Bobbing")]
        // 摆动旋转幅度乘数（X/Y/Z 三轴），缩放各轴的摆动旋转角度。
        [SerializeField] private Vector3 rotationMultiplier;
        // 摆动位移的平滑插值速度。
        [SerializeField] private float translationSpeed;
        // 摆动旋转的平滑插值速度。
        [SerializeField] private float rotationSpeed;
        // 基于移动输入产生的位移上限（各轴限制）。
        [SerializeField] private Vector3 movementLimit;
        // 基于正余弦波形产生的摆动幅度上限（各轴限制）。
        [SerializeField] private Vector3 bobLimit;
        // 瞄准状态下摆动位移 / 旋转的缩放倍率。
        [SerializeField] private float aimingMultiplier = 1f;
        // 水平移动输入的倾斜（侧倾）幅度乘数。
        [SerializeField] private float horizontalInclineMultiplier = 1f;
        // 前后移动输入的倾斜（俯仰）幅度乘数。
        [SerializeField] private float forwardInclineMultiplier = 1f;

        // 摆动相位累计值：随时间递增，用于驱动正弦 / 余弦波形。
        private float bobSpeed;
        // 计算出的目标摆动位移（未经平滑插值）。
        private Vector3 bobPos;
        // 计算出的目标摆动旋转（未经平滑插值，以欧拉角表示）。
        private Vector3 bobRot;
        #endregion

        #region Jump Motion Settings
        [Header("Jump Motion")]
        // 起跳动效曲线：横轴 0~1 为采样进度，纵轴为动效强度。
        [SerializeField] private AnimationCurve jumpMotion;
        // 落地动效曲线：横轴 0~1 为采样进度，纵轴为动效强度。
        [SerializeField] private AnimationCurve groundedMotion;
        // 跳跃位移的平滑插值速度。
        [SerializeField] private float jumpTranslationSpeed;
        // 跳跃旋转的平滑插值速度。
        [SerializeField] private float jumpRotationSpeed;
        // 跳跃动效的最大竖直位移距离。
        [SerializeField] private float jumpMotionDistance = 1f;
        // 跳跃动效的最大旋转角度（绕 X 轴俯仰）。
        [SerializeField] private float jumpMotionRotationAmount = 10f;
        // 动效曲线采样进度的递增速度（最小为 1，控制跳跃 / 落地动效播放快慢）。
        [SerializeField, Min(1)] private float evaluationSpeed = 5f;

        // 当前播放跳跃 / 落地动效的协程引用，用于在切换时停止旧协程。
        private Coroutine jumpCoroutine;
        // 跳跃动效产生的位移偏移（由协程逐帧更新）。
        private Vector3 jumpOffsetPosition;
        // 跳跃动效产生的旋转偏移（由协程逐帧更新）。
        private Quaternion jumpOffsetRotation;
        #endregion

        // 初始化：记录挂载点初始本地位置 / 旋转，并将各类当前位移 / 旋转归零。
        private void Start()
        {
            basePosition = weaponEffectsTransform.localPosition;
            baseRotation = weaponEffectsTransform.localRotation;

            currentBobPos = Vector3.zero;
            currentJumpPos = Vector3.zero;
            currentBobRot = Quaternion.identity;
            currentJumpRot = Quaternion.identity;
        }

        /// <summary>注入玩家依赖（控制、移动状态、武器行为、刚体、输入），并订阅跳跃与落地事件。</summary>
        /// <param name="playerDependencies">聚合玩家各系统引用的依赖容器。</param>
        public void Initialize(PlayerDependencies playerDependencies)
        {
            playerControlProvider = playerDependencies.PlayerControl;
            playerMovementProvider = playerDependencies.PlayerMovementState;
            weaponController = playerDependencies.WeaponBehaviour;
            rb = GetComponent<Rigidbody>();
            inputManager = playerDependencies.InputManager;

            playerDependencies.PlayerMovementEvents.Events.OnJump.AddListener(OnJump);
            playerDependencies.PlayerMovementEvents.Events.OnLand.AddListener(OnLand);
        }

        // 跳跃事件回调：停止旧协程，重置跳跃偏移，然后播放起跳动效。
        private void OnJump()
        {
            if (jumpCoroutine != null) StopCoroutine(jumpCoroutine);
            ResetJumpMotion();
            jumpCoroutine = StartCoroutine(ApplyJumpMotion(jumpMotion));
        }

        // 落地事件回调：停止旧协程，重置跳跃偏移，然后播放落地动效。
        private void OnLand()
        {
            if (jumpCoroutine != null) StopCoroutine(jumpCoroutine);
            ResetJumpMotion();
            jumpCoroutine = StartCoroutine(ApplyJumpMotion(groundedMotion));
        }

        // 跳跃 / 落地动效协程：从 0 到 1 按 evaluationSpeed 递增采样时间，读取 motionCurve.Evaluate(time)
        // 得到当前动效强度，据此生成竖直位移与绕 X 轴的俯仰旋转；采样结束后将偏移归零。
        // 参数 motionCurve：要采样的动效曲线（jumpMotion 或 groundedMotion）。
        private IEnumerator ApplyJumpMotion(AnimationCurve motionCurve)
        {
            float time = 0f;

            yield return null;

            while (time < 1f)
            {
                time += Time.deltaTime * evaluationSpeed;
                float motion = motionCurve.Evaluate(time);

                jumpOffsetPosition = new Vector3(0, motion * jumpMotionDistance, 0);
                jumpOffsetRotation = Quaternion.Euler(motion * jumpMotionRotationAmount, 0, 0);

                yield return null;
            }

            // Reset after motion ends
            jumpOffsetPosition = Vector3.zero;
            jumpOffsetRotation = Quaternion.identity;
        }

        /// <summary>重置跳跃动效产生的位移与旋转偏移，使武器回到基准姿态。</summary>
        public void ResetJumpMotion()
        {
            jumpOffsetPosition = Vector3.zero;
            jumpOffsetRotation = Quaternion.identity;
        }

        // 每帧更新：玩家不可控时直接返回；落地或墙跑时更新摆动；最后统一叠加摆动与跳跃效果。
        private void Update()
        {
            if(!playerControlProvider.IsControllable) return;

            if (playerMovementProvider.Grounded || playerMovementProvider.IsWallRunning)
            {
                // Only bob is played when Grounded or Wallruning
                UpdateBobbing();
            }

            ApplyCombinedEffects(); // Always Apply Jump/Bob effects
        }

        // 更新摆动：累加摆动相位，根据移动输入、速度与瞄准状态计算目标摆动位移 / 旋转。
        private void UpdateBobbing()
        {
            bobSpeed += Time.deltaTime * (playerMovementProvider.Grounded ? rb.linearVelocity.magnitude / 2 : 1) + 0.01f;
            float mult = weaponController.IsAiming ? aimingMultiplier : 1;

            float sin = Mathf.Sin(bobSpeed);
            float cos = Mathf.Cos(bobSpeed);

            bobPos.x = (cos * bobLimit.x * (playerMovementProvider.Grounded || playerMovementProvider.IsWallRunning ? 1 : 0)) - (inputManager.X * movementLimit.x);
            bobPos.y = sin * bobLimit.y;

            if (playerMovementProvider.Grounded || playerMovementProvider.IsWallRunning)
            {
                bobPos.y -= rb.linearVelocity.y * movementLimit.y;
            }

            bobPos.z = -inputManager.Y * movementLimit.z;

            bobRot.x = inputManager.X != 0 ? rotationMultiplier.x * Mathf.Sin(2 * bobSpeed) : rotationMultiplier.x * Mathf.Sin(2 * bobSpeed) / 2;
            bobRot.y = inputManager.X != 0 ? rotationMultiplier.y * cos : 0;
            bobRot.z = inputManager.X != 0 ? rotationMultiplier.z * cos * inputManager.X : 0;

            if (inputManager.X != 0)
                bobRot.z += horizontalInclineMultiplier * inputManager.X;
            if (inputManager.Y != 0)
                bobRot.x += forwardInclineMultiplier * inputManager.Y;

            // Apply aiming multiplier
            bobPos *= mult;
            bobRot *= mult;
        }

        // 叠加并应用效果：分别平滑插值摆动与跳跃的位移 / 旋转，叠加后写入挂载点本地变换。
        private void ApplyCombinedEffects()
        {
            // Bob Effect
            currentBobPos = Vector3.Lerp(currentBobPos, bobPos, Time.deltaTime * translationSpeed);
            currentBobRot = Quaternion.Slerp(currentBobRot, Quaternion.Euler(bobRot), Time.deltaTime * rotationSpeed);

            // Jump Effect
            currentJumpPos = Vector3.Lerp(currentJumpPos, jumpOffsetPosition, Time.deltaTime * jumpTranslationSpeed);
            currentJumpRot = Quaternion.Slerp(currentJumpRot, jumpOffsetRotation, Time.deltaTime * jumpRotationSpeed);

            // Combine both
            Vector3 offsetPos = currentBobPos + currentJumpPos;

            Vector3 finalPos = basePosition + offsetPos;
            Quaternion finalRot = baseRotation * currentBobRot * currentJumpRot;

            weaponEffectsTransform.localPosition = finalPos;
            weaponEffectsTransform.localRotation = finalRot;
        }

    }
#if UNITY_EDITOR
    [CustomEditor(typeof(WeaponEffects))]
    /// <summary>WeaponEffects 的自定义 Inspector：分组展示共享设置、摆动设置与跳跃动效设置。</summary>
    public class WeaponEffectsEditor : Editor
    {
        /// <summary>绘制自定义 Inspector：分组显示各项参数，并提供跳转到武器预制体文件夹的按钮。</summary>
        override public void OnInspectorGUI()
        {
            serializedObject.Update();
            var myScript = target as WeaponEffects;
            
            EditorGUILayout.Space(10f);
            EditorGUILayout.LabelField("SHARED SETTINGS", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("weaponEffectsTransform"));

            EditorGUILayout.Space(10f);
            EditorGUILayout.LabelField("WEAPON BOB SETTINGS", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(serializedObject.FindProperty("rotationMultiplier"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("translationSpeed"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("rotationSpeed"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("movementLimit"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("bobLimit"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("aimingMultiplier"));

            // Add fields for the new inclination multipliers
            EditorGUILayout.PropertyField(serializedObject.FindProperty("horizontalInclineMultiplier"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("forwardInclineMultiplier"));

            EditorGUI.indentLevel--;


            EditorGUILayout.Space(10f);
            EditorGUILayout.LabelField("JUMP MOTION SETTINGS", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("jumpMotion"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("groundedMotion"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("jumpTranslationSpeed"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("jumpRotationSpeed"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("jumpMotionDistance"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("jumpMotionRotationAmount"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("evaluationSpeed"));
            EditorGUILayout.Space(5f);

            EditorGUILayout.LabelField("WEAPON SWAY SETTINGS", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Weapon Sway can be modified in each Weapon Prefab ( Root of the Weapon Prefab )", EditorStyles.helpBox);
            EditorGUILayout.Space(5f);
            if (GUILayout.Button("Go to Weapon Prefabs Folder"))
            {
                string folderPath = "Assets/Cowsins/ScriptableObjects/Weapons";
                EditorUtility.FocusProjectWindow();
                Object folder = AssetDatabase.LoadAssetAtPath<Object>(folderPath);
                if (folder != null)
                {
                    EditorGUIUtility.PingObject(folder);
                    Selection.activeObject = folder;
                    EditorUtility.FocusProjectWindow();
                }
                else
                {
                    Debug.LogError($"Folder is empty or not found: {folderPath}. Did you remove or rename the Weapons Folder?");
                }
            }
            EditorGUILayout.Space(5f);
            serializedObject.ApplyModifiedProperties();

        }
    }
#endif
}
