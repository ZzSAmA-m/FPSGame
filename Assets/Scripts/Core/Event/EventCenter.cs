using System;
using System.Threading;
using Core.ZLog;

namespace Core.Event
{
    /// <summary>按整数编号注册、订阅和同步发布强类型事件。</summary>
    public static class EventCenter
    {
        /// <summary>全局事件注册表。</summary>
        private static readonly EventRegistry Registry = new EventRegistry();

        /// <summary>由主线程首次初始化时建立的线程标识。</summary>
        private static int _mainThreadId;

        /// <summary>所有事件当前的嵌套发布深度。</summary>
        private static int _publishDepth;

        /// <summary>下一次订阅使用的诊断编号。</summary>
        private static ulong _subscriptionId;

        /// <summary>下一次发布使用的诊断编号。</summary>
        private static ulong _publishId;

        /// <summary>必须由 Unity 主线程初始化，清除全部定义和监听。</summary>
        public static bool Reset()
        {
            if (_mainThreadId == 0) _mainThreadId = Thread.CurrentThread.ManagedThreadId;
            if (!CheckAccess()) return false;
            if (_publishDepth != 0)
            {
                EventLogger.Error("[事件正在发布，拒绝重置注册表]");
                return false;
            }
            Registry.Clear();
            _subscriptionId = 0;
            _publishId = 0;
            return true;
        }

        /// <summary>检查当前操作是否在初始化过的主线程中执行。</summary>
        private static bool CheckAccess()
        {
            if (_mainThreadId == 0)
            {
                EventLogger.Error("[事件系统尚未初始化，请由主线程先执行 Reset 和注册]");
                return false;
            }
            if (Thread.CurrentThread.ManagedThreadId != _mainThreadId)
            {
                EventLogger.Error("[事件操作必须在 Unity 主线程执行]");
                return false;
            }
            return true;
        }

        /// <summary>验证注册身份，幂等调用保留现有通道和所有监听。</summary>
        private static bool PrepareRegistration(int id, string name, Type callbackType, out bool existing)
        {
            existing = false;
            if (!CheckAccess()) return false;
            if (string.IsNullOrWhiteSpace(name))
            {
                EventLogger.Error("[事件名称为空，注册已拒绝]");
                return false;
            }
            if (!Registry.TryGet(id, out IEventChannel channel)) return true;
            EventDescriptor descriptor = channel.Descriptor;
            if (descriptor.CallbackType != callbackType || !string.Equals(descriptor.Name, name, StringComparison.Ordinal))
            {
                EventLogger.Mismatch(descriptor, name, callbackType);
                return false;
            }
            existing = true;
            EventLogger.Registered(descriptor, true);
            return true;
        }

        /// <summary>查找注册项，不自动创建未注册事件。</summary>
        private static bool Find(int id, out IEventChannel channel)
        {
            channel = null;
            if (!CheckAccess()) return false;
            if (Registry.TryGet(id, out channel)) return true;
            EventLogger.Missing(id);
            return false;
        }

        /// <summary>按调用处声明的完整泛型签名查找通道。</summary>
        private static TChannel Find<TChannel>(int id, Type callbackType) where TChannel : class, IEventChannel
        {
            if (!Find(id, out IEventChannel channel)) return null;
            if (channel is TChannel typed) return typed;
            EventLogger.Mismatch(channel.Descriptor, channel.Descriptor.Name, callbackType);
            return null;
        }

        /// <summary>检查事件是否已经注册。</summary>
        public static bool IsRegistered(int id) => CheckAccess() && Registry.TryGet(id, out _);

        /// <summary>读取有效监听数量，未注册时返回负一并报错。</summary>
        public static int GetListenerCount(int id) => Find(id, out IEventChannel channel) ? channel.ListenerCount : -1;

        /// <summary>配置发布频率分类和逐方法明细，不影响错误日志。</summary>
        public static bool ConfigurePublishLog(int id, LogFrequency frequency, bool detailed)
        {
            if (!Find(id, out IEventChannel channel)) return false;
            if (frequency != LogFrequency.Medium && frequency != LogFrequency.High && frequency != LogFrequency.Extreme)
            {
                EventLogger.Error("[发布日志频率只允许 Medium、High 或 Extreme]");
                return false;
            }
            channel.LogOptions.Frequency = frequency;
            channel.LogOptions.Detailed = detailed;
            return true;
        }

        /// <summary>分配当前运行中的订阅编号。</summary>
        internal static ulong NextSubscriptionId() => ++_subscriptionId;

        /// <summary>分配当前运行中的发布编号。</summary>
        internal static ulong NextPublishId() => ++_publishId;

        /// <summary>记录开始分发，阻止回调中重置全局注册。</summary>
        internal static void EnterPublish() => _publishDepth++;

        /// <summary>记录分发结束。</summary>
        internal static void ExitPublish() => _publishDepth--;

        /// <summary>注册 0 参数事件，同身份重复注册返回成功。</summary>
        public static bool Register(int id, string name)
        {
            if (!PrepareRegistration(id, name, typeof(Action), out bool existing)) return false;
            if (existing) return true;
            var channel = new EventChannel(new EventDescriptor(id, name, typeof(Action)));
            Registry.Add(channel);
            EventLogger.Registered(channel.Descriptor, false);
            return true;
        }

        /// <summary>添加 0 参数监听，重复或无效回调返回失败。</summary>
        public static bool AddListener(int id, Action listener, object owner = null)
        {
            EventChannel channel = Find<EventChannel>(id, typeof(Action));
            return channel != null && channel.Add(listener, owner);
        }

