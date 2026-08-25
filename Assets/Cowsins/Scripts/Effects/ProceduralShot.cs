// =====================================================================
// ProceduralShot.cs：程序化射击表现组件。
// 根据 ProceduralShot_SO 配置的位移/旋转曲线，在射击时对武器施加程序化动画，
// 并根据瞄准状态应用缩放系数。通过单例 Instance 对外提供触发入口。
// =====================================================================
using UnityEngine;
using System.Collections;

namespace cowsins
{
    /// <summary>
    /// 程序化射击表现：依据射击模式（ProceduralShot_SO）驱动武器的位移与旋转动画。
    /// </summary>
    public class ProceduralShot : MonoBehaviour
    {
        // 玩家依赖集合（提供武器行为）。
        [SerializeField] private PlayerDependencies player;
        
        // 武器行为提供者（读取瞄准状态）。
        private IWeaponBehaviourProvider weaponController;

        // 当前使用的程序化射击模式配置。
        private ProceduralShot_SO pattern;

        // 单例实例引用。
        static ProceduralShot instance;
        // 单例访问器，供外部触发射击动画。
        public static ProceduralShot Instance => instance;

        // 射击动画协程引用（重复触发时先停止旧协程）。
        private Coroutine shotCoroutine;

        // 初始化：注册单例并获取武器行为提供者，引用缺失时输出错误。
        private void Start()
        {
            instance = this;
            if(player == null)
            {
                CowsinsUtilities.LogError("<b><color=yellow>Player Reference not found!</color></b> " +
                "Ignore this if you are not planning to use the Procedural Shot Feature.");
                return;
            }
            weaponController = player.WeaponBehaviour;

            if (weaponController == null)
            {
                CowsinsUtilities.LogError("<b><color=yellow>Player Reference does not provide a Weapon Controller!</color></b> " +
                "Please, ensure the referenced Player is the Weapon Controller to solve this error.");
            }
        }

        /// <summary>
        /// Start a Procedural Shot motion given a ProceduralShot_SO ( Procedural Shot Pattern )
        /// </summary>
        public void Shoot(ProceduralShot_SO shot)
        {
            pattern = shot;

            if (shotCoroutine != null) StopCoroutine(shotCoroutine); // Stop any ongoing shot motion
            shotCoroutine = StartCoroutine(ApplyShotMotion());
        }

        // 射击动画协程：逐帧采样位移/旋转曲线并应用到武器，按瞄准状态缩放幅度。
        private IEnumerator ApplyShotMotion()
        {
            float timer = 0; // Reset the timer

            while (timer < 1) // Continue the motion while timer is below 1
            {
                timer += Time.deltaTime * pattern.playSpeed; // Increase the timer

                // Evaluate positions
                float x = pattern.translation.xTranslation.Evaluate(timer);
                float y = pattern.translation.yTranslation.Evaluate(timer);
                float z = pattern.translation.zTranslation.Evaluate(timer);

                // Evaluate rotations
                float xRot = pattern.rotation.xRotation.Evaluate(timer);
                float yRot = pattern.rotation.yRotation.Evaluate(timer);
                float zRot = pattern.rotation.zRotation.Evaluate(timer);

                // Get the aiming multipliers depending on the state of the WeaponController
                float aimingTransl = weaponController.IsAiming && pattern != null ? pattern.aimingTranslationMultiplier : 1;
                float aimingRot = weaponController.IsAiming && pattern != null ? pattern.aimingRotationMultiplier : 1;

                // Apply the motions
                transform.localPosition = new Vector3(
                    x * pattern.translationDistance.x,
                    y * pattern.translationDistance.y,
                    z * pattern.translationDistance.z
                ) * aimingTransl;

                transform.localRotation = Quaternion.Euler(new Vector3(
                    xRot * pattern.rotationDistance.x,
                    yRot * pattern.rotationDistance.y,
                    zRot * pattern.rotationDistance.z
                ) * aimingRot);

                yield return null; // Wait for the next frame
            }
        }
    }
}
