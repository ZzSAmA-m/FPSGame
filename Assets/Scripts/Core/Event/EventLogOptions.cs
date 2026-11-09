using Core.ZLog;

namespace Core.Event
{
    /// <summary>保存单个事件的发布日志配置。</summary>
    internal sealed class EventLogOptions
    {
        /// <summary>发布汇总和明细使用的噪声分类。</summary>
        internal LogFrequency Frequency { get; set; } = LogFrequency.Medium;

        /// <summary>是否记录每个监听方法的执行结果。</summary>
        internal bool Detailed { get; set; } = true;
    }

    /// <summary>保存一次发布开始时的日志选项，不分配堆对象。</summary>
    internal readonly struct PublishTrace
    {
        /// <summary>本次发布的诊断编号。</summary>
        internal readonly ulong Id;

        /// <summary>本次发布使用的噪声分类。</summary>
        internal readonly LogFrequency Frequency;

        /// <summary>本次发布是否输出汇总。</summary>
        internal readonly bool Summary;

        /// <summary>本次发布是否输出逐方法明细。</summary>
        internal readonly bool Detailed;

        /// <summary>固定本次发布的日志设置。</summary>
        internal PublishTrace(ulong id, EventLogOptions options)
        {
            Id = id;
            Frequency = options.Frequency;
            Summary = EventLogger.CanWrite(Frequency);
            Detailed = Summary && options.Detailed;
        }
    }
}
