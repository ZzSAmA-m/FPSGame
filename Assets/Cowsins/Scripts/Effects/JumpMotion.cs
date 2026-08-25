// =====================================================================
// JumpMotion.cs：跳跃/落地动作表现组件。
// 在玩家跳跃或落地时，按配置的动画曲线对武器施加位移与旋转动画，
// 用于增强跳跃与落地的视觉反馈，通过协程逐帧采样曲线驱动。
// =====================================================================
using UnityEngine;
using System.Collections;

namespace cowsins
{
    /// <summary>
    /// 跳跃/落地动作表现：根据动画曲线驱动武器的位移与旋转。
    /// </summary>
    public class JumpMotion : MonoBehaviour
    {
        // 玩家依赖集合（提供跳跃/落地等移动事件）。
        [SerializeField] private PlayerDependencies playerDependencies;
        // 跳跃（jumpMotion）与落地（groundedMotion）时使用的动画曲线。
        [SerializeField] private AnimationCurve jumpMotion, groundedMotion;
        // 位移距离与旋转幅度。
        [SerializeField] private float distance, rotationAmount;
        // 曲线采样速度（最小为 1，值越大动作越快）。
        [SerializeField, Min(1)] private float evaluationSpeed;

        // 动作协程引用（重复触发时先停止旧协程）。
        private Coroutine motionCoroutine;

        // 启用时订阅跳跃与落地事件。
        private void OnEnable()
        {
            playerDependencies.PlayerMovementEvents.Events.OnJump.AddListener(OnJump);
            playerDependencies.PlayerMovementEvents.Events.OnLand.AddListener(OnLand);
        }

        // 跳跃事件回调：停止旧动作协程并播放跳跃动画。
        private void OnJump()
        {
            if (motionCoroutine != null) StopCoroutine(motionCoroutine);
            motionCoroutine = StartCoroutine(ApplyMotion(jumpMotion));
        }

        // 落地事件回调：停止旧动作协程并播放落地动画。
        private void OnLand()
        {
            if (motionCoroutine != null) StopCoroutine(motionCoroutine);
            motionCoroutine = StartCoroutine(ApplyMotion(groundedMotion));
        }

        // 动作协程：逐帧采样动画曲线，插值武器位置与旋转，直到曲线走完（motion >= 1）。
        private IEnumerator ApplyMotion(AnimationCurve motionCurve)
        {
            float motion = 0;

            while (motion < 1f)
            {
                motion += Time.deltaTime * evaluationSpeed;
                // 采样曲线在当前进度下的值（用于计算位移与旋转）。
                float evaluatedMotion = motionCurve.Evaluate(motion);

                transform.localPosition = Vector3.Lerp(transform.localPosition, new Vector3(0, evaluatedMotion, 0) * distance, motion);
                transform.localRotation = Quaternion.Lerp(transform.localRotation, Quaternion.Euler(new Vector3(evaluatedMotion * rotationAmount, 0, 0)), motion);

                yield return null;
            }
        }
    }
}
