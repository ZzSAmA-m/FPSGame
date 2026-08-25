// ============================================================================
// StatModifierType.cs —— 属性修饰器类型枚举。
// 定义属性修饰器的两种类型：加成型（Additive）与乘法型（Multiplicative），
// 用于 ModifiableStat 在计算最终值时区分不同的叠加规则。
// ============================================================================
using System;

namespace cowsins
{
    /// <summary>
    /// 属性修饰器类型：加成型直接累加，乘法型按百分比整体相乘。
    /// </summary>
    public enum StatModifierType
    {
        Additive, // 加成型：直接加到基础值上
        Multiplicative // 乘法型：作为百分比参与乘法计算
    }
}
