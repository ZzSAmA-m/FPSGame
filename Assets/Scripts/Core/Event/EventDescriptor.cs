using System;
using System.Text;

namespace Core.Event
{
    /// <summary>保存一个事件固定的身份和参数签名。</summary>
    internal sealed class EventDescriptor
    {
        /// <summary>全局唯一的事件编号。</summary>
        internal int Id { get; }

        /// <summary>注册时生成的日志描述。</summary>
        internal string Description { get; }

        /// <summary>创建不可变的事件定义，只在注册时格式化签名。</summary>
        internal EventDescriptor(int id, Type callbackType)
        {
            Id = id;
            var text = new StringBuilder();
            text.Append(id).Append('(');
            Type[] arguments = callbackType.GenericTypeArguments;
            for (int i = 0; i < arguments.Length; i++)
            {
                if (i > 0) text.Append(", ");
                text.Append(arguments[i].FullName ?? arguments[i].Name);
            }
            Description = text.Append(')').ToString();
        }
    }
}
