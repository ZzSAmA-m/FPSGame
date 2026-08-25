// ============================================================================
// ModifiableStat.cs —— 可被修饰器修改的属性值。
// 维护一个基础值（BaseValue）与一组属性修饰器（StatModifier），通过「脏标记」
// 惰性计算最终值：加成型修饰器直接累加，乘法型修饰器彼此加和后再整体相乘，
// 结果会被缓存，直到基础值或修饰器发生变化。
// ============================================================================
using System;
using System.Collections.Generic;

namespace cowsins
{
    /// <summary>
    /// 可被修饰器修改的属性：封装基础值与修饰器列表，按需计算并缓存最终值。
    /// </summary>
    public class ModifiableStat
    {
        private float _baseValue; // 基础值
        private float _lastCalculatedValue; // 上次计算得到的最终值（缓存）
        private bool _isDirty = true; // 脏标记：为 true 表示需要重新计算最终值
        private readonly List<StatModifier> _statModifiers; // 当前生效的属性修饰器列表

        public IReadOnlyList<StatModifier> StatModifiers => _statModifiers; // 只读暴露修饰器列表

        public float BaseValue // 基础值属性：修改时标记为脏，触发重算
        {
            get => _baseValue;
            set
            {
                _baseValue = value;
                _isDirty = true;
            }
        }

        // 构造函数：设置基础值并初始化修饰器列表。
        public ModifiableStat(float baseValue)
        {
            _baseValue = baseValue;
            _statModifiers = new List<StatModifier>();
        }

        public float Value // 最终值属性：懒计算，仅在脏标记为真时重算并缓存
        {
            get
            {
                if (!_isDirty) return _lastCalculatedValue; // 未变化时直接返回缓存值
                _lastCalculatedValue = CalculateFinalValue();
                _isDirty = false;
                return _lastCalculatedValue;
            }
        }

        // 添加一个修饰器并标记为脏。
        public void AddModifier(StatModifier modifier)
        {
            _statModifiers.Add(modifier);
            _isDirty = true;
        }

        // 根据来源移除所有匹配的修饰器；返回是否真的移除过，并更新脏标记。
        public bool RemoveModifierFromSource(object source)
        {
            bool removed = _statModifiers.RemoveAll(mod => mod.Source == source) > 0;
            if (removed) _isDirty = true;
            return removed;
        }

        // 计算最终值：先累加加成型修饰器，再汇总乘法型修饰器百分比，
        // 最后将（基础值 + 加成型）乘以（1 + 乘法型总和）。
        private float CalculateFinalValue()
        {
            float finalValue = _baseValue;
            float sumPercentAdd = 0; // 乘法型修饰器百分比累加和

            foreach (StatModifier mod in _statModifiers)
            {
                switch (mod.Type)
                {
                    case StatModifierType.Additive:
                        finalValue += mod.Value; // 加成型：直接加到当前值上
                        break;
                    case StatModifierType.Multiplicative:
                        sumPercentAdd += mod.Value; // 乘法型：先累加百分比
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(); // 未知修饰器类型，抛出异常
                }
            }

            // Multiplicative modifiers are stacked additively with each other
            // 乘法型修饰器之间彼此相加后再整体相乘
            return finalValue * (1 + sumPercentAdd);
        }
    }
}
