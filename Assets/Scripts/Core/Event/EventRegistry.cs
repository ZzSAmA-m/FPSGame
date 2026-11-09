using System.Collections.Generic;

namespace Core.Event
{
    /// <summary>集中保存所有事件定义，保证编号全局唯一。</summary>
    internal sealed class EventRegistry
    {
        /// <summary>按事件编号索引已注册通道。</summary>
        private readonly Dictionary<int, IEventChannel> _channels = new Dictionary<int, IEventChannel>(32);

        /// <summary>查找事件，不创建临时键或参数数组。</summary>
        internal bool TryGet(int id, out IEventChannel channel) => _channels.TryGetValue(id, out channel);

        /// <summary>添加已经过身份校验的新通道。</summary>
        internal void Add(IEventChannel channel) => _channels.Add(channel.Descriptor.Id, channel);

        /// <summary>清理监听引用，再清空注册表。</summary>
        internal void Clear()
        {
            foreach (IEventChannel channel in _channels.Values)
                channel.Clear();
            _channels.Clear();
        }
    }
}
