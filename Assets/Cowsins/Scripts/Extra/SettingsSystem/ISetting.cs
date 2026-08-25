// ISetting（设置项接口定义）：
// 定义设置系统的最基础接口与泛型接口。所有具体设置类型都实现这些接口，
// 统一提供键名、保存/加载/重置/应用，以及泛型值与值变更事件，方便设置 UI 与存储层解耦。
using System;

namespace cowsins.Settings
{
    // Base interface for all settings
    /// <summary>
    /// 所有设置项的基础接口：定义键名以及保存、加载、重置、应用四个核心操作。
    /// </summary>
    public interface ISetting
    {
        // 设置的唯一键名（用于持久化存储）
        string Key { get; }
        // 保存当前值
        void Save();
        // 加载已保存的值
        void Load();
        // 重置为默认值
        void Reset();
        // 将当前值应用到运行时（如画面、音量等）
        void Apply();
    }

    // Generic setting interface
    /// <summary>
    /// 泛型设置接口：在基础接口之上增加带类型的值、默认值以及值变更事件。
    /// </summary>
    public interface ISetting<T> : ISetting
    {
        // 当前值（可读写，赋值时触发变更逻辑）
        T Value { get; set; }
        // 默认值
        T DefaultValue { get; }
        // 值发生变化时触发的事件
        event Action<T> OnValueChanged;
    }
}