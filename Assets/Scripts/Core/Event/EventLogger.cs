using System;
using Core.ZLog;
using Log = Core.ZLog.ZLog;

namespace Core.Event
{
    /// <summary>先检查日志过滤，再格式化事件诊断信息。</summary>
    internal static class EventLogger
    {
        /// <summary>阻止日志回调再次写入事件日志导致递归。</summary>
        [ThreadStatic] private static bool _writing;

        /// <summary>写入前检查重入和日志过滤。</summary>
        internal static bool CanWrite(LogFrequency frequency) => !_writing && Log.ShouldLog(LogChannel.Event, frequency);

        /// <summary>在重入保护中输出普通日志。</summary>
        private static void Write(string message, LogFrequency frequency)
        {
            if (_writing) return;
            _writing = true;
            try { Log.Log(message, LogChannel.Event, frequency); }
            finally { _writing = false; }
        }

        /// <summary>在重入保护中输出警告日志。</summary>
        private static void WriteWarning(string message, LogFrequency frequency)
        {
            if (_writing) return;
            _writing = true;
            try { Log.LogWarning(message, LogChannel.Event, frequency); }
            finally { _writing = false; }
        }

        /// <summary>在重入保护中输出错误日志。</summary>
        private static void WriteError(string message, LogFrequency frequency)
        {
            if (_writing) return;
            _writing = true;
            try { Log.LogError(message, LogChannel.Event, frequency); }
            finally { _writing = false; }
        }

        /// <summary>记录注册结果，幂等注册不伪装成更新。</summary>
        internal static void Registered(EventDescriptor descriptor, bool existing)
        {
            if (CanWrite(LogFrequency.Low))
                Write($"[事件注册] {(existing ? "已存在，保留注册、监听和日志设置" : "注册成功")} | {descriptor.Description}", LogFrequency.Low);
        }

        /// <summary>记录实际添加或移除的监听。</summary>
        internal static void ListenerChanged(EventDescriptor descriptor, string listener, bool added, int count)
        {
            if (CanWrite(LogFrequency.Low))
                Write($"[{(added ? "订阅成功" : "解订阅成功")}] {descriptor.Description} | {listener} | 有效监听数：{count}", LogFrequency.Low);
        }

        /// <summary>记录重复订阅，保留原 owner 和回调。</summary>
        internal static void Duplicate(EventDescriptor descriptor, string listener)
        {
            if (CanWrite(LogFrequency.Low))
                WriteWarning($"[重复订阅，已拒绝] {descriptor.Description} | {listener}", LogFrequency.Low);
        }

        /// <summary>说明未找到待移除的方法。</summary>
        internal static void ListenerMissing(EventDescriptor descriptor, Delegate callback)
        {
            if (CanWrite(LogFrequency.Low))
                WriteWarning($"[解订阅未找到] {descriptor.Description} | {callback.Method.DeclaringType?.FullName}.{callback.Method.Name}", LogFrequency.Low);
        }

        /// <summary>报告未注册事件，拒绝自动创建。</summary>
        internal static void Missing(int id)
        {
            if (CanWrite(LogFrequency.Low))
                WriteError($"[事件未注册，操作已拒绝] ID={id}", LogFrequency.Low);
        }

        /// <summary>报告名称或完整参数签名不一致。</summary>
        internal static void Mismatch(EventDescriptor expected, string name, Type actual)
        {
            if (CanWrite(LogFrequency.Low))
                WriteError($"[事件定义或签名冲突，操作已拒绝] 已注册：{expected.Description} | 请求名称：{name} | 请求委托：{actual}", LogFrequency.Low);
        }

        /// <summary>记录无需动态拼接的使用错误。</summary>
        internal static void Error(string message)
        {
            if (CanWrite(LogFrequency.Low))
                WriteError(message, LogFrequency.Low);
        }

        /// <summary>记录一次发布开始时的候选数量。</summary>
        internal static void PublishStarted(EventDescriptor descriptor, in PublishTrace trace, int count)
        {
            if (trace.Summary && CanWrite(trace.Frequency))
                Write($"[发布#{trace.Id} 开始] {descriptor.Description} | 待调用监听数：{count}", trace.Frequency);
        }

        /// <summary>只在回调正常返回后记录调用成功。</summary>
        internal static void Succeeded(in PublishTrace trace, string listener)
        {
            if (trace.Detailed && CanWrite(trace.Frequency))
                Write($"[发布#{trace.Id} 调用成功] {listener}", trace.Frequency);
        }

        /// <summary>记录未执行的监听及原因。</summary>
        internal static void Skipped(in PublishTrace trace, string listener, bool destroyed)
        {
            if (trace.Detailed && CanWrite(trace.Frequency))
                Write($"[发布#{trace.Id} 跳过] {listener} | {(destroyed ? "回调目标已销毁" : "本轮执行前已解订阅")}", trace.Frequency);
        }

        /// <summary>明细关闭时仍按错误分类记录回调异常。</summary>
        internal static void Failed(EventDescriptor descriptor, in PublishTrace trace, string listener, Exception exception)
        {
            if (CanWrite(LogFrequency.Low))
                WriteError($"[发布#{trace.Id} 调用失败] {descriptor.Description} | {listener}\n{exception}", LogFrequency.Low);
        }

        /// <summary>根据实际执行计数记录发布结果。</summary>
        internal static void PublishFinished(EventDescriptor descriptor, in PublishTrace trace, int success, int failure, int skipped)
        {
            if (trace.Summary && CanWrite(trace.Frequency))
                Write($"[发布#{trace.Id} 结束] {descriptor.Description} | 成功：{success}，失败：{failure}，跳过：{skipped}", trace.Frequency);
        }
    }
}
