using UnityEngine;

namespace GamePlay.Player
{
    /// <summary>玩家射击射线的公共配置。</summary>
    public static class PlayerShootConfig
    {
        private static float _shootIgnoreDistance = 0.3f;

        /// <summary>沿射击方向跳过的相机前方距离，单位为米；负数按 0 处理。</summary>
        public static float ShootIgnoreDistance => Mathf.Max(0f, _shootIgnoreDistance);

        /// <summary>从原始视线生成前移后的射击射线，不改变射击方向。</summary>
        public static Ray CreateShootRay(Ray viewRay) =>
            new Ray(viewRay.GetPoint(ShootIgnoreDistance), viewRay.direction);
    }
}
