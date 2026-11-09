using System;
using Network.Common.Messages;

namespace Network.Client
{
    public sealed partial class ClientManager
    {
        ulong _lastReceivedEventId;

        /// <summary>
        /// 每个服务器事件最多通知一次，包含本地玩家的开火确认。
        /// 该回调不执行武器动作，消费方可通过 netId 区分本地确认和远端事件。
        /// </summary>
        public event Action<NetworkEventMessage> EventReceived;

        void HandleNetworkEvent(NetworkEventMessage message)
        {
            // 广播使用同一个可靠有序通道，服务器事件号在本次连接期间全局递增。
            // 玩家可能尚未生成或已经离开，瞬时事件仍按携带的位置正常交付。
            if (message == null || message.eventId == 0 || message.eventId <= _lastReceivedEventId)
                return;

            _lastReceivedEventId = message.eventId;
            EventReceived?.Invoke(message);
        }
    }
}
