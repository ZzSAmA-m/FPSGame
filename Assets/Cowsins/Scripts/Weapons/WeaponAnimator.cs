// =============================================================================
// WeaponAnimator.cs
// 武器动画控制器：根据玩家移动/瞄准/换弹等状态，计算并平滑过渡武器 Animator 的移动
// （Locomotion）速度混合参数（0=待机 / 0.5=走路 / 1=跑步），并负责检视（Inspect）、
// 收枪/掏枪、换弹等动画的播放与切换，同时支持按武器覆盖移动过渡速度。
// =============================================================================
using System.Runtime.CompilerServices;
using UnityEngine;

namespace cowsins
{
    /// <summary>
    /// 武器动画控制器：监听玩家移动与武器事件，驱动武器 Animator 的 Locomotion 速度参数，
    /// 并播放检视、换弹、掏枪/收枪等动画。
    /// </summary>
    public class WeaponAnimator : MonoBehaviour
    {
        // 相机动画组件，用于瞄准/检视时调整相机（父约束目标）
        [SerializeField] private CameraAnimations cameraAnimations;

        // 收枪动作的动画器（Holster 动作对象），掏枪/收枪时驱动该动画
        [SerializeField] private Animator holsterMotionObject;

        [Header("Locomotion Transition Speeds")]
        // 待机(0)→走路(0.5) 的过渡速度（值越低越平滑缓慢，越高越干脆快速）
        [Tooltip("Speed of transition from Idle (0) to Walk (0.5) Lower = Smoother & Slower, Higher = Snappier & Faster")]
        [SerializeField, Range(0.1f, 10f)] private float idleToWalkSpeed;
        
        // 走路(0.5)→待机(0) 的过渡速度
        [Tooltip("Speed of transition from Walk (0.5) to Idle (0) Lower = Smoother & Slower, Higher = Snappier & Faster")]
        [SerializeField, Range(0.1f, 10f)] private float walkToIdleSpeed;
        
        // 走路(0.5)→跑步(1.0) 的过渡速度
        [Tooltip("Speed of transition from Walk (0.5) to Run (1.0) Lower = Smoother & Slower, Higher = Snappier & Faster")]
        [SerializeField, Range(0.1f, 10f)] private float walkToRunSpeed;
        
        // 跑步(1.0)→走路(0.5) 的过渡速度
        [Tooltip("Speed of transition from Run (1.0) to Walk (0.5) Lower = Smoother & Slower, Higher = Snappier & Faster")]
        [SerializeField, Range(0.1f, 10f)] private float runToWalkSpeed;

        // 对外暴露收枪动作动画器引用
        public Animator HolsterMotionObject => holsterMotionObject;

        // 玩家依赖聚合对象，用于获取玩家各模块引用
        private PlayerDependencies playerDependencies;
        // 武器状态机（管理当前武器状态）
        private WeaponStates weaponStates;
        // 玩家移动状态提供者（跑步/蹲伏/地面/墙跑等状态）
        private IPlayerMovementStateProvider player; // IPlayerMovementStateProvider is implemented in PlayerMovement.cs
        // 玩家移动事件提供者（监听攀爬等事件）
        private IPlayerMovementEventsProvider playerEvents; // IPlayerMovementEventsProvider is implemented in PlayerMovement.cs
        // 武器引用提供者（当前武器实例的标识）
        private IWeaponReferenceProvider weaponController; // IWeaponReferenceProvider is implemented in WeaponController.cs
        // 武器行为提供者（瞄准/换弹/射击等行为状态）
        private IWeaponBehaviourProvider weaponBehaviour; // IWeaponBehaviourProvider is implemented in WeaponController.cs
        // 武器事件提供者（掏枪/换弹/副攻击等事件）
        private IWeaponEventsProvider weaponEvents;// IWeaponEventsProvider is implemented in WeaponController.cs
        // 交互管理器提供者（检视等交互状态）
        private IInteractManagerProvider interactManager; // IWeaponReferenceProvider is implemented in InteractManager.cs

        // 掏枪（unholster）动画触发参数名
        private readonly string UNHOLSTER_KEY = "unholster";
        // 移动速度（speed）动画参数名，用于混合待机/走路/跑步
        private readonly string SPEED_KEY = "speed";
        // 普通换弹（reloading）动画触发参数名
        private readonly string RELOADING_KEY = "reloading";
        // 空仓换弹（emptyReloading）动画触发参数名
        private readonly string EMPTY_RELOADING_KEY = "emptyReloading";
        // 检视（inspect）动画触发参数名
        private readonly string INSPECT_KEY = "inspect";
        // 结束检视（finishedInspect）动画触发参数名
        private readonly string FINISHEDINSPECT_KEY = "finishedInspect";
        
        // 当前插值后的动画速度值（0=待机 / 0.5=走路 / 1=跑步）
        private float currentAnimatorSpeed = 0f;

