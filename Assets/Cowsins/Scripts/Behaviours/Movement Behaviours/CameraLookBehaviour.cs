// ============================================================
// CameraLookBehaviour（相机视角行为）
// 负责玩家的第一人称视角旋转：读取鼠标/手柄输入计算偏航与俯仰，
// 应用灵敏度与后坐力偏移，处理墙跑/滑铲的相机倾斜，并实现瞄准辅助。
// 通过 Tick() 每帧更新相机朝向，VerticalLook() 供攀爬等场景单独调整俯仰。
// ============================================================
using cowsins.Settings;
using UnityEngine;

namespace cowsins
{
    /// <summary>处理第一人称相机视角：输入驱动的偏航/俯仰旋转、倾斜效果与瞄准辅助。</summary>
    public class CameraLookBehaviour
    {
        // 移动上下文：聚合相机、刚体、输入与依赖等共享数据。
        private MovementContext context;
        // 输入管理器：提供鼠标/手柄的原始视角输入。
        private InputManager inputManager;
        // 玩家刚体，用于读取位置、速度等状态。
        private Rigidbody rb;
        // 相机 Transform，视角旋转直接作用于它。
        private Transform camera;
        // 玩家移动状态提供者：暴露墙跑、蹲伏、速度等运行时状态。
        private IPlayerMovementStateProvider playerMovement;
        // 移动事件提供者：用于判定滑铲等事件是否允许。
        private IPlayerMovementEventsProvider playerEvents;
        // 武器行为提供者：访问是否瞄准等武器状态。
        private IWeaponBehaviourProvider weaponBehaviourProvider;
        // 武器引用提供者：提供当前武器与主相机等引用。
        private IWeaponReferenceProvider weaponReference;
        // 武器后坐力提供者：提供后坐力对视角的偏移量。
        private IWeaponRecoilProvider weaponRecoil;
        // 移动设置：视角灵敏度、瞄准辅助等参数来源。
        private PlayerMovementSettings playerSettings;

        // 相机俯仰角（绕 X 轴），用于上下看。
        private float cameraPitch;
        // 相机偏航角（绕 Y 轴），用于左右转向。
        private float cameraYaw;
        // 相机翻滚角（绕 Z 轴），用于墙跑/滑铲的倾斜效果。
        private float cameraRoll;

        // Controls the current sensitivity.
        // Sensitivity can be overrided by the Game Settings Manager
        // 当前生效的鼠标/手柄灵敏度，优先从游戏设置管理器读取。
        private float currentSensX, currentSensY, currentControllerSensX, currentControllerSensY;
        // 鼠标与手柄的 Y 轴反转开关（分别独立）。
        private bool invertYLook, invertYLookController; 

        // 玩家朝向：根据当前偏航角同步玩家的 Forward/Right 向量。
        private PlayerOrientation orientation => playerMovement.Orientation;

        // 是否已订阅设置管理器事件，防止重复订阅。
        private bool subscribedToSettings = false;

        /// <summary>构造函数：缓存相机、刚体、输入与各依赖引用，并立即读取灵敏度设置。</summary>
        /// <param name="context">聚合相机、刚体、输入与依赖的移动上下文。</param>
        public CameraLookBehaviour(MovementContext context)
        {
            this.context = context;
            this.rb = context.Rigidbody;
            this.camera = context.Camera;

            this.inputManager = context.InputManager;

            this.playerMovement = context.Dependencies.PlayerMovementState;
            this.playerEvents = context.Dependencies.PlayerMovementEvents;
            this.weaponBehaviourProvider = context.Dependencies.WeaponBehaviour;
            this.weaponReference = context.Dependencies.WeaponReference;
            this.weaponRecoil = context.Dependencies.WeaponRecoil;
            this.playerSettings = context.Settings;

            GatherSensitivityValues();
            CheckSettingsSubscription();
        }

        // 订阅游戏设置管理器的加载/重置/应用事件，确保灵敏度等设置在变动后同步刷新。
        private void CheckSettingsSubscription()
        {
            if (!subscribedToSettings && GameSettingsManager.Instance != null)
            {
                GameSettingsManager.Instance.OnSettingsLoaded += GatherSensitivityValues;
                GameSettingsManager.Instance.OnSettingsReset += GatherSensitivityValues;
                GameSettingsManager.Instance.OnSettingsApplied += GatherSensitivityValues;
                GatherSensitivityValues(); // Fetch again now that we found the instance
                subscribedToSettings = true;
            }
        }

