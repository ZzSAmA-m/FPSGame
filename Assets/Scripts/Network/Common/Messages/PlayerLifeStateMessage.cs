using GamePlay.Player;
using Mirror;

namespace Network.Common.Messages
{
    /// <summary>可靠发送给观察客户端的玩家生命状态，不包含具体数值。</summary>
    public struct PlayerLifeStateMessage : NetworkMessage
    {
        public uint netId;
        public ulong revision;
        public PlayerLifeState lifeState;
    }
}
