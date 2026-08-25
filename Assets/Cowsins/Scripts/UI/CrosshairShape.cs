// ============================================================================
// CrosshairShape.cs —— 准星形状定义与切换。
// 通过 CrosshairParts 描述准星由哪些部件组成（上下左右四线、中心点、四个
// 角括号），CrosshairShape 组件则保存默认形状并提供运行时切换/重置形状的接口，
// 供 Crosshair 绘制时读取当前形状。
// ============================================================================
#if UNITY_EDITOR
using UnityEditor.Presets;
#endif
using UnityEngine;

namespace cowsins
{
    /// <summary>
    /// 描述准星由哪些部件组成（可序列化，供 Inspector 配置）。
    /// </summary>
    [System.Serializable]
    public class CrosshairParts
    {
        // 上/下/左/右四条线（默认开启），中心点（默认关闭），以及四个角括号（默认关闭）
        public bool topPart = true, downPart = true, leftPart = true, rightPart = true, center = false,
            topLeftBracket = false, topRightBracket = false, bottomLeftBracket = false, bottomRightBracket = false;
    }

    /// <summary>
    /// 准星形状组件：保存默认形状与当前形状，并提供切换/重置形状的方法。
    /// </summary>
    public class CrosshairShape : MonoBehaviour
    {
        [SerializeField] private CrosshairParts defaultCrosshairParts; // 默认准星形状（Inspector 配置）

        private CrosshairParts currentCrosshairParts; // 当前生效的准星形状

        public CrosshairParts DefaultCrosshairParts => defaultCrosshairParts; // 默认形状访问器
        public CrosshairParts CurrentCrosshairParts => currentCrosshairParts; // 当前形状访问器

        public string presetName; // 预设名称

        // 初始化时使用默认准星形状。
        private void Awake() => SetCrosshair(defaultCrosshairParts);
        // 设置当前准星形状：传入 null 时恢复默认形状。
        public void SetCrosshair(CrosshairParts parts)
        {
            if (parts == null) // 未指定形状时
            {
                ResetCrosshairToDefault(); // 恢复默认形状
                return;
            }
                
            currentCrosshairParts = parts; // 应用传入的准星形状
        }
        // 将当前准星形状重置为默认形状。
        public void ResetCrosshairToDefault() => currentCrosshairParts = defaultCrosshairParts;
    }
}