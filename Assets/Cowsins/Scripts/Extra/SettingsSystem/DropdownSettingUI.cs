// DropdownSettingUI（下拉框设置 UI 控件）：
// 将整型设置（ISetting<int>）绑定到 TMP_Dropdown 下拉框上，实现 UI 与设置值的双向同步：
// 用户选择时写回设置，设置外部变化时刷新下拉框显示。
using UnityEngine;
using TMPro;

namespace cowsins.Settings.UI
{
    // UI Controller for dropdown settings
    /// <summary>
    /// 下拉框设置 UI：绑定整型设置到 TMP_Dropdown，并双向同步。
    /// </summary>
    public class DropdownSettingUI : SettingUIController
    {
        // 绑定的下拉框控件
        [SerializeField] private TMP_Dropdown dropdown;
        // 绑定的整型设置
        private ISetting<int> setting;

        // 绑定设置并立即初始化 UI
        public void Bind(ISetting<int> setting)
        {
            this.setting = setting;
            Initialize();
        }

        // 初始化：把设置值赋给下拉框，并相互订阅变更事件
        public override void Initialize()
        {
            if (setting == null || dropdown == null) return;

            dropdown.value = setting.Value;
            dropdown.onValueChanged.AddListener(OnDropdownChanged);
            setting.OnValueChanged += OnSettingChanged;
        }

        // 清理：移除双方监听，防止悬空引用
        public override void Cleanup()
        {
            if (dropdown != null)
                dropdown.onValueChanged.RemoveListener(OnDropdownChanged);
            if (setting != null)
                setting.OnValueChanged -= OnSettingChanged;
        }

        // 用户在下拉框选择后：把新值写回设置
        private void OnDropdownChanged(int value)
        {
            setting.Value = value;
        }

        // 设置外部变化后：同步刷新下拉框显示
        private void OnSettingChanged(int value)
        {
            if (dropdown.value != value)
                dropdown.value = value;
        }

        // 销毁时清理监听
        private void OnDestroy() => Cleanup();
    }
}