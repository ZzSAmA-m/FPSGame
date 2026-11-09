using System;
using System.Collections.Generic;

namespace Core.Event
{
    /// <summary>复用监听列表，在分发结束后原地清理失效项。</summary>
    internal abstract class EventChannelBase<TCallback> : IEventChannel where TCallback : Delegate
    {
        /// <summary>只在订阅时增长的有序监听列表。</summary>
        protected readonly List<ListenerEntry<TCallback>> Listeners = new List<ListenerEntry<TCallback>>(4);

        /// <summary>当前有效的监听数量，不包含待清理项。</summary>
        private int _listenerCount;

        /// <summary>是否正在发布当前事件。</summary>
        private bool _publishing;

        /// <summary>本轮结束后是否需要压缩监听列表。</summary>
        private bool _needsCompaction;

        /// <summary>固定事件定义。</summary>
        public EventDescriptor Descriptor { get; }

        /// <summary>当前发布日志配置。</summary>
        public EventLogOptions LogOptions { get; } = new EventLogOptions();

        /// <summary>当前有效的监听数量。</summary>
        public int ListenerCount => _listenerCount;

        /// <summary>创建一个指定签名的通道。</summary>
        protected EventChannelBase(EventDescriptor descriptor) => Descriptor = descriptor;

        /// <summary>添加单个回调，按原始委托相等性拒绝重复。</summary>
        internal bool Add(TCallback callback, object owner)
        {
            if (!ValidateCallback(callback)) return false;
            for (int i = 0; i < Listeners.Count; i++)
            {
                ListenerEntry<TCallback> entry = Listeners[i];
                if (entry.Active && entry.Callback.Equals(callback))
                {
                    EventLogger.Duplicate(Descriptor, entry.Description);
                    return false;
                }
            }
            var added = new ListenerEntry<TCallback>(EventCenter.NextSubscriptionId(), callback, owner);
            Listeners.Add(added);
            _listenerCount++;
            EventLogger.ListenerChanged(Descriptor, added.Description, true, _listenerCount);
            return true;
        }

        /// <summary>移除单个回调，发布期间保留槽位以免影响当前循环。</summary>
        internal bool Remove(TCallback callback)
        {
            if (!ValidateCallback(callback)) return false;
            for (int i = 0; i < Listeners.Count; i++)
            {
                ListenerEntry<TCallback> entry = Listeners[i];
                if (!entry.Active || !entry.Callback.Equals(callback)) continue;
                entry.Active = false;
                _listenerCount--;
                if (_publishing) _needsCompaction = true;
                else Listeners.RemoveAt(i);
                EventLogger.ListenerChanged(Descriptor, entry.Description, false, _listenerCount);
                return true;
            }
            EventLogger.ListenerMissing(Descriptor, callback);
            return false;
        }

        /// <summary>仅在订阅或解订阅时检查空值和多播委托。</summary>
        private static bool ValidateCallback(TCallback callback)
        {
            if (callback == null)
            {
                EventLogger.Error("[事件回调为空，操作已拒绝]");
                return false;
            }
            if (callback.GetInvocationList().Length != 1)
            {
                EventLogger.Error("[不接受多播委托，请分别添加或移除每个回调]");
                return false;
            }
            return true;
        }

        /// <summary>锁定当前事件，并捕获本轮允许访问的列表长度。</summary>
        protected bool BeginPublish(out int count, out PublishTrace trace)
        {
            count = 0;
            trace = default;
            if (_publishing)
            {
                EventLogger.Error("[同一事件递归发布，已拒绝]");
                return false;
            }
            _publishing = true;
            EventCenter.EnterPublish();
            count = Listeners.Count;
            trace = new PublishTrace(EventCenter.NextPublishId(), LogOptions);
            return true;
        }

        /// <summary>检查本轮项是否仍可调用，自动清理已销毁的直接目标。</summary>
        protected bool CanInvoke(ListenerEntry<TCallback> entry, in PublishTrace trace)
        {
            if (!entry.Active)
            {
                EventLogger.Skipped(trace, entry.Description, false);
                return false;
            }
            if (entry.TargetDestroyed)
            {
                entry.Active = false;
                _listenerCount--;
                _needsCompaction = true;
                EventLogger.Skipped(trace, entry.Description, true);
                return false;
            }
            return true;
        }

        /// <summary>结束发布，原地压缩列表且不创建快照数组。</summary>
        protected void EndPublish()
        {
            if (_needsCompaction)
            {
                int write = 0;
                for (int read = 0; read < Listeners.Count; read++)
                {
                    ListenerEntry<TCallback> entry = Listeners[read];
                    if (entry.Active) Listeners[write++] = entry;
                }
                Listeners.RemoveRange(write, Listeners.Count - write);
                _needsCompaction = false;
            }
            _publishing = false;
            EventCenter.ExitPublish();
        }

        /// <summary>由注册表在非发布阶段移除全部监听。</summary>
        public void Clear()
        {
            Listeners.Clear();
            _listenerCount = 0;
            _needsCompaction = false;
        }
    }

    /// <summary>按原始类型同步分发 0 个参数，不装箱参数。</summary>
    internal sealed class EventChannel : EventChannelBase<Action>
    {
        /// <summary>创建 0 参数事件通道。</summary>
        internal EventChannel(EventDescriptor descriptor) : base(descriptor) { }

