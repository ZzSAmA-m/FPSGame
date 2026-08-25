// SliderSettingUI（滑块设置 UI 控件）：
// 将浮点设置（ISetting<float>）绑定到 Slider 滑块上，实现 UI 与设置值的双向同步，
// 并用文本实时显示当前值（可自定义数值格式化字符串）。
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace cowsins.Settings.UI
{
    /// <summary>
    /// 滑块设置 UI：绑定浮点设置到 Slider，并同步显示数值文本。
    /// </summary>
    public class SliderSettingUI : SettingUIController
    {
        // 绑定的滑块控件
        [SerializeField] private Slider slider;
        // 用于显示当前数值的文本
        [SerializeField] private TextMeshProUGUI valueText;
        // 数值显示格式（如 "F1" 保留一位小数）
        [SerializeField] private string format = "F1";

        // 绑定的浮点设置
        private ISetting<float> setting;

        // 绑定设置并立即初始化 UI
        public void Bind(ISetting<float> setting)
        {
            this.setting = setting;
            Initialize();
        }

        // 初始化：把设置值赋给滑块、刷新文本，并相互订阅变更事件
        public override void Initialize()
        {
            if (setting == null || slider == null) return;

            slider.value = setting.Value;
            UpdateValueText(setting.Value);

            slider.onValueChanged.AddListener(OnSliderChanged);
            setting.OnValueChanged += OnSettingChanged;
        }

        // 清理：移除双方监听
        public override void Cleanup()
        {
            if (slider != null)
                slider.onValueChanged.RemoveListener(OnSliderChanged);
            if (setting != null)
                setting.OnValueChanged -= OnSettingChanged;
        }

        // 用户拖动滑块后：写回设置并刷新数值文本
        private void OnSliderChanged(float value)
        {
            setting.Value = value;
            UpdateValueText(value);
        }

        // 设置外部变化后：同步滑块位置与文本显示
        private void OnSettingChanged(float value)
        {
            if (!Mathf.Approximately(slider.value, value))
                slider.value = value;
            UpdateValueText(value);
        }

        // 按格式把数值写入显示文本
        private void UpdateValueText(float value)
        {
            if (valueText != null)
                valueText.text = value.ToString(format);
        }

        // 销毁时清理监听
        private void OnDestroy() => Cleanup();
    }
}