// AimBehaviour.cs：武器瞄准行为。
// 负责进入/退出瞄准时的处理：计算瞄准点位置（含瞄准镜偏移），将瞄准点与武器平滑过渡到瞄准姿态，
// 更新瞄准时的镜头抖动倍率，并触发瞄准开始/进行中/停止等事件。
using UnityEngine;

namespace cowsins
{
    /// <summary>
    /// 武器瞄准行为：处理瞄准状态的进入与退出，计算瞄准点位置并联动武器姿态、镜头抖动倍率与相关事件。
    /// </summary>
    public class AimBehaviour
    {
        // 武器上下文，聚合依赖项与设置。
        private WeaponContext context;
        // 输入管理器（本系统内暂未直接使用）。
        private InputManager inputManager;
        // 武器行为提供者，提供瞄准等武器状态。
        private IWeaponBehaviourProvider weaponBehaviour;
        // 武器引用提供者，提供当前武器与实例标识。
        private IWeaponReferenceProvider weaponReference;
        // 武器事件提供者，用于触发瞄准相关事件。
        private IWeaponEventsProvider weaponEvents;
        // 玩家移动状态提供者（本系统内暂未直接使用）。
        private IPlayerMovementStateProvider playerMovement;

        // 退出瞄准时的过渡速度（缓存用于平滑复位）。
        private float aimingOutSpeed;
        // 瞄准时的镜头抖动倍率：瞄准期间取武器的倍率，退出后重置为 1。
        private float aimingCamShakeMultiplier;

        // 当前武器数据（Weapon_SO）快捷属性。
        private Weapon_SO weapon => weaponReference.Weapon;
        // 当前武器实例标识快捷属性。
        private WeaponIdentification id => weaponReference.Id;
        // 主相机快捷属性。
        private Camera mainCamera => weaponReference.MainCamera;
        // 武器挂载点（持枪父节点）快捷属性。
        private Transform weaponHolder => context.WeaponHolder;

        // 武器控制器设置，含用户事件等配置。
        private WeaponControllerSettings settings;

        /// <summary>
        /// 构造函数：缓存依赖引用与设置，并订阅切换武器事件以在切枪时强制重置瞄准姿态。
        /// </summary>
        /// <param name="context">武器上下文。</param>
        public AimBehaviour(WeaponContext context)
        {
            this.context = context;
            this.inputManager = context.InputManager;
            this.weaponBehaviour = context.Dependencies.WeaponBehaviour;
            this.weaponReference = context.Dependencies.WeaponReference;
            this.weaponEvents = context.Dependencies.WeaponEvents;
            this.playerMovement = context.Dependencies.PlayerMovementState;
            this.settings = context.Settings;

            weaponEvents.Events.OnSwitchingWeapon.AddListener(ForceAimReset);
        }

        /// <summary>
        /// 每帧执行的瞄准更新：首次进入瞄准时触发开始事件并缓存过渡速度；随后计算相机前方一定距离的瞄准点位置，
        /// 叠加瞄准镜（Scope）偏移后，将武器的 aimPoint 平滑过渡到该瞄准姿态，并触发“瞄准中”事件。
        /// </summary>
        public void Tick()
        {
            if (!weaponBehaviour.IsAiming)
            {
                settings.userEvents.OnStartAim?.Invoke();
                aimingOutSpeed = (weapon != null) ? id.aimSpeed : 2;
                
                weaponEvents.Events.OnAimStart?.Invoke(aimingOutSpeed);
            }
            weaponBehaviour.IsAiming = true;

            aimingCamShakeMultiplier = weapon.camShakeAimMultiplier;

            // Get distance from camera to weapons && Calculate new Aim Position locally
            float cameraDistance = mainCamera.nearClipPlane + weapon.aimDistance;
            Vector3 localForwardCamera = mainCamera.transform.position + mainCamera.transform.forward * cameraDistance;

            // Calculate Scope Offset if there�s any scope available
            Vector3 scopeOffset = Vector3.zero;
            Attachment scopeAttachment = id.AttachmentState.GetCurrent(AttachmentType.Scope);
            if (scopeAttachment)
            {
                Scope scopeComponent = scopeAttachment.GetComponent<Scope>();
                scopeOffset = mainCamera.transform.TransformVector(scopeComponent.aimingOffset);
            }
            Vector3 aimPosition = localForwardCamera + scopeOffset;

            if(!weapon.ignoreAimPoint)
            {
                id.aimPoint.position = Vector3.Lerp(id.aimPoint.position, aimPosition, id.aimSpeed * Time.deltaTime);
                id.aimPoint.localRotation = Quaternion.Lerp(id.aimPoint.localRotation, Quaternion.Euler(id.aimingRotation), id.aimSpeed * Time.deltaTime);
            }

            weaponEvents.Events.OnAiming?.Invoke();
            settings.userEvents.OnAiming?.Invoke();
        }

        /// <summary>
        /// 退出瞄准：将瞄准状态与镜头抖动倍率复位，并把瞄准点位置/旋转以及武器挂载点平滑过渡回初始姿态。
        /// </summary>
        public void Exit()
        {
            if (weaponBehaviour.IsAiming)
            {
                weaponBehaviour.IsAiming = false;

                settings.userEvents.OnStopAim?.Invoke();
                weaponEvents.Events.OnAimStop?.Invoke();
            }

            weaponBehaviour.IsAiming = false;
            aimingCamShakeMultiplier = 1;

            if (id == null || weapon == null) return;
            // Change the position and FOV
            if (!weapon.ignoreAimPoint)
            {
                id.aimPoint.localPosition = Vector3.Lerp(id.aimPoint.localPosition, id.originalAimPointPos, id.aimSpeed * Time.deltaTime);
                id.aimPoint.localRotation = Quaternion.Lerp(id.aimPoint.localRotation, Quaternion.Euler(id.originalAimPointRot), aimingOutSpeed * Time.deltaTime);
            }

            weaponHolder.localRotation = Quaternion.Lerp(weaponHolder.transform.localRotation, Quaternion.Euler(Vector3.zero), aimingOutSpeed * Time.deltaTime);
        }

        // 强制重置瞄准：立即将瞄准点与武器挂载点恢复到初始位置/旋转，用于切枪等场景瞬间复位。
        /// <summary>
        /// Forces the Weapon to go back to its initial position 
        /// </summary>
        public void ForceAimReset()
        {
            weaponBehaviour.IsAiming = false;
            if (weaponReference.Id?.aimPoint)
            {
                weaponReference.Id.aimPoint.localPosition = weaponReference.Id.originalAimPointPos;
                weaponReference.Id.aimPoint.localRotation = Quaternion.Euler(id.originalAimPointRot);
            }
            context.WeaponHolder.localRotation = Quaternion.Euler(Vector3.zero);
        }
    }

}