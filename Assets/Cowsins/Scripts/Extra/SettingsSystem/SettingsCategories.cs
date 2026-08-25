// SettingsCategories（设置分类）：
// 定义设置的分类基类 SettingsCategory（统一管理一组设置的保存/加载/重置/应用），
// 并提供三个具体分类：GraphicsSettings（画质）、AudioSettings（音量）、ControlSettings（操作灵敏度/反转）。
using UnityEngine;
using System.Collections.Generic;

namespace cowsins.Settings
{
    // Base class for organizing settings into categories
    /// <summary>
    /// 设置分类基类：把一组设置组织在一起，并提供批量保存/加载/重置/应用。
    /// </summary>
    public abstract class SettingsCategory
    {
        // 分类名称（子类实现）
        public abstract string CategoryName { get; }
        // 该分类下的所有设置项
        protected List<ISetting> settings = new List<ISetting>();

        // 只读暴露设置列表
        public IReadOnlyList<ISetting> Settings => settings.AsReadOnly();

        // 批量保存所有设置并刷新 PlayerPrefs 到磁盘
        public void SaveAll()
        {
            foreach (var setting in settings)
                setting.Save();
            PlayerPrefs.Save();
        }

        // 批量加载所有设置
        public void LoadAll()
        {
            foreach (var setting in settings)
                setting.Load();
        }

        // 批量重置所有设置
        public void ResetAll()
        {
            foreach (var setting in settings)
                setting.Reset();
        }

        // 批量应用所有设置到运行时
        public void ApplyAll()
        {
            foreach (var setting in settings)
                setting.Apply();
        }

        // 注册一个设置项到本分类
        protected void RegisterSetting(ISetting setting)
        {
            settings.Add(setting);
        }
    }

    // CONCRETE CATEGORIES
    /// <summary>
    /// 画质设置分类：全屏、分辨率、帧率、垂直同步、画质等级。
    /// </summary>
    public class GraphicsSettings : SettingsCategory
    {
        public override string CategoryName => "Graphics";

        // 全屏开关
        public BoolSetting Fullscreen { get; private set; }
        // 分辨率
        public ResolutionSetting Resolution { get; private set; }
        // 帧率上限
        public FrameRateSetting FrameRate { get; private set; }
        // 垂直同步
        public VSyncSetting VSync { get; private set; }
        // 画质等级
        public GraphicsQualitySetting Quality { get; private set; }

        // 构造函数：创建各设置项并注册到本分类
        public GraphicsSettings()
        {
            Fullscreen = new BoolSetting("fullScreen", true);
            Resolution = new ResolutionSetting("resolution", Screen.resolutions.Length - 1, Fullscreen);
            FrameRate = new FrameRateSetting("maxFrameRate", 2);
            VSync = new VSyncSetting("vsync", false);
            Quality = new GraphicsQualitySetting("graphicsQuality", 2);

            RegisterSetting(Fullscreen);
            RegisterSetting(Resolution);
            RegisterSetting(FrameRate);
            RegisterSetting(VSync);
            RegisterSetting(Quality);
        }
    }

    /// <summary>
    /// 音频设置分类：主音量。
    /// </summary>
    public class AudioSettings : SettingsCategory
    {
        public override string CategoryName => "Audio";

        // 主音量
        public AudioVolumeSetting MasterVolume { get; private set; }

        // 构造函数：接收主混音器并创建主音量设置
        public AudioSettings(UnityEngine.Audio.AudioMixer masterMixer)
        {
            MasterVolume = new AudioVolumeSetting("masterVolume", 1f, masterMixer, "Volume");
            RegisterSetting(MasterVolume);
        }
    }

    /// <summary>
    /// 操作设置分类：鼠标/手柄灵敏度与 Y 轴反转。
    /// </summary>
    public class ControlSettings : SettingsCategory
    {
        public override string CategoryName => "Controls";

        // 鼠标水平灵敏度
        public FloatSetting MouseSensitivityX { get; private set; }
        // 鼠标垂直灵敏度
        public FloatSetting MouseSensitivityY { get; private set; }
        // 手柄水平灵敏度
        public FloatSetting ControllerSensitivityX { get; private set; }
        // 手柄垂直灵敏度
        public FloatSetting ControllerSensitivityY { get; private set; }
        // 鼠标是否反转 Y 轴
        public BoolSetting InvertY { get; private set; }
        // 手柄是否反转 Y 轴
        public BoolSetting InvertYController { get; private set; }


        // 构造函数：创建各设置项并注册到本分类
        public ControlSettings()
        {
            MouseSensitivityX = new FloatSetting("playerSensX", 4f);
            MouseSensitivityY = new FloatSetting("playerSensY", 4f);
            ControllerSensitivityX = new FloatSetting("playerControllerSensX", 35f);
            ControllerSensitivityY = new FloatSetting("playerControllerSensY", 35f);
            InvertY = new BoolSetting("invertY", false);
            InvertYController = new BoolSetting("invertYController", false);

            RegisterSetting(MouseSensitivityX);
            RegisterSetting(MouseSensitivityY);
            RegisterSetting(ControllerSensitivityX);
            RegisterSetting(ControllerSensitivityY);
            RegisterSetting(InvertY);
            RegisterSetting(InvertYController);
        }
    }
}