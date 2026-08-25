// SettingsMenuController（设置菜单控制器）：
// 设置界面的总控制器：在启动时获取 GameSettingsManager 单例，把各 UI 控件与对应的设置项绑定，
// 填充分辨率下拉框选项，并处理“重置全部设置”按钮，同时负责销毁时清理监听。
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

namespace cowsins.Settings.UI
{
    // Main settings menu controller
    /// <summary>
    /// 设置菜单控制器：绑定各设置 UI 与设置项，填充分辨率选项并处理重置逻辑。
    /// </summary>
    public class SettingsMenuController : MonoBehaviour
    {
        [Header("Graphics")]
        // 全屏开关 UI
        [SerializeField] private ToggleSettingUI fullscreenToggle;
        // 分辨率下拉框（原生 TMP_Dropdown，特殊处理）
        [SerializeField] private TMP_Dropdown resolutionDropdown;
        // 帧率下拉框
        [SerializeField] private DropdownSettingUI frameRateDropdown;
        // 垂直同步开关
        [SerializeField] private ToggleSettingUI vsyncToggle;
        // 画质下拉框
        [SerializeField] private DropdownSettingUI qualityDropdown;

        [Header("Audio")]
        // 主音量滑块
        [SerializeField] private SliderSettingUI masterVolumeSlider;

        [Header("Controls")]
        // 鼠标水平灵敏度滑块
        [SerializeField] private SliderSettingUI mouseSensXSlider;
        // 鼠标垂直灵敏度滑块
        [SerializeField] private SliderSettingUI mouseSensYSlider;
        // 手柄水平灵敏度滑块
        [SerializeField] private SliderSettingUI controllerSensXSlider;
        // 手柄垂直灵敏度滑块
        [SerializeField] private SliderSettingUI controllerSensYSlider;
        // 鼠标 Y 轴反转开关
        [SerializeField] private ToggleSettingUI invertYToggle;
        // 手柄 Y 轴反转开关
        [SerializeField] private ToggleSettingUI invertYControllerToggle;

        [Header("Buttons")]
        // 重置按钮
        [SerializeField] private Button resetButton;

        // 设置管理器单例引用
        private GameSettingsManager settingsManager;

        // 启动时：获取管理器、初始化 UI、绑定设置并设置按钮
        private void Start()
        {
            settingsManager = GameSettingsManager.Instance;
            if (settingsManager == null)
            {
                Debug.LogError("GameSettingsManager instance not found!");
                return;
            }

            InitializeUI();
            BindSettings();
            SetupButtons();
        }

        // 初始化 UI（填充分辨率下拉框选项）
        private void InitializeUI()
        {
            PopulateResolutionDropdown();
        }

        // 把各 UI 控件绑定到对应的设置项
        private void BindSettings()
        {
            // Graphics
            fullscreenToggle?.Bind(settingsManager.Graphics.Fullscreen);
            frameRateDropdown?.Bind(settingsManager.Graphics.FrameRate);
            vsyncToggle?.Bind(settingsManager.Graphics.VSync);
            qualityDropdown?.Bind(settingsManager.Graphics.Quality);

            // Resolution is a special case
            // 分辨率使用原生下拉框，单独手动绑定
            if (resolutionDropdown != null)
            {
                resolutionDropdown.value = settingsManager.Graphics.Resolution.Value;
                resolutionDropdown.onValueChanged.AddListener(OnResolutionChanged);
            }

            // Audio
            masterVolumeSlider?.Bind(settingsManager.Audio.MasterVolume);

            // Controls
            mouseSensXSlider?.Bind(settingsManager.Controls.MouseSensitivityX);
            mouseSensYSlider?.Bind(settingsManager.Controls.MouseSensitivityY);
            controllerSensXSlider?.Bind(settingsManager.Controls.ControllerSensitivityX);
            controllerSensYSlider?.Bind(settingsManager.Controls.ControllerSensitivityY);
            invertYToggle?.Bind(settingsManager.Controls.InvertY);
            invertYControllerToggle?.Bind(settingsManager.Controls.InvertYController);
        }

        // 填充分辨率下拉框：清空后按可用分辨率生成形如 "1920 x 1080 @ 60.00Hz" 的选项
        private void PopulateResolutionDropdown()
        {
            if (resolutionDropdown == null) return;

            resolutionDropdown.ClearOptions();
            Resolution[] resolutions = settingsManager.Graphics.Resolution.GetAvailableResolutions();

            List<string> options = new List<string>();
            foreach (Resolution res in resolutions)
            {
                // This avoids getting an annoying "Depcrated Warning" when using Unity 6 or above. newer Unity versions use refreshRateRatio not just refreshRate
#if UNITY_6000_0_OR_NEWER
                float refreshRate = (float)res.refreshRateRatio.numerator / res.refreshRateRatio.denominator;
                options.Add($"{res.width} x {res.height} @ {refreshRate:F2}Hz");
#else
                options.Add($"{res.width} x {res.height} @ {Mathf.Round(res.refreshRate)}Hz");
#endif
            }

            resolutionDropdown.AddOptions(options);
            resolutionDropdown.value = settingsManager.Graphics.Resolution.Value;
            resolutionDropdown.RefreshShownValue();
        }

        // 用户选择分辨率后：写回分辨率设置
        private void OnResolutionChanged(int value)
        {
            settingsManager.Graphics.Resolution.Value = value;
        }

        // 为重置按钮绑定点击回调
        private void SetupButtons()
        {
            if (resetButton != null)
                resetButton.onClick.AddListener(OnResetClicked);
        }

        // 点击重置：重置所有设置并恢复默认按键绑定
        private void OnResetClicked()
        {
            settingsManager.ResetAllSettings();
            InputManager.ResetAllBindings();
        }

        // 销毁时移除监听，防止悬空引用
        private void OnDestroy()
        {
            if (resolutionDropdown != null)
                resolutionDropdown.onValueChanged.RemoveListener(OnResolutionChanged);

            if (resetButton != null)
                resetButton.onClick.RemoveListener(OnResetClicked);
        }
    }
}