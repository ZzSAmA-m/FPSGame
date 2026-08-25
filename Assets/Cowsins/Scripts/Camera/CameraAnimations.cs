// CameraAnimations.cs：相机动画控制器。
// 通过监听 InteractManager 的 onDrop 事件，在玩家放下可交互物体时重置旋转目标；
// 存在目标时让相机跟随目标相对参考旋转的差值，无目标时则平滑复位相机本地旋转到零。
using UnityEngine;

namespace cowsins
{
    /// <summary>
    /// 相机动画组件：跟随一个目标物体的旋转差值，或在无目标时平滑恢复相机本地旋转至零。
    /// </summary>
    public class CameraAnimations : MonoBehaviour
    {
        // 交互管理器引用，用于订阅“放下物体”事件以重置相机旋转目标。
        [SerializeField] private InteractManager interactManager;
        // 无目标时，相机旋转复位到零的插值速度。
        [SerializeField] private float rotationResetSpeed = 2;
        // 相机需要跟随旋转的目标物体。
        private Transform target;
        // 记录设置目标时的参考旋转，用于计算目标相对该参考的旋转差值。
        private Quaternion referenceRotation;

        // 启用时订阅“放下物体”事件，触发旋转目标重置。
        private void OnEnable() => interactManager.userEvents.onDrop.AddListener(ResetTarget);
        // 禁用时取消订阅，避免组件销毁后事件仍指向已失效的方法。
        private void OnDisable() => interactManager.userEvents.onDrop.RemoveListener(ResetTarget);

        // 每帧执行：无目标时平滑复位本地旋转到零；有目标时应用相对参考旋转的差值，使相机跟随目标旋转。
        private void Update()
        {
            if (target == null)
            {
                transform.localRotation = Quaternion.Lerp(transform.localRotation, Quaternion.Euler(Vector3.zero), Time.deltaTime * rotationResetSpeed);
                return;
            }

            // Apply delta from the reference
            Quaternion deltaRotation = Quaternion.Inverse(referenceRotation) * target.localRotation;
            transform.localRotation = deltaRotation;
        }

        // 设置相机要跟随旋转的目标，并记录目标当前旋转作为参考（目标为空时参考设为零旋转）。
        /// <summary>
        /// Overrides the camera rotation target
        /// </summary>
        /// <param name="target">New target</param>
        public void SetTarget(Transform target)
        {
            this.target = target;
            referenceRotation = target != null ? target.localRotation : Quaternion.Euler(Vector3.zero);
        }

        /// <summary>
        /// 重置旋转目标为空，使相机旋转平滑复位到零（供 onDrop 事件回调使用）。
        /// </summary>
        public void ResetTarget(Pickeable pickeable) => SetTarget(null);
    }
}