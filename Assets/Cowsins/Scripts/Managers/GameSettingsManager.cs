// 文件：GameSettingsManager.cs
// 作用：游戏设置管理器：统一管理图形、音频、控制三大类设置（SettingsCategory 派生类），
//       负责它们的初始化、注册、加载、保存、应用与重置，并通过事件通知设置变化。
using UnityEngine;
using UnityEngine.Audio;
using System;
using System.Collections.Generic;


namespace cowsins.Settings
{
    /// <summary>
    /// 游戏设置管理器：集中管理并持久化各类设置，提供加载/保存/应用/重置的统一入口。
    /// </summary>
    public class GameSettingsManager : MonoBehaviour
    {
        // 单例实例。
        public static GameSettingsManager Instance { get; private set; }

        // 音频混音器引用，供音频设置写入音量参数。
        [SerializeField] private AudioMixer masterMixer;

        // 按类型索引的设置类别字典，用于统一遍历各设置类别。
        private Dictionary<Type, SettingsCategory> categories = new Dictionary<Type, SettingsCategory>();

        // 各类设置对象（图形 / 音频 / 控制）。
        public GraphicsSettings Graphics { get; private set; }
        public AudioSettings Audio { get; private set; }
        public ControlSettings Controls { get; private set; }

        // 设置相关事件：加载完成 / 重置完成 / 应用完成。
        public event Action OnSettingsLoaded;
        public event Action OnSettingsReset;
        public event Action OnSettingsApplied;

        // 初始化单例：若已存在其它实例则销毁自身，否则设为 Instance，并完成设置类别的初始化与加载。
        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;

            InitializeCategories();
            LoadAllSettings();
        }

        // 启动时应用所有已加载的设置，使其生效。
        private void Start()
        {
            ApplyAllSettings();
        }

        // 实例化三类设置对象并注册到字典中。
        private void InitializeCategories()
        {
            Graphics = new GraphicsSettings();
            Audio = new AudioSettings(masterMixer);
            Controls = new ControlSettings();

            RegisterCategory(Graphics);
            RegisterCategory(Audio);
            RegisterCategory(Controls);
        }

        // 将设置类别按其类型注册到字典中，便于后续按类型查找。
        private void RegisterCategory(SettingsCategory category)
        {
            categories[category.GetType()] = category;
        }

        /// <summary>
        /// 按类型获取指定设置类别实例。
        /// </summary>
        /// <typeparam name="T">SettingsCategory 的派生类型。</typeparam>
        /// <returns>对应类型的设置类别实例；未注册时返回 null。</returns>
        public T GetCategory<T>() where T : SettingsCategory
        {
            return categories.TryGetValue(typeof(T), out var category) ? category as T : null;
        }

        /// <summary>
        /// 保存所有设置类别。
        /// </summary>
        public void SaveAllSettings()
        {
            foreach (var category in categories.Values)
            {
                category.SaveAll();
            }
        }

        /// <summary>
        /// 加载并应用所有设置类别，完成后触发 OnSettingsLoaded 事件。
        /// </summary>
        public void LoadAllSettings()
        {
            foreach (var category in categories.Values)
            {
                category.LoadAll();
                category.ApplyAll();
            }
            OnSettingsLoaded?.Invoke();
        }

        /// <summary>
        /// 将所有设置重置为默认值并应用，随后保存并触发 OnSettingsReset 事件。
        /// </summary>
        public void ResetAllSettings()
        {
            foreach (var category in categories.Values)
            {
                category.ResetAll();
                category.ApplyAll();
            }
            SaveAllSettings();
            OnSettingsReset?.Invoke();
        }

        /// <summary>
        /// 应用所有设置类别，完成后触发 OnSettingsApplied 事件。
        /// </summary>
        public void ApplyAllSettings()
        {
            foreach (var category in categories.Values)
            {
                category.ApplyAll();
            }
            OnSettingsApplied?.Invoke();
        }

        /// <summary>
        /// 手动触发一次“设置已应用”事件。
        /// </summary>
        public void InvokeSettingsApplied() => OnSettingsApplied?.Invoke();

        // 应用退出时保存所有设置。
        private void OnApplicationQuit()
        {
            SaveAllSettings();
        }
    }
}
