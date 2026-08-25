// ============================================================================
// StatModifier.cs —— 属性修饰器。
// 表示对某个 ModifiableStat 的一次增减或百分比调整：包含修饰值、修饰类型
// （加成型/乘法型）以及来源对象。来源对象用于后续按来源精确移除修饰器。
// ============================================================================
namespace cowsins
{
    /// <summary>
    /// 属性修饰器：描述一次属性调整的值、类型与来源，用于 ModifiableStat 的计算与移除。
    /// </summary>
    public class StatModifier
    {
        public readonly float Value; // 修饰数值
        public readonly StatModifierType Type; // 修饰类型：加成型或乘法型
        // 修饰来源，用于识别并移除特定 buff/debuff
        public readonly object Source; // Used to identify and remove specific buffs

        // 构造函数：设置修饰值、类型与来源（来源可为空）。
        public StatModifier(float value, StatModifierType type, object source = null)
        {
            Value = value;
            Type = type;
            Source = source;
        }
    }
}
