// ToggleSettingUI（开关设置 UI 控件）：
// 将布尔设置（ISetting<bool>）绑定到 Toggle 开关上，实现 UI 与设置值的双向同步：
// 用户切换开关时写回设置，设置外部变化时刷新开关状态。
using UnityEngine;
using UnityEngine.UI;

namespace cowsins.Settings.UI
{
    // UI Controller for toggle settings
    /// <summary>
    /// 开关设置 UI：绑定布尔设置到 Toggle，并双向同步。
    /// </summary>
    public class ToggleSettingUI : SettingUIController
    {
        // 绑定的开关控件
        [SerializeField] private Toggle toggle;
        // 绑定的布尔设置
        private ISetting<bool> setting;

        // 绑定设置并立即初始化 UI
        public void Bind(ISetting<bool> setting)
        {
            this.setting = setting;
            Initialize();
        }

        // 初始化：把设置值赋给开关，并相互订阅变更事件
        public override void Initialize()
        {
            if (setting == null || toggle == null) return;

            toggle.isOn = setting.Value;
            toggle.onValueChanged.AddListener(OnToggleChanged);
            setting.OnValueChanged += OnSettingChanged;
        }

        // 清理：移除双方监听
        public override void Cleanup()
        {
            if (toggle != null)
                toggle.onValueChanged.RemoveListener(OnToggleChanged);
            if (setting != null)
                setting.OnValueChanged -= OnSettingChanged;
        }

        // 用户切换开关后：写回设置
        private void OnToggleChanged(bool value)
        {
            setting.Value = value;
        }

        // 设置外部变化后：同步开关状态
        private void OnSettingChanged(bool value)
        {
            if (toggle.isOn != value)
                toggle.isOn = value;
        }

        // 销毁时清理监听
        private void OnDestroy() => Cleanup();
    }
}