        /// <summary>释放资源：取消对设置管理器事件的订阅，避免悬挂引用。</summary>
        public void Dispose()
        {
            if (subscribedToSettings && GameSettingsManager.Instance != null)
            {
                GameSettingsManager.Instance.OnSettingsLoaded -= GatherSensitivityValues;
                GameSettingsManager.Instance.OnSettingsReset -= GatherSensitivityValues;
                GameSettingsManager.Instance.OnSettingsApplied -= GatherSensitivityValues;
                subscribedToSettings = false;
            }
        }

        /// <summary>每帧更新视角：读取输入计算偏航/俯仰，叠加后坐力与灵敏度，应用旋转并执行瞄准辅助。</summary>
        public void Tick()
        {
            if (camera == null) return;
            CheckSettingsSubscription();

            // 处理 Y 轴反转选项（鼠标与手柄分别独立）。
            int sensYInverted = invertYLook ? -1 : 1;
            int sensYInvertedController = invertYLookController ? 1 : -1;
            // 瞄准时按配置降低灵敏度。
            float sensitivityMultiplier = weaponBehaviourProvider.IsAiming ? playerSettings.aimingSensitivityMultiplier : 1;

            // Grab the Inputs from the user.
            // 采集原始鼠标/手柄输入（已按灵敏度换算）。
            float rawMouseX = inputManager.GatherRawMouseX(currentSensX, currentControllerSensX);
            float rawMouseY = inputManager.GatherRawMouseY(sensYInverted, sensYInvertedController, currentSensY, currentControllerSensY);
            float mouseX = rawMouseX * sensitivityMultiplier;
            float mouseY = rawMouseY * sensitivityMultiplier;

            // Calculate new yaw rotation ( around the y axis )
            // 偏航角 = 当前偏航 + 鼠标输入 + 后坐力水平偏移。
            cameraYaw = camera.rotation.eulerAngles.y + mouseX + weaponRecoil.RecoilYawOffset * Time.deltaTime;
            //Rotate Camera Pitch ( around x axis )
            // 俯仰角减去鼠标输入并叠加后坐力垂直偏移（向下为正）。
            cameraPitch -= mouseY - weaponRecoil.RecoilPitchOffset * Time.deltaTime;
            // Make sure we dont over- or under-rotate.
            // The reason why the value is 89.7 instead of 90 is to prevent errors with the wallrun
            cameraPitch = Mathf.Clamp(cameraPitch, -playerSettings.maxCameraAngle, playerSettings.maxCameraAngle);

            CalculateCameraRoll();

            ApplyCameraRotation();

            HandleAimAssist();
        }

        /// <summary>仅更新俯仰（上下看）：供攀爬等只允许垂直视角调整的场景调用。</summary>
        public void VerticalLook()
        {
            if (PauseMenu.isPaused || !playerSettings.allowVerticalLookWhileClimbing) return;
            CheckSettingsSubscription();

            // 处理 Y 轴反转选项（鼠标与手柄分别独立）。
            int sensYInverted = invertYLook ? -1 : 1;
            int sensYInvertedController = invertYLookController ? -1 : 1;
            float sensitivityMultiplier = weaponBehaviourProvider.IsAiming ? playerSettings.aimingSensitivityMultiplier : 1;

            float rawMouseY = inputManager.GatherRawMouseY(
                sensYInverted,
                sensYInvertedController,
                currentSensY,
                currentControllerSensY
            );

            float mouseY = rawMouseY * sensitivityMultiplier;

            // 直接累加俯仰变化（仅垂直视角）。
            cameraPitch -= mouseY;
            cameraPitch = Mathf.Clamp(cameraPitch, -playerSettings.maxCameraAngle, playerSettings.maxCameraAngle);

            ApplyCameraRotation();
        }

        // 把计算好的俯仰/偏航/翻滚角写入相机，并同步更新玩家的朝向状态。
        private void ApplyCameraRotation()
        {
            camera.rotation = Quaternion.Euler(cameraPitch, cameraYaw, cameraRoll);
            orientation.UpdateOrientation(context.Rigidbody.transform.position, cameraYaw);
        }