        // 初始化：获取各模块引用，并订阅攀爬开始/结束与武器掏枪/副攻击/开始换弹/取消换弹事件。
        private void Start()
        {
            playerDependencies = GetComponent<PlayerDependencies>();
            weaponStates = GetComponent<WeaponStates>();
            player = playerDependencies.PlayerMovementState;
            playerEvents = playerDependencies.PlayerMovementEvents;
            weaponController = playerDependencies.WeaponReference;
            weaponBehaviour = playerDependencies.WeaponBehaviour;
            weaponEvents = playerDependencies.WeaponEvents;
            interactManager = playerDependencies.InteractManager;

            playerEvents.Events.OnClimbStart.AddListener(TryHideWeapon);
            playerEvents.Events.OnClimbStop.AddListener(TryShowWeapon);

            weaponEvents.Events.OnUnholster.AddListener(OnUnholster);
            weaponEvents.Events.OnSecondaryAttack.AddListener(SetParentConstraintSource);
            weaponEvents.Events.OnStartReload.AddListener(StartReload);
            weaponEvents.Events.OnCancelReload.AddListener(CancelReload); 
        }

        // 销毁时：移除对攀爬与武器事件的监听，避免内存泄漏。
        private void OnDestroy()
        {
            if (playerEvents != null && playerEvents.Events != null)
            {
                playerEvents.Events.OnClimbStart.RemoveListener(TryHideWeapon);
                playerEvents.Events.OnClimbStop.RemoveListener(TryShowWeapon);
            }

            if (weaponEvents != null && weaponEvents.Events != null)
            {
                weaponEvents.Events.OnUnholster.RemoveListener(OnUnholster);
                weaponEvents.Events.OnSecondaryAttack.RemoveListener(SetParentConstraintSource);
                weaponEvents.Events.OnStartReload.RemoveListener(StartReload);
                weaponEvents.Events.OnCancelReload.RemoveListener(CancelReload);
            }
        }

        // 每帧更新：若未持有武器则跳过；读取当前武器 Animator，计算目标速度并平滑过渡后写入 speed 参数。
        private void Update()
        {
            if (weaponController.Id == null) return;

            Animator currentAnimator = weaponController.Id.Animator;

            // Calculate normalized speed value where 0 = Idle, .5 = Walk, 1 = Run
            float targetSpeed = CalculateNormalizedSpeed();
            
            // Interpolate to target speed with transitions speeds
            currentAnimatorSpeed = SmoothSpeedTransition(currentAnimatorSpeed, targetSpeed);
            
            currentAnimator.SetFloat(SPEED_KEY, currentAnimatorSpeed);
        }

        // 根据玩家当前状态计算归一化速度目标值：换弹/蹲伏/空中/待机/瞄准时强制待机(0)，
        // 墙跑时按走路(0.5)，否则按当前速度阈值区分跑步(1)与走路(0.5)。
        /// <summary>
        /// Calculate normalized speed value where 0 = Idle, .5 = Walk, 1 = Run
        /// </summary>
        private float CalculateNormalizedSpeed()
        {
            // Force idle state for specific conditions
            if (weaponBehaviour.IsReloading || player.IsCrouching || (!player.Grounded && !player.IsWallRunning) ||
                player.IsIdle || weaponBehaviour.IsAiming)
            {
                return 0f; // Idle
            }

            // Wall running uses walk animation
            if (player.IsWallRunning && !weaponBehaviour.IsReloading)
            {
                return 0.5f; // Walk
            }

            // Determine speed based on CurrentSpeed thresholds
            if (player.CurrentSpeed >= player.RunSpeed && player.Grounded)
            {
                return 1f; // Run
            }
            else if (player.CurrentSpeed > player.CrouchSpeed && 
                     player.CurrentSpeed < player.RunSpeed && 
                     player.Grounded && 
                     !interactManager.Inspecting)
            {
                return 0.5f; // Walk
            }

            return 0f; // Idle
        }

        // 平滑过渡：根据当前值与目标值选择过渡速度，再用 MoveTowards 做帧间插值逼近目标。
        private float SmoothSpeedTransition(float current, float target)
        {
            // Determine transition speed
            float transitionSpeed = GetTransitionSpeed(current, target);
            
            return Mathf.MoveTowards(current, target, transitionSpeed * Time.deltaTime);
        }

        // 返回对应状态切换的过渡速度：优先使用当前武器的过渡速度覆盖值，否则用本组件的默认值。
        /// <summary>
        /// Returns the appropriate transition speed based on state
        /// </summary>
        private float GetTransitionSpeed(float current, float target)
        {
            // Check if current weapon has transition overrides
            var weapon = weaponController.Id?.weapon;
            bool hasOverride = weapon != null && weapon.locomotionTransitions.overrideTransitions;

            // Idle > Walk or Walk > Run
            if (target > current)
            {
                // If moving toward Walk from below
                if (target <= 0.5f)
                    return hasOverride ? weapon.locomotionTransitions.idleToWalkSpeed : idleToWalkSpeed;
                // If moving toward Run from below
                else
                    return hasOverride ? weapon.locomotionTransitions.walkToRunSpeed : walkToRunSpeed;
            }
            // Run > Walk or Walk > Idle
            else if (target < current)
            {
                // If moving toward Walk from above
                if (target >= 0.5f)
                    return hasOverride ? weapon.locomotionTransitions.runToWalkSpeed : runToWalkSpeed;
                // If moving toward Idle from above
                else
                    return hasOverride ? weapon.locomotionTransitions.walkToIdleSpeed : walkToIdleSpeed;
            }
            
            // No transition needed
            return 0f;
        }

