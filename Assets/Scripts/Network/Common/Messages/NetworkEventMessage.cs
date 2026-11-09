using Mirror;

namespace Network.Common.Messages
{
    /// <summary>服务器已经确认、只发生一次的瞬时事件；通过 Envelope 发送。</summary>
    /// <remarks>
    /// Mirror 的顶层收发 API 仅接受结构体，因此用可继承的消息负载描述事件，
    /// 由 <see cref="NetworkEventEnvelope"/> 适配传输。事件不参与移动状态恢复或重放。
    /// </remarks>
    public abstract class NetworkEventMessage : NetworkMessage
    {
        /// <summary>服务器运行期间全局递增的事件号，0 无效。</summary>
        public ulong eventId;

        /// <summary>事件被服务器确认的模拟拍。</summary>
        public uint serverTick;

        /// <summary>产生事件的玩家网络标识。</summary>
        public uint netId;
    }
}