        // 计算相机翻滚角：墙跑与滑铲时向目标倾斜量插值，否则平滑归零。
        private void CalculateCameraRoll()
        {
            if (playerMovement.IsWallRunning) cameraRoll = context.WallLeft ? Mathf.Lerp(cameraRoll, -playerSettings.wallrunCameraTiltAmount, Time.deltaTime * playerSettings.cameraTiltTransitionSpeed) : Mathf.Lerp(cameraRoll, playerSettings.wallrunCameraTiltAmount, Time.deltaTime * playerSettings.cameraTiltTransitionSpeed);
            else if (playerMovement.IsCrouching && playerMovement.CurrentSpeed >= playerMovement.WalkSpeed && playerEvents.Events.InvokeAllowSlide() && !context.HasJumped) cameraRoll = Mathf.Lerp(cameraRoll, playerSettings.slidingCameraTiltAmount, Time.deltaTime * playerSettings.cameraTiltTransitionSpeed);
            else cameraRoll = Mathf.Lerp(cameraRoll, 0, Time.deltaTime * playerSettings.cameraTiltTransitionSpeed);
        }

        private Transform currentAimAssistTarget;
        private float targetLockTimer;

        // 处理瞄准辅助：锁定/寻找目标，并按距离与角度权重对相机做平滑吸附旋转。
        private void HandleAimAssist()
        {
            // Check if aim assist is enabled
            if (!playerSettings.applyAimAssist) return;

            // Only assist when having a weapon. Optional setting
            if (playerSettings.assistOnlyWithWeapons && weaponReference.Weapon == null) return;

            // Only assist when aiming down sights. Optional setting
            if (playerSettings.assistOnlyWhenAiming && !weaponBehaviourProvider.IsAiming)
            {
                currentAimAssistTarget = null;
                return;
            }

            // Handle target locking & maintain current target for smooth tracking
            Transform target = null;

            if (currentAimAssistTarget != null && targetLockTimer > 0)
            {
                // Check if current target is valid
                float distanceToCurrent = Vector3.Distance(currentAimAssistTarget.position, rb.transform.position);
                Vector3 _targetAimPoint = GetTargetAimPoint(currentAimAssistTarget);
                Vector3 directionToCurrent = (_targetAimPoint - camera.position).normalized;
                float angleToCurrent = Vector3.Angle(camera.forward, directionToCurrent);

                // Keep locked target if still in range and reasonable angle
                if (distanceToCurrent <= playerSettings.maximumDistanceToAssistAim &&
                    angleToCurrent <= playerSettings.aimAssistActivationAngle * 2f) // Allow wider angle for locked target
                {
                    target = currentAimAssistTarget;
                    targetLockTimer -= Time.deltaTime;
                }
                else
                {
                    // Lost the target, we need to find a new one
                    currentAimAssistTarget = null;
                    targetLockTimer = 0;
                }
            }

            // Find new target if we don't have a locked one just yet
            if (target == null)
            {
                target = AimAssistHit();
                if (target != null)
                {
                    currentAimAssistTarget = target;
                    targetLockTimer = playerSettings.targetLockDuration;
                }
                else
                {
                    currentAimAssistTarget = null;
                    return;
                }
            }

            // Calculate assist
            float distance = Vector3.Distance(target.position, rb.transform.position);
            Vector3 targetAimPoint = GetTargetAimPoint(target);
            Vector3 directionToTarget = (targetAimPoint - camera.position).normalized;
            float angleToTarget = Vector3.Angle(camera.forward, directionToTarget);

            // Only assist if player is aiming close to the target
            if (angleToTarget > playerSettings.aimAssistActivationAngle)
            {
                currentAimAssistTarget = null;
                return;
            }

            // Calculate assist strength with multiple falloff factors
            float distanceWeight = 1f - Mathf.Clamp01(distance / playerSettings.maximumDistanceToAssistAim);
            float angleWeight = 1f - Mathf.Clamp01(angleToTarget / playerSettings.aimAssistActivationAngle);

            // Use animation curve for more control over falloff. Falls back if no values are provided to avoid weird behaviours
            if (playerSettings.aimAssistFalloffCurve != null && playerSettings.aimAssistFalloffCurve.keys.Length > 0)
            {
                distanceWeight = playerSettings.aimAssistFalloffCurve.Evaluate(distance / playerSettings.maximumDistanceToAssistAim);
            }

            float assistStrength = distanceWeight * angleWeight;

            // Apply rotation assist
            if (assistStrength > 0.01f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(directionToTarget);

                camera.rotation = Quaternion.Slerp(
                    camera.rotation,
                    targetRotation,
                    Time.deltaTime * playerSettings.aimAssistSpeed * assistStrength
                );

                // Update yaw and pitch to match the assisted rotation
                Vector3 eulerAngles = camera.rotation.eulerAngles;
                cameraYaw = eulerAngles.y;
                cameraPitch = eulerAngles.x;

                if (cameraPitch > 180f) cameraPitch -= 360f;
            }
        }

        // 在相机前方搜索最佳辅助目标：过滤遮挡与视野外敌人，取角度+距离评分最低者。
        private Transform AimAssistHit()
        {
            float range = weaponReference.Weapon != null
                ? weaponReference.Weapon.bulletRange
                : 40f;

            // Find all potential enemy targets within range
            Collider[] nearbyEnemies = Physics.OverlapSphere(
                camera.position,
                range,
                LayerMask.GetMask("Enemy")
            );

            if (nearbyEnemies.Length == 0) return null;

            Transform bestTarget = null;
            float bestScore = float.MaxValue;

            foreach (var enemyCollider in nearbyEnemies)
            {
                // Gather the aim point from the target
                Vector3 targetPosition = GetTargetAimPoint(enemyCollider.transform);

                // Exclude Enemy layer from obstruction check to avoid hitting the target itself
                int obstructionMask = playerSettings.whatIsGround & ~LayerMask.GetMask("Enemy");
                RaycastHit obstacleHit;
                if (Physics.Linecast(camera.position, targetPosition, out obstacleHit, obstructionMask))
                    continue;

                // Skip if not visible in camera view
                Vector3 viewportPoint = Camera.main.WorldToViewportPoint(targetPosition);
                if (viewportPoint.z < 0 || viewportPoint.x < 0 || viewportPoint.x > 1 ||
                    viewportPoint.y < 0 || viewportPoint.y > 1)
                    continue;

                // Calculate direction
                Vector3 directionToEnemy = (targetPosition - camera.position).normalized;
                float angle = Vector3.Angle(camera.forward, directionToEnemy);

                // Skip enemies outside activation cone
                if (angle > playerSettings.aimAssistActivationAngle)
                    continue;

                float distance = Vector3.Distance(camera.position, targetPosition);

                // Score prioritizes enemies closer to crosshair over distance
                // Lower score is better
                // 综合角度与距离打分，分数越低越优先（更靠近准星的目标优先）。
                float score = angle + (distance * 0.05f);

                if (score < bestScore)
                {
                    bestScore = score;
                    bestTarget = enemyCollider.transform;
                }
            }

            return bestTarget;
        }

        /// <summary>
        /// Gets a proper aim point on a target
        /// </summary>
        // 获取目标身上合适的瞄准点：优先取碰撞体中心并按比例上移，否则回退到默认偏移。
        private Vector3 GetTargetAimPoint(Transform target)
        {
            // Find a collider
            if (target.TryGetComponent<Collider>(out var targetCollider))
            {
                Vector3 bounds = targetCollider.bounds.center;
                float heightOffset = targetCollider.bounds.extents.y * playerSettings.aimAssistHeightMultiplier;
                return new Vector3(bounds.x, bounds.y + heightOffset, bounds.z);
            }

            // Fallback
            return target.position + Vector3.up * playerSettings.aimAssistDefaultHeightOffset;
        }
        // 从游戏设置管理器（或回退到默认设置）读取鼠标/手柄灵敏度与 Y 轴反转选项。
        private void GatherSensitivityValues()
        {
            var inst = GameSettingsManager.Instance;
            if (inst)
            {
                currentSensX = inst.Controls.MouseSensitivityX.Value;
                currentSensY = inst.Controls.MouseSensitivityY.Value;
                currentControllerSensX = inst.Controls.ControllerSensitivityX.Value;
                currentControllerSensY = inst.Controls.ControllerSensitivityY.Value;
                invertYLook = inst.Controls.InvertY.Value;
                invertYLookController = inst.Controls.InvertYController.Value;
            }
            else
            {
                currentSensX = playerSettings.sensitivityX;
                currentSensY = playerSettings.sensitivityY;
                currentControllerSensX = playerSettings.controllerSensitivityX;
                currentControllerSensY = playerSettings.controllerSensitivityY;
                invertYLook = playerSettings.invertYSensitivty;
                invertYLookController = playerSettings.invertYControllerSensitivty;
            }
        }
    }

}