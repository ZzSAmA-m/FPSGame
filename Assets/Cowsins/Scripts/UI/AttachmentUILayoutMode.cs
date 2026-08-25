// ============================================================================
// AttachmentUILayoutMode.cs —— 配件分组 UI 布局模式枚举。
// 定义配件分组（AttachmentGroupUI）在界面中的两种布局方式：跟随 3D 目标并
// 转换为屏幕坐标，或以垂直列布局显示，供布局策略工厂选择使用。
// ============================================================================
namespace cowsins
{
    /// <summary>
    /// Defines how attachment groups are displayed in the UI
    /// 定义配件分组在 UI 中的显示方式。
    /// </summary>
    public enum AttachmentUILayoutMode
    {
        // 跟随 3D 目标位置并转换为屏幕坐标
        WorldSpaceTracking, // follows 3D Target position converted to screen space
        // 以垂直列布局显示
        VerticalLayout // Displayed in a vertical column layout
    }
}
