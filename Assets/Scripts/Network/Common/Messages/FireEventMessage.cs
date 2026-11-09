using UnityEngine;

namespace Network.Common.Messages
{
    /// <summary>服务器成功发射一发后产生的开火事件，不代表命中。</summary>
    public sealed class FireEventMessage : NetworkEventMessage
    {
        public uint predictionEpoch;
        public uint inputTick;

        /// <summary>开火时服务器模拟的玩家位置。</summary>
        public Vector3 position;

        /// <summary>服务器计算的开火起点。</summary>
        public Vector3 origin;

        /// <summary>服务器计算的单位开火方向。</summary>
        public Vector3 direction;
    }
}