        /// <summary>移除 0 参数监听，仅实际移除时返回成功。</summary>
        public static bool RemoveListener(int id, Action listener)
        {
            EventChannel channel = Find<EventChannel>(id, typeof(Action));
            return channel != null && channel.Remove(listener);
        }

        /// <summary>同步发布 0 参数事件，正常热路径不创建参数数组。</summary>
        public static void Publish(int id)
        {
            EventChannel channel = Find<EventChannel>(id, typeof(Action));
            if (channel != null) channel.Publish();
        }

        /// <summary>注册 1 参数事件，同身份重复注册返回成功。</summary>
        public static bool Register<T1>(int id, string name)
        {
            if (!PrepareRegistration(id, name, typeof(Action<T1>), out bool existing)) return false;
            if (existing) return true;
            var channel = new EventChannel<T1>(new EventDescriptor(id, name, typeof(Action<T1>)));
            Registry.Add(channel);
            EventLogger.Registered(channel.Descriptor, false);
            return true;
        }

        /// <summary>添加 1 参数监听，重复或无效回调返回失败。</summary>
        public static bool AddListener<T1>(int id, Action<T1> listener, object owner = null)
        {
            EventChannel<T1> channel = Find<EventChannel<T1>>(id, typeof(Action<T1>));
            return channel != null && channel.Add(listener, owner);
        }

        /// <summary>移除 1 参数监听，仅实际移除时返回成功。</summary>
        public static bool RemoveListener<T1>(int id, Action<T1> listener)
        {
            EventChannel<T1> channel = Find<EventChannel<T1>>(id, typeof(Action<T1>));
            return channel != null && channel.Remove(listener);
        }

        /// <summary>同步发布 1 参数事件，正常热路径不创建参数数组。</summary>
        public static void Publish<T1>(int id, T1 arg1)
        {
            EventChannel<T1> channel = Find<EventChannel<T1>>(id, typeof(Action<T1>));
            if (channel != null) channel.Publish(arg1);
        }

        /// <summary>注册 2 参数事件，同身份重复注册返回成功。</summary>
        public static bool Register<T1, T2>(int id, string name)
        {
            if (!PrepareRegistration(id, name, typeof(Action<T1, T2>), out bool existing)) return false;
            if (existing) return true;
            var channel = new EventChannel<T1, T2>(new EventDescriptor(id, name, typeof(Action<T1, T2>)));
            Registry.Add(channel);
            EventLogger.Registered(channel.Descriptor, false);
            return true;
        }

        /// <summary>添加 2 参数监听，重复或无效回调返回失败。</summary>
        public static bool AddListener<T1, T2>(int id, Action<T1, T2> listener, object owner = null)
        {
            EventChannel<T1, T2> channel = Find<EventChannel<T1, T2>>(id, typeof(Action<T1, T2>));
            return channel != null && channel.Add(listener, owner);
        }

        /// <summary>移除 2 参数监听，仅实际移除时返回成功。</summary>
        public static bool RemoveListener<T1, T2>(int id, Action<T1, T2> listener)
        {
            EventChannel<T1, T2> channel = Find<EventChannel<T1, T2>>(id, typeof(Action<T1, T2>));
            return channel != null && channel.Remove(listener);
        }

        /// <summary>同步发布 2 参数事件，正常热路径不创建参数数组。</summary>
        public static void Publish<T1, T2>(int id, T1 arg1, T2 arg2)
        {
            EventChannel<T1, T2> channel = Find<EventChannel<T1, T2>>(id, typeof(Action<T1, T2>));
            if (channel != null) channel.Publish(arg1, arg2);
        }

        /// <summary>注册 3 参数事件，同身份重复注册返回成功。</summary>
        public static bool Register<T1, T2, T3>(int id, string name)
        {
            if (!PrepareRegistration(id, name, typeof(Action<T1, T2, T3>), out bool existing)) return false;
            if (existing) return true;
            var channel = new EventChannel<T1, T2, T3>(new EventDescriptor(id, name, typeof(Action<T1, T2, T3>)));
            Registry.Add(channel);
            EventLogger.Registered(channel.Descriptor, false);
            return true;
        }

        /// <summary>添加 3 参数监听，重复或无效回调返回失败。</summary>
        public static bool AddListener<T1, T2, T3>(int id, Action<T1, T2, T3> listener, object owner = null)
        {
            EventChannel<T1, T2, T3> channel = Find<EventChannel<T1, T2, T3>>(id, typeof(Action<T1, T2, T3>));
            return channel != null && channel.Add(listener, owner);
        }

        /// <summary>移除 3 参数监听，仅实际移除时返回成功。</summary>
        public static bool RemoveListener<T1, T2, T3>(int id, Action<T1, T2, T3> listener)
        {
            EventChannel<T1, T2, T3> channel = Find<EventChannel<T1, T2, T3>>(id, typeof(Action<T1, T2, T3>));
            return channel != null && channel.Remove(listener);
        }

        /// <summary>同步发布 3 参数事件，正常热路径不创建参数数组。</summary>
        public static void Publish<T1, T2, T3>(int id, T1 arg1, T2 arg2, T3 arg3)
        {
            EventChannel<T1, T2, T3> channel = Find<EventChannel<T1, T2, T3>>(id, typeof(Action<T1, T2, T3>));
            if (channel != null) channel.Publish(arg1, arg2, arg3);
        }

    }
}