        /// <summary>逐条调用固定范围内的监听，异常不阻断后续通知。</summary>
        internal void Publish()
        {
            if (!BeginPublish(out int count, out PublishTrace trace)) return;
            int success = 0, failure = 0, skipped = 0;
            try
            {
                EventLogger.PublishStarted(Descriptor, trace, ListenerCount);
                for (int i = 0; i < count; i++)
                {
                    ListenerEntry<Action> entry = Listeners[i];
                    if (!CanInvoke(entry, trace)) { skipped++; continue; }
                    try
                    {
                        entry.Callback();
                        success++;
                    }
                    catch (Exception exception)
                    {
                        failure++;
                        EventLogger.Failed(Descriptor, trace, entry.Description, exception);
                        continue;
                    }
                    EventLogger.Succeeded(trace, entry.Description);
                }
            }
            finally
            {
                try { EventLogger.PublishFinished(Descriptor, trace, success, failure, skipped); }
                finally { EndPublish(); }
            }
        }
    }

    /// <summary>按原始类型同步分发 1 个参数，不装箱参数。</summary>
    internal sealed class EventChannel<T1> : EventChannelBase<Action<T1>>
    {
        /// <summary>创建 1 参数事件通道。</summary>
        internal EventChannel(EventDescriptor descriptor) : base(descriptor) { }

        /// <summary>逐条调用固定范围内的监听，异常不阻断后续通知。</summary>
        internal void Publish(T1 arg1)
        {
            if (!BeginPublish(out int count, out PublishTrace trace)) return;
            int success = 0, failure = 0, skipped = 0;
            try
            {
                EventLogger.PublishStarted(Descriptor, trace, ListenerCount);
                for (int i = 0; i < count; i++)
                {
                    ListenerEntry<Action<T1>> entry = Listeners[i];
                    if (!CanInvoke(entry, trace)) { skipped++; continue; }
                    try
                    {
                        entry.Callback(arg1);
                        success++;
                    }
                    catch (Exception exception)
                    {
                        failure++;
                        EventLogger.Failed(Descriptor, trace, entry.Description, exception);
                        continue;
                    }
                    EventLogger.Succeeded(trace, entry.Description);
                }
            }
            finally
            {
                try { EventLogger.PublishFinished(Descriptor, trace, success, failure, skipped); }
                finally { EndPublish(); }
            }
        }
    }

    /// <summary>按原始类型同步分发 2 个参数，不装箱参数。</summary>
    internal sealed class EventChannel<T1, T2> : EventChannelBase<Action<T1, T2>>
    {
        /// <summary>创建 2 参数事件通道。</summary>
        internal EventChannel(EventDescriptor descriptor) : base(descriptor) { }

        /// <summary>逐条调用固定范围内的监听，异常不阻断后续通知。</summary>
        internal void Publish(T1 arg1, T2 arg2)
        {
            if (!BeginPublish(out int count, out PublishTrace trace)) return;
            int success = 0, failure = 0, skipped = 0;
            try
            {
                EventLogger.PublishStarted(Descriptor, trace, ListenerCount);
                for (int i = 0; i < count; i++)
                {
                    ListenerEntry<Action<T1, T2>> entry = Listeners[i];
                    if (!CanInvoke(entry, trace)) { skipped++; continue; }
                    try
                    {
                        entry.Callback(arg1, arg2);
                        success++;
                    }
                    catch (Exception exception)
                    {
                        failure++;
                        EventLogger.Failed(Descriptor, trace, entry.Description, exception);
                        continue;
                    }
                    EventLogger.Succeeded(trace, entry.Description);
                }
            }
            finally
            {
                try { EventLogger.PublishFinished(Descriptor, trace, success, failure, skipped); }
                finally { EndPublish(); }
            }
        }
    }

    /// <summary>按原始类型同步分发 3 个参数，不装箱参数。</summary>
    internal sealed class EventChannel<T1, T2, T3> : EventChannelBase<Action<T1, T2, T3>>
    {
        /// <summary>创建 3 参数事件通道。</summary>
        internal EventChannel(EventDescriptor descriptor) : base(descriptor) { }

        /// <summary>逐条调用固定范围内的监听，异常不阻断后续通知。</summary>
        internal void Publish(T1 arg1, T2 arg2, T3 arg3)
        {
            if (!BeginPublish(out int count, out PublishTrace trace)) return;
            int success = 0, failure = 0, skipped = 0;
            try
            {
                EventLogger.PublishStarted(Descriptor, trace, ListenerCount);
                for (int i = 0; i < count; i++)
                {
                    ListenerEntry<Action<T1, T2, T3>> entry = Listeners[i];
                    if (!CanInvoke(entry, trace)) { skipped++; continue; }
                    try
                    {
                        entry.Callback(arg1, arg2, arg3);
                        success++;
                    }
                    catch (Exception exception)
                    {
                        failure++;
                        EventLogger.Failed(Descriptor, trace, entry.Description, exception);
                        continue;
                    }
                    EventLogger.Succeeded(trace, entry.Description);
                }
            }
            finally
            {
                try { EventLogger.PublishFinished(Descriptor, trace, success, failure, skipped); }
                finally { EndPublish(); }
            }
        }
    }

}
