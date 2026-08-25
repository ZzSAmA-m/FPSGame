// CameraFOVManager.cs：相机视野（FOV）管理器。
// 负责在普通、奔跑、墙跑、瞄准、射击等状态间平滑切换相机视野，
// 通过订阅玩家移动事件与武器事件设置目标 FOV，并在 Update 中逐帧插值逼近目标值。
using UnityEngine;

namespace cowsins
{
    /// <summary>
    /// 相机视野管理器：根据玩家移动与武器状态动态调整并平滑过渡相机的 fieldOfView。
    /// </summary>
    public class CameraFOVManager : MonoBehaviour
    {
        // 常规状态下的相机视野值。
        private float normalFOV;
        // 墙跑状态下的相机视野值。
        private float wallrunningFOV;
        // 奔跑状态下的相机视野值。
        private float runningFOV;
        // 本组件所挂载的相机引用。
        private Camera cam;
        // 玩家移动状态提供者（实现于 PlayerMovement.cs），提供各状态下的 FOV 配置。
        private IPlayerMovementStateProvider playerMovement; // IPlayerMovementStateProvider is implemented in PlayerMovement.cs
        // 武器引用提供者（实现于 WeaponController.cs），提供当前武器数据。
        private IWeaponReferenceProvider weaponReference; // IWeaponReferenceProvider is implemented in WeaponController.cs
        // 武器行为提供者（实现于 WeaponController.cs），提供瞄准等武器状态。
        private IWeaponBehaviourProvider weaponBehaviour; // IWeaponBehaviourProvider is implemented in WeaponController.cs
        // 武器事件提供者（实现于 WeaponController.cs），用于订阅武器相关事件。
        private IWeaponEventsProvider weaponEvents; // IWeaponEventsProvider is implemented in WeaponController.cs
        // 目标视野值，Update 中会逐帧向它插值逼近。
        private float targetFOV;
        // 视野插值的速度系数，决定过渡快慢。
        private float lerpSpeed;

        /// <summary>
        /// 初始化视野管理器：缓存相机与各依赖引用、读取初始 FOV 配置，并订阅墙跑/瞄准/射击/选枪等事件以联动视野切换。
        /// </summary>
        /// <param name="playerDependencies">玩家依赖集合，提供移动状态、武器引用/行为/事件等接口。</param>
        public void Initialize(PlayerDependencies playerDependencies)
        {
            cam = GetComponent<Camera>();
            playerMovement = playerDependencies.PlayerMovementState;
            weaponReference = playerDependencies.WeaponReference;
            weaponBehaviour = playerDependencies.WeaponBehaviour;
            weaponEvents = playerDependencies.WeaponEvents;

            normalFOV = playerMovement.NormalFOV; // Initialize baseFOV once in Start
            wallrunningFOV = playerMovement.WallRunningFOV;
            runningFOV = playerMovement.RunningFOV;
            targetFOV = normalFOV;

            cam.fieldOfView = normalFOV;

            playerDependencies.PlayerMovementEvents.Events.OnWallRunStart.AddListener(SetFOVToWallrun);
            playerDependencies.PlayerMovementEvents.Events.OnWallRunStop.AddListener(SetFOVToNormal);
            weaponEvents.Events.OnAimStop.AddListener(OnAimStop);
            weaponEvents.Events.OnAimStart.AddListener(OnAimStart);
            weaponEvents.Events.OnShootApplyFOV.AddListener(ForceAddFOV);
            weaponEvents.Events.OnSelectWeapon.AddListener(SetFOVOnUnholster);
        }

        // 每帧将相机视野向目标 FOV 平滑插值逼近。
        private void Update()
        {
            // Smoothly interpolate FOV towards the target value
            cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, targetFOV, Time.deltaTime * lerpSpeed);
        }
        // 判断当前是否允许更改视野：仅当玩家正在瞄准且持有武器时返回 true，用于在瞄准期间锁定 FOV 修改。
        private bool AllowChangeFOV()
        {
            return weaponBehaviour.IsAiming && weaponReference.Weapon != null;
        }

        /// <summary>
        /// 设置目标视野值及过渡速度；若当前正在瞄准（不允许更改）则直接返回。
        /// </summary>
        /// <param name="fov">目标视野值。</param>
        /// <param name="speed">过渡插值速度。</param>
        public void SetFOV(float fov, float speed)
        {
            if (AllowChangeFOV())
                return; // Not applicable if aiming
            targetFOV = fov;
            lerpSpeed = speed;  
        }

        /// <summary>
        /// 设置目标视野值（可空类型），过渡速度使用玩家移动配置中的 FadeFOVAmount；瞄准期间不生效。
        /// </summary>
        /// <param name="fov">目标视野值，可为 null。</param>
        public void SetFOV(float? fov)
        {
            if (AllowChangeFOV())
                return; // Not applicable if aiming
            targetFOV = fov.Value;
            lerpSpeed = playerMovement.FadeFOVAmount; 
        }

        /// <summary>
        /// 强制在当前视野上直接增减（用于射击瞬间的 FOV 脉冲效果），并重置过渡速度。
        /// </summary>
        /// <param name="fov">要增减的视野量（负值表示视野缩小）。</param>
        public void ForceAddFOV(float fov)
        {
            cam.fieldOfView -= fov;
            lerpSpeed = playerMovement.FadeFOVAmount;
        }

        /// <summary>将视野恢复为常规值。</summary>
        public void SetFOVToNormal() => SetFOV(normalFOV);
        /// <summary>将视野切换为墙跑状态值。</summary>
        public void SetFOVToWallrun() => SetFOV(wallrunningFOV);
        /// <summary>将视野切换为奔跑状态值。</summary>
        public void SetFOVToRun() => SetFOV(runningFOV);
        // 开始瞄准时回调：读取当前武器的瞄准视野，并以传入的速度过渡到瞄准视野。
        private void OnAimStart(float aimingOutSpeed)
        {
            Weapon_SO weapon = weaponReference.Weapon;
            if(weapon == null) return;

            SetFOV(weapon.aimingFOV, aimingOutSpeed);
        }
        // 停止瞄准时回调：根据是否正在墙跑，将视野恢复为墙跑视野或常规视野。
        private void OnAimStop()
        {
            SetFOV(playerMovement.IsWallRunning ? playerMovement.WallRunningFOV : playerMovement.NormalFOV);
        }

        // 掏出/切换武器时回调：若正在瞄准且有武器则设为瞄准视野，否则恢复常规视野。
        private void SetFOVOnUnholster()
        {
            SetFOV(weaponBehaviour.IsAiming && weaponReference.Weapon != null ? weaponReference.Weapon.aimingFOV : playerMovement.NormalFOV); 
        }
    }
}
