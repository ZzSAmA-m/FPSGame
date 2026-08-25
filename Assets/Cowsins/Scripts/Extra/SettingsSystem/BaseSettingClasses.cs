// BaseSettingClasses（设置基类与具体设置类型）：
// 定义设置系统的核心类型体系：Setting<T> 抽象基类封装通用逻辑（值、默认值、保存/加载/应用/重置、值变更事件），
// 并基于 PlayerPrefs 实现 int / float / bool / string 四种基础类型，以及分辨率、帧率、垂直同步、画质、音量等具体设置。
using UnityEngine;
using System;
using System.Collections.Generic;

namespace cowsins.Settings
{
    /// <summary>
    /// Abstract base class for all settings with common functionalities
    /// </summary>
    /// <summary>
    /// 所有设置的抽象基类：提供键名、默认值、值变更事件与保存/加载/应用/重置的通用实现。
    /// </summary>
    public abstract class Setting<T> : ISetting<T>
    {
        // 设置键名（用于持久化存储）
        public string Key { get; private set; }
        // 默认值
        public T DefaultValue { get; private set; }
        // 值变更事件
        public event Action<T> OnValueChanged;

        // 当前值的后备存储字段
        private T _value;
        // 当前值：赋值时若与旧值不同，则触发事件并立即应用、保存
        public T Value
        {
            get => _value;
            set
            {
                if (!EqualityComparer<T>.Default.Equals(_value, value))
                {
                    _value = value;
                    OnValueChanged?.Invoke(_value);
                    Apply();
                    Save();
                    GameSettingsManager.Instance?.InvokeSettingsApplied();
                }
            }
        }

        // 构造函数：设置键名、默认值，并把当前值初始化为默认值
        protected Setting(string key, T defaultValue)
        {
            Key = key;
            DefaultValue = defaultValue;
            _value = defaultValue;
        }

        // 子类必须实现：保存当前值
        public abstract void Save();
        // 子类必须实现：加载已保存的值
        public abstract void Load();
        // 应用当前值（默认空实现，子类按需覆盖）
        public virtual void Apply() { }

        // 重置为默认值
        public virtual void Reset()
        {
            Value = DefaultValue;
        }
    }

    // These Specific Settings Types all inherit from Setting. They are all based on PlayerPrefs as well.
    /// <summary>
    /// 整型设置：基于 PlayerPrefs 存取 int 值。
    /// </summary>
    public class IntSetting : Setting<int>
    {
        public IntSetting(string key, int defaultValue) : base(key, defaultValue) { }

        public override void Save() => PlayerPrefs.SetInt(Key, Value);
        public override void Load() => Value = PlayerPrefs.GetInt(Key, DefaultValue);
    }

    /// <summary>
    /// 浮点设置：基于 PlayerPrefs 存取 float 值。
    /// </summary>
    public class FloatSetting : Setting<float>
    {
        public FloatSetting(string key, float defaultValue) : base(key, defaultValue) { }

        public override void Save() => PlayerPrefs.SetFloat(Key, Value);
        public override void Load() => Value = PlayerPrefs.GetFloat(Key, DefaultValue);
    }

    /// <summary>
    /// 布尔设置：基于 PlayerPrefs 以 0/1 形式存取 bool 值。
    /// </summary>
    public class BoolSetting : Setting<bool>
    {
        public BoolSetting(string key, bool defaultValue) : base(key, defaultValue) { }

        public override void Save() => PlayerPrefs.SetInt(Key, Value ? 1 : 0);
        public override void Load() => Value = PlayerPrefs.GetInt(Key, DefaultValue ? 1 : 0) == 1;
    }

    /// <summary>
    /// 字符串设置：基于 PlayerPrefs 存取 string 值。
    /// </summary>
    public class StringSetting : Setting<string>
    {
        public StringSetting(string key, string defaultValue) : base(key, defaultValue) { }

        public override void Save() => PlayerPrefs.SetString(Key, Value);
        public override void Load() => Value = PlayerPrefs.GetString(Key, DefaultValue);
    }

    // OTHER SETTINGS ( SPECIFIC IMPLEMENTATIONS LIKE RESOLUTIONS, FRAMERATE, ETC )
    /// <summary>
    /// 分辨率设置：保存所选分辨率的宽、高及刷新率，并在应用时调用 Screen.SetResolution。
    /// </summary>
    public class ResolutionSetting : IntSetting
    {
        // 系统可用的分辨率列表
        private Resolution[] availableResolutions;
        // 关联的全屏设置（应用分辨率时决定是否全屏）
        private BoolSetting fullscreenSetting;