        /// <summary>停止走路/跑步动作：将当前武器动画的 speed 参数重置为待机(0)。</summary>
        public void StopWalkAndRunMotion()
        {
            if (weaponController == null) return; 
            Animator weapon = weaponController.Id.Animator;
            ResetSpeed(weapon);
        }

        // 将指定 Animator 的 speed 参数重置为 0（待机），并同步清零本地插值速度缓存。
        /// <summary>
        /// Resets the speed parameter to 0 = Idle
        /// </summary>
        private void ResetSpeed(Animator animator)
        {
            if (animator == null) return;
            currentAnimatorSpeed = 0f;
            animator.SetFloat(SPEED_KEY, 0f);
        }

        /// <summary>尝试隐藏武器（攀爬开始事件回调）：当参数 hide 为 true 时执行隐藏。</summary>
        /// <param name="hide">是否隐藏（可空布尔）</param>
        public void TryHideWeapon(bool? hide)
        {
            if (hide.Value) HideWeapon();
        }
        /// <summary>尝试显示武器（攀爬结束事件回调）：当参数 show 为 true 时执行显示。</summary>
        /// <param name="show">是否显示（可空布尔）</param>
        public void TryShowWeapon(bool? show)
        {
            if (show.Value) ShowWeapon();
        }
        /// <summary>通过状态机强制将武器切换到 Hidden（隐藏）状态。</summary>
        public void HideWeapon() => weaponStates.ForceChangeState(weaponStates._States.Hidden());

        /// <summary>通过状态机强制将武器切换到 Default（默认）状态。</summary>
        public void ShowWeapon() => weaponStates.ForceChangeState(weaponStates._States.Default());

        /// <summary>设置相机父约束的目标（副攻击/检视时让相机跟随指定骨骼）。</summary>
        /// <param name="transform">要作为父约束目标的对象</param>
        public void SetParentConstraintSource(Transform transform) => cameraAnimations?.SetTarget(transform);

        // 掏枪事件回调：重新绑定并启用武器 Animator，可选播放掏枪动画，
        // 然后停止走路/跑步动作，并把相机父约束目标设为武器头部骨骼。
        private void OnUnholster(bool prop, bool playAnim)
        {
            var animator = weaponController.Id.GetComponentInChildren<Animator>(true);
            animator.Rebind();
            animator.Update(0f);
            animator.enabled = true;
            if (playAnim)
                CowsinsUtilities.PlayAnim(UNHOLSTER_KEY, animator);

            StopWalkAndRunMotion();
            SetParentConstraintSource(weaponController.Id.HeadBone);
        }

        // 开始换弹：根据弹匣是否为空选择“空仓换弹”或“普通换弹”动画键并播放。
        private void StartReload()
        {
            if(weaponController == null) return;
            string reloadKey = weaponController.Id.bulletsLeftInMagazine == 0 ? EMPTY_RELOADING_KEY : RELOADING_KEY;
            CowsinsUtilities.PlayAnim(reloadKey, weaponController.Id.Animator);
        }

        // 取消换弹：将武器 Animator 交叉淡入到 Locomotion（移动）状态，快速回到待机。
        private void CancelReload()
        {
            if (weaponController == null || weaponController.Id == null) return;
            Animator animator = weaponController.Id.Animator;
            if (animator == null) return;
            animator.CrossFadeInFixedTime("Locomotion", 0.1f, 0);
        }

        // 判断 Animator 是否处于 Locked（锁定）标签状态：检查当前状态或过渡目标状态是否带 Locked 标签。
        private bool IsAnimatorLocked(Animator animator)
        {
            if (animator == null) return false;

            // Current state
            if (animator.GetCurrentAnimatorStateInfo(0).IsTag("Locked"))
                return true;

            // Destination state
            if (animator.IsInTransition(0))
            {
                var nextState = animator.GetNextAnimatorStateInfo(0);
                if (nextState.IsTag("Locked"))
                    return true;
            }

            return false;
        }


        #region INSPECT
        /// <summary>开始检视：播放 inspect 动画并停止 finishedInspect 动画。</summary>
        public void InitializeInspection()
        {
            WeaponIdentification wiD = weaponController.Id;
            CowsinsUtilities.PlayAnim(INSPECT_KEY, wiD.Animator);
            CowsinsUtilities.StopAnim(FINISHEDINSPECT_KEY, wiD.Animator);
        }

        /// <summary>结束检视：播放 finishedInspect 动画并停止 inspect 动画。</summary>
        public void DisableInspection()
        {
            WeaponIdentification wID = weaponController.Id;
            CowsinsUtilities.PlayAnim(FINISHEDINSPECT_KEY, wID.Animator);
            CowsinsUtilities.StopAnim(INSPECT_KEY, wID.Animator);
        }
        #endregion
    }
}