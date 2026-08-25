// =====================================================================
// CrouchTilt.cs：蹲伏倾斜效果组件。
// 在玩家蹲伏时对武器进行旋转与位置倾斜，瞄准时临时复位，
// 松开瞄准或起身/蹲下时通过协程平滑过渡到目标姿态。
// =====================================================================
/// <summary>
/// This script belongs to cowsins as a part of the cowsins´ FPS Engine. All rights reserved.
/// </summary>
using UnityEngine;
using System.Collections;

namespace cowsins
{
    /// <summary>
    /// 蹲伏倾斜效果：根据蹲伏与瞄准状态，平滑过渡武器的旋转与位置。
    /// </summary>
    public class CrouchTilt : MonoBehaviour
    {
        // 玩家依赖集合（提供移动状态、武器行为与相关事件）。
        [SerializeField] private PlayerDependencies playerDependencies;

        // 蹲伏时的目标旋转（tiltRot）与位置偏移（tiltPosOffset）。
        [Tooltip("Rotation desired when crouching"), SerializeField] private Vector3 tiltRot, tiltPosOffset;
        // 倾斜过渡速度。
        [Tooltip("Tilting / Rotation velocity"), SerializeField] private float tiltSpeed;

        // 武器行为提供者（用于读取瞄准状态）。
        private IWeaponBehaviourProvider wp; // IWeaponBehaviourProvider is implemented in WeaponController.cs

        // 站立姿态下的初始旋转。
        private Quaternion origRot;
        // 站立姿态下的初始位置。
        private Vector3 origPos;
        // 倾斜过渡协程引用。
        private Coroutine tiltCoroutine;

        // 当前是否处于蹲伏状态。
        private bool isCrouching;

        // 初始化：缓存依赖与初始姿态，并订阅蹲伏与瞄准相关事件。
        void Start()
        {
            wp = playerDependencies.WeaponBehaviour;

            origRot = transform.localRotation;
            origPos = transform.localPosition;

            if (playerDependencies.PlayerMovementState != null)
                isCrouching = playerDependencies.PlayerMovementState.IsCrouching;

            playerDependencies.PlayerMovementEvents.Events.OnCrouchStart.AddListener(StartCrouch);
            playerDependencies.PlayerMovementEvents.Events.OnCrouchStop.AddListener(StopCrouch);

            var weaponEvents = wp as IWeaponEventsProvider;
            if (weaponEvents != null)
            {
                weaponEvents.Events.OnAimStart.AddListener(OnAimStart);
                weaponEvents.Events.OnAimStop.AddListener(OnAimStop);
            }
        }

        // 禁用时移除所有订阅的事件监听，避免悬空回调。
        private void OnDisable()
        {
            if (playerDependencies != null && playerDependencies.PlayerMovementEvents != null)
            {
                playerDependencies.PlayerMovementEvents.Events.OnCrouchStart.RemoveListener(StartCrouch);
                playerDependencies.PlayerMovementEvents.Events.OnCrouchStop.RemoveListener(StopCrouch);
            }

            var weaponEvents = wp as IWeaponEventsProvider;
            if (weaponEvents != null)
            {
                weaponEvents.Events.OnAimStart.RemoveListener(OnAimStart);
                weaponEvents.Events.OnAimStop.RemoveListener(OnAimStop);
            }
        }

        // 开始蹲伏：设置状态，未瞄准时开始倾斜到蹲伏姿态。
        private void StartCrouch()
        {
            isCrouching = true; 
            if (!wp.IsAiming) StartTilting(tiltRot, origPos + tiltPosOffset);
        }

        // 结束蹲伏：平滑过渡回站立时的初始旋转与位置。
        private void StopCrouch()
        {
            isCrouching = false;
            StartTilting(origRot.eulerAngles, origPos);
        }

        // 开始瞄准：若处于蹲伏状态，临时恢复到站立姿态（避免瞄准时倾斜）。
        private void OnAimStart(float fov)
        {
            if (isCrouching) StartTilting(origRot.eulerAngles, origPos);
        }

        // 结束瞄准：若仍处于蹲伏状态，重新倾斜回蹲伏姿态。
        private void OnAimStop()
        {
            if (isCrouching) StartTilting(tiltRot, origPos + tiltPosOffset);
        }

        // 启动倾斜过渡协程（先停止旧的协程再开始新的过渡）。
        private void StartTilting(Vector3 targetRotation, Vector3 targetPosition)
        {
            if (tiltCoroutine != null) StopCoroutine(tiltCoroutine);
            tiltCoroutine = StartCoroutine(TiltRoutine(targetRotation, targetPosition));
        }

        // 倾斜过渡协程：逐帧插值旋转与位置，直到接近目标姿态后结束。
        private IEnumerator TiltRoutine(Vector3 targetRotation, Vector3 targetPosition)
        {
            while (Quaternion.Angle(transform.localRotation, Quaternion.Euler(targetRotation)) > 0.1f ||
                   Vector3.Distance(transform.localPosition, targetPosition) > 0.01f)
            {
                transform.localRotation = Quaternion.Lerp(transform.localRotation, Quaternion.Euler(targetRotation), Time.deltaTime * tiltSpeed);    
                transform.localPosition = Vector3.Lerp(transform.localPosition, targetPosition, Time.deltaTime * tiltSpeed);
                yield return null;  // Wait for the next frame before continuing
            }
        }
    }
}