        public ResolutionSetting(string key, int defaultValue, BoolSetting fullscreenSetting)
            : base(key, defaultValue)
        {
            this.fullscreenSetting = fullscreenSetting;
            availableResolutions = Screen.resolutions;

            // Validates default value only if within the length of the Available resolutions Array
            // 若默认索引越界则回退到最后一个可用分辨率
            if (defaultValue < 0 || defaultValue >= availableResolutions.Length)
                Value = availableResolutions.Length - 1;
        }

        // 获取所有可用分辨率
        public Resolution[] GetAvailableResolutions() => availableResolutions;

        public override void Save()
        {
            base.Save();
            if (Value >= 0 && Value < availableResolutions.Length)
            {
                Resolution res = availableResolutions[Value];
                // 分别保存宽、高以及刷新率（Unity 6 前后刷新率的表示方式不同）
                PlayerPrefs.SetInt(Key + "_width", res.width);
                PlayerPrefs.SetInt(Key + "_height", res.height);
#if UNITY_6000_0_OR_NEWER
                PlayerPrefs.SetInt(Key + "_refreshNumerator", (int)res.refreshRateRatio.numerator);
                PlayerPrefs.SetInt(Key + "_refreshDenominator", (int)res.refreshRateRatio.denominator);
#else
                PlayerPrefs.SetInt(Key + "_refreshRate", Mathf.RoundToInt((float)res.refreshRate));
#endif
            }
        }

        public override void Load()
        {
            int width = PlayerPrefs.GetInt(Key + "_width", -1);
            int height = PlayerPrefs.GetInt(Key + "_height", -1);

            // 若已保存过宽高，则按宽高匹配出对应的分辨率索引
            if (width != -1 && height != -1 && availableResolutions.Length > 0)
            {
                for (int i = 0; i < availableResolutions.Length; i++)
                {
                    if (availableResolutions[i].width == width && availableResolutions[i].height == height)
                    {
                        Value = i;
                        return;
                    }
                }
            }

            base.Load();

            // 把索引限制在合法范围内
            if (availableResolutions.Length > 0)
                Value = Mathf.Clamp(Value, 0, availableResolutions.Length - 1);
            else
                Value = -1;
        }

        public override void Apply()
        {
            if (Value >= 0 && Value < availableResolutions.Length)
            {
                Resolution res = availableResolutions[Value];
                Screen.SetResolution(res.width, res.height, fullscreenSetting.Value);
            }
        }
    
    }

    /// <summary>
    /// 帧率设置：从预设帧率选项中选择并应用到 Application.targetFrameRate。
    /// </summary>
    public class FrameRateSetting : IntSetting
    {
        // 可选的帧率上限列表
        private static readonly int[] FrameRateOptions = { 60, 120, 240, 300 };

        public FrameRateSetting(string key, int defaultValue) : base(key, defaultValue) { }

        public override void Apply()
        {
            if (Value >= 0 && Value < FrameRateOptions.Length)
            {
                Application.targetFrameRate = FrameRateOptions[Value];
            }
        }

        // 重置时默认选择最后一个选项（300）
        public override void Reset()
        {
            Value = FrameRateOptions.Length - 1;
        }

        // 获取当前生效的帧率数值
        public int GetCurrentFrameRate() =>
            Value >= 0 && Value < FrameRateOptions.Length ? FrameRateOptions[Value] : 60;
    }

    /// <summary>
    /// 垂直同步设置：应用时设置 QualitySettings.vSyncCount。
    /// </summary>
    public class VSyncSetting : BoolSetting
    {
        public VSyncSetting(string key, bool defaultValue) : base(key, defaultValue) { }

        public override void Apply()
        {
            QualitySettings.vSyncCount = Value ? 1 : 0;
        }
    }

    /// <summary>
    /// 画质设置：应用时设置 QualitySettings.SetQualityLevel。
    /// </summary>
    public class GraphicsQualitySetting : IntSetting
    {
        public GraphicsQualitySetting(string key, int defaultValue) : base(key, defaultValue) { }

        public override void Apply()
        {
            QualitySettings.SetQualityLevel(Value);
        }
    }

    /// <summary>
    /// 音量设置：把线性音量转换为分贝值后设置到 AudioMixer 的指定参数。
    /// </summary>
    public class AudioVolumeSetting : FloatSetting
    {
        // 目标音频混音器
        private UnityEngine.Audio.AudioMixer mixer;
        // 混音器上的参数名
        private string mixerParameter;

        public AudioVolumeSetting(string key, float defaultValue,
            UnityEngine.Audio.AudioMixer mixer, string mixerParameter)
            : base(key, defaultValue)
        {
            this.mixer = mixer;
            this.mixerParameter = mixerParameter;
        }

        public override void Apply()
        {
            if (mixer != null)
            {
                // 线性值转换为分贝：极小值时用 -144dB 表示静音
                float dbValue = Value > 0.001f ? Mathf.Log10(Value) * 20 : -144f;
                mixer.SetFloat(mixerParameter, dbValue);
            }
        }
    }
}