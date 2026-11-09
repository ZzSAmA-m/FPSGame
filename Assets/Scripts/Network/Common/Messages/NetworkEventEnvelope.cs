using Mirror;

namespace Network.Common.Messages
{
    /// <summary>适配 Mirror 结构体消息限制；负载使用显式类型码进行多态序列化。</summary>
    public struct NetworkEventEnvelope : NetworkMessage
    {
        public NetworkEventMessage payload;
    }
}
