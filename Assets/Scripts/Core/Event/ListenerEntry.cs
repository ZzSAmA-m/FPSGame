using System;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace Core.Event
{
    /// <summary>保存原始回调和注册时缓存的诊断信息。</summary>
    internal sealed class ListenerEntry<TCallback> where TCallback : Delegate
    {
        /// <summary>本次订阅的诊断编号。</summary>
        internal readonly ulong Id;

        /// <summary>用于调用、判重和退订的原始委托。</summary>
        internal readonly TCallback Callback;

        /// <summary>订阅的所属对象，仅用于归属说明。</summary>
        internal readonly object Owner;

        /// <summary>缓存的对象和完整方法描述。</summary>
        internal readonly string Description;

        /// <summary>是否仍允许该订阅参与分发。</summary>
        internal bool Active = true;

        /// <summary>回调直接绑定的 Unity 对象。</summary>
        private readonly UnityEngine.Object _unityTarget;

        /// <summary>回调是否直接绑定 Unity 对象。</summary>
        private readonly bool _hasUnityTarget;

        /// <summary>回调直接绑定的 Unity 对象是否已经销毁。</summary>
        internal bool TargetDestroyed => _hasUnityTarget && _unityTarget == null;

        /// <summary>创建订阅，反射和字符串格式化只在这里进行。</summary>
        internal ListenerEntry(ulong id, TCallback callback, object owner)
        {
            Id = id;
            Callback = callback;
            Owner = owner ?? callback.Target;
            _unityTarget = callback.Target as UnityEngine.Object;
            _hasUnityTarget = !ReferenceEquals(_unityTarget, null);
            var method = callback.Method;
            string type = method.DeclaringType?.FullName ?? "<未知类型>";
            Description = $"订阅#{id} | 所属：{DescribeOwner(Owner)} | 方法：{type}.{method.Name}";
        }

        /// <summary>区分 Unity 实例、普通对象和静态回调。</summary>
        private static string DescribeOwner(object owner)
        {
            if (owner == null) return "<静态方法>";
            if (owner is UnityEngine.Object unityObject)
                return unityObject == null
                    ? $"<已销毁的 {owner.GetType().FullName}>"
                    : $"{unityObject.name} / {owner.GetType().FullName}#{unityObject.GetEntityId()}";
            return $"{owner.GetType().FullName}#{RuntimeHelpers.GetHashCode(owner)}";
        }
    }
}
