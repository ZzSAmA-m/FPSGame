// =====================================================================
// IPlayerMultipliers.cs
// 作用：定义“玩家属性倍率”接口。
// 实现方（PlayerMultipliers）维护伤害/治疗/负重等可修改属性倍率，
// 并支持添加带持续时间的临时修饰器、按来源移除修饰器，
// 供伤害计算、治疗效果、负重影响等系统统一查询与调整。
// =====================================================================
using System.Collections.Generic;

namespace cowsins
{
    /// <summary>
    /// 玩家属性倍率接口：约定实现方提供可叠加修改的属性倍率以及临时修饰器管理能力。
    /// </summary>
    public interface IPlayerMultipliers
    {
        /// <summary>
        /// 获取当前所有处于激活状态的临时修饰器（只读列表）。
        /// </summary>
        IReadOnlyList<PlayerMultipliers.TemporaryModifierData> ActiveTemporaryModifiers { get; }
        
        /// <summary>
        /// 获取伤害倍率这一可修改属性。
        /// </summary>
        ModifiableStat DamageMultiplier { get; }
        /// <summary>
        /// 获取治疗倍率这一可修改属性。
        /// </summary>
        ModifiableStat HealMultiplier { get; }
        /// <summary>
        /// 获取负重倍率这一可修改属性。
        /// </summary>
        ModifiableStat WeightMultiplier { get; }

        /// <summary>
        /// 为指定属性添加一个持续指定时长的临时修饰器。
        /// </summary>
        /// <param name="stat">要应用修饰器的目标属性。</param>
        /// <param name="modifier">要应用的属性修饰器。</param>
        /// <param name="duration">修饰器持续时长（秒）。</param>
        void AddTemporaryModifier(ModifiableStat stat, StatModifier modifier, float duration);
        /// <summary>
        /// 移除由指定来源对象添加的所有修饰器。
        /// </summary>
        /// <param name="source">修饰器的来源对象。</param>
        void RemoveModifierFromSource(object source);
    }
}