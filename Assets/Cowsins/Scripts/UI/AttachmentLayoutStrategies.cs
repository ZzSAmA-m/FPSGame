// ============================================================================
// AttachmentLayoutStrategies.cs —— 配件分组 UI 的布局策略。
// 定义布局策略接口及其两种实现：WorldSpaceLayoutStrategy 将 3D 配件位置转换为
// 屏幕坐标；VerticalLayoutStrategy 将配件分组按垂直方向以固定间距排列。
// 通过策略模式让 AttachmentGroupUI 可以灵活切换不同的布局方式。
// ============================================================================
using UnityEngine;

namespace cowsins
{
    /// <summary>
    /// Interface that defines a strategy for calculating attachment group UI positions / different layout modes
    /// 定义配件分组 UI 位置计算策略的接口（对应不同的布局模式）。
    /// </summary>
    public interface IAttachmentLayoutStrategy
    {
        /// <summary>
        /// Calculates the position for the attachment group UI element.
        /// 计算配件分组 UI 元素应放置的位置。
        /// </summary>
        /// <param name="target">The 3D target attachment in world space</param>
        /// <param name="rectTransform">RectTransform of the UI element</param>
        /// <param name="groupIndex">The index of this attachment group</param>
        Vector3 CalculatePosition(Transform target, RectTransform rectTransform, int groupIndex);

        bool RequiresTarget { get; } // 该策略是否需要 3D 目标（世界空间追踪需要，固定布局不需要）
    }

    /// <summary>
    /// Captures the 3D attachment position and converts to screen space
    /// 世界空间追踪策略：捕获 3D 配件位置并转换为屏幕坐标。
    /// </summary>
    public class WorldSpaceLayoutStrategy : IAttachmentLayoutStrategy
    {
        public bool RequiresTarget => true; // 该策略需要 3D 目标

        // 计算位置：将 3D 目标位置转换为屏幕坐标返回。
        public Vector3 CalculatePosition(Transform target, RectTransform rectTransform, int groupIndex)
        {
            if (target == null || Camera.main == null) return rectTransform.position; // 无目标或主相机时返回当前位置

            Vector3 objectPosition = target.position; // 获取 3D 目标世界坐标
            Vector3 screenPosition = Camera.main.WorldToScreenPoint(objectPosition); // 转为屏幕坐标
            return screenPosition;
        }
    }

    /// <summary>
    /// Displays attachment groups in a column
    /// 垂直列布局策略：将配件分组沿垂直方向按间距依次排列。
    /// </summary>
    public class VerticalLayoutStrategy : IAttachmentLayoutStrategy
    {
        private readonly float spacing; // 分组之间的间距
        private readonly Vector2 startPosition; // 起始位置
        private readonly bool spacingDown; // 是否向下排列（否则向上）

        public bool RequiresTarget => false; // 固定布局不需要 3D 目标

        // 构造函数：设置间距、起始位置与排列方向。
        public VerticalLayoutStrategy(float spacing = 150f, Vector2? startPosition = null, bool spacingDown = true)
        {
            this.spacing = spacing;
            this.startPosition = startPosition ?? Vector2.zero; // 未指定起始位置时使用原点
            this.spacingDown = spacingDown;
        }

        // 计算位置：根据分组索引与间距计算垂直方向上的位置（向下或向上）。
        public Vector3 CalculatePosition(Transform target, RectTransform rectTransform, int groupIndex)
        {
            float yPosition = spacingDown 
                ? startPosition.y - (groupIndex * spacing)
                : startPosition.y + (groupIndex * spacing);
            return new Vector2(startPosition.x, yPosition);
        }
    }
}
