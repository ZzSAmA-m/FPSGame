namespace Core.Event
{
    /// <summary>供注册表管理不同参数数量的事件通道。</summary>
    internal interface IEventChannel
    {
        /// <summary>当前通道的固定事件定义。</summary>
        EventDescriptor Descriptor { get; }

        /// <summary>当前有效的监听数量。</summary>
        int ListenerCount { get; }

        /// <summary>当前通道的发布日志选项。</summary>
        EventLogOptions LogOptions { get; }

        /// <summary>移除全部监听并释放对象引用。</summary>
        void Clear();
    }
}
