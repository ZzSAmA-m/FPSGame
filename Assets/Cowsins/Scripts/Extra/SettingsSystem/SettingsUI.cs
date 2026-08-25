// SettingsUI（设置 UI 控制器基类）：
// 定义所有设置 UI 控件的抽象基类，约定“初始化绑定”与“清理解绑”两个抽象方法，
// 供下拉框、滑块、开关等具体设置 UI 控件继承实现，统一生命周期管理。
using UnityEngine;

namespace cowsins.Settings.UI
{
    // Base class for UI controllers that bind to settings
    /// <summary>
    /// 设置 UI 控制器基类：定义初始化与清理两个抽象方法。
    /// </summary>
    public abstract class SettingUIController : MonoBehaviour
    {
        // 初始化：将 UI 控件与对应设置值绑定
        public abstract void Initialize();
        // 清理：解除绑定并移除监听，防止内存泄漏
        public abstract void Cleanup();
    }
}