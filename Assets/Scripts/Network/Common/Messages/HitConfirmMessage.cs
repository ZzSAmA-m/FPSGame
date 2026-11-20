using GamePlay.Player.Combat;
using Mirror;
using UnityEngine;

namespace Network.Common.Messages
{
    /// <summary>服务器向所有就绪客户端可靠广播的一次权威命中。</summary>
    public struct HitConfirmMessage : NetworkMessage
    {
        public ulong shotId;
        public uint serverTick;
        public uint shooterNetId;
        public uint targetNetId;
        public HitRegion region;
        public Vector3 point;
        public bool killed;
    }
}
