// =====================================================================
// IPlayerStatsProvider.cs
// 作用：定义“玩家基础属性提供者”接口及其事件提供者、事件容器。
// IPlayerStatsProvider 由 PlayerStats 实现并被 PlayerDependencies 引用，
// 提供生命值/护盾/死亡状态的读取与治疗操作，以及死亡事件的订阅接口；
// PlayerStatsEvents 则承载生命初始化与生命变化等 Unity 事件。
// =====================================================================
using System;
using UnityEngine.Events;

namespace cowsins
{
    // Implemented by PlayerStats and required by PlayerDependencies
    /// <summary>
    /// 玩家基础属性提供者接口：约定实现方提供生命值、护盾、死亡状态及相关事件订阅能力。
    /// </summary>
    public interface IPlayerStatsProvider
    {
        /// <summary>获取玩家当前生命值。</summary>
        float Health { get; }
        /// <summary>获取玩家最大生命值。</summary>
        float MaxHealth { get; }
        /// <summary>获取玩家当前护盾值。</summary>
        float Shield { get; }
        /// <summary>获取玩家最大护盾值。</summary>
        float MaxShield { get; }
        /// <summary>获取玩家当前是否已死亡。</summary>
        bool IsDead { get; }
        /// <summary>获取玩家死亡时是否应被冻结（禁用物理，使其静止）。</summary>
        bool FreezePlayerOnDeath { get; }
        /// <summary>
        /// 为玩家恢复指定数值的生命值（超出部分会按实现方规则转为护盾或忽略）。
        /// </summary>
        /// <param name="amount">要恢复的生命值数量。</param>
        void Heal(float amount);
        /// <summary>
        /// 判断玩家是否已完全回复到满生命值。
        /// </summary>
        /// <returns>若生命值（及护盾）已满则返回 true，否则返回 false。</returns>
        bool IsFullyHealed();

        /// <summary>
        /// 注册一个在玩家死亡时被调用的回调。
        /// </summary>
        /// <param name="callback">死亡时触发的回调委托。</param>
        void AddOnDieListener(Action callback);
        /// <summary>
        /// 移除之前注册的死亡回调。
        /// </summary>
        /// <param name="callback">要移除的死亡回调委托。</param>
        void RemoveOnDieListener(Action callback);
    }

    /// <summary>
    /// 玩家属性事件提供者接口：约定实现方暴露一组可订阅的玩家属性事件。
    /// </summary>
    public interface IPlayerStatsEventsProvider
    {
        /// <summary>获取玩家属性事件集合。</summary>
        PlayerStatsEvents Events { get; }
    }

    /// <summary>
    /// 玩家属性事件容器：集中存放与玩家属性相关的 Unity 事件，供外部（如 UI）订阅。
    /// </summary>
    public class PlayerStatsEvents
    {
        /// <summary>生命值初始化时触发的事件，回调参数为实现该接口的玩家属性提供者。</summary>
        public UnityEvent<IPlayerStatsProvider> OnInitializeHealth = new UnityEvent<IPlayerStatsProvider>();
        /// <summary>生命/护盾变化时触发的事件，回调参数依次为：当前生命值、当前护盾值、是否受到伤害。</summary>
        public UnityEvent<float, float, bool> OnHealthChanged = new UnityEvent<float, float, bool>();
    }
}