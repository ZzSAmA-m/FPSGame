// =====================================================================
// ITab.cs：Cowsins Manager 标签页的统一接口定义。
// 所有标签页（如首页、武器页）都需实现该接口，供主窗口通过反射自动发现并渲染。
// 仅在编辑器环境（UNITY_EDITOR）下编译。
// =====================================================================
#if UNITY_EDITOR
namespace cowsins
{
    /// <summary>
    /// Cowsins Manager 标签页接口：定义标签页的名称、绘制方法以及被选中时的初始化方法。
    /// </summary>
    public interface ITab
    {
        // 标签页名称，显示在窗口顶部的标签栏中。
        string TabName { get; }
        // 绘制标签页内容的方法。
        void OnGUI();
        // 标签页被选中时调用一次，用于执行初始化逻辑。
        void StartTab();
    }
}
#endif