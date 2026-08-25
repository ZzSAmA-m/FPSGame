// SaveFieldAttribute.cs：自定义“保存字段”标记特性。
// 通过 [SaveField] 标注希望被序列化/保存系统识别的字段，
// 本身不包含任何逻辑，仅作为元数据供反射或编辑器工具使用。
using System;

namespace cowsins
{
    /// <summary>
    /// 自定义“保存字段”标记特性：用于标注需要被保存/序列化系统识别的字段。
    /// 特性本身不含任何逻辑，仅作为供反射或编辑器工具读取的元数据标记。
    /// </summary>
    [AttributeUsage(AttributeTargets.Field)]
    public class SaveFieldAttribute : Attribute
    {
    }
}
