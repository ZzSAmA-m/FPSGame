using GamePlay.Player.Simulation;
using Mirror;

namespace Network.Common.Messages
{
    /// <summary>开始一轮预测时，可靠发送的权威起点。</summary>
    public struct PredictionBaselineMessage : NetworkMessage
    {
        public uint netId;
        public uint predictionEpoch;
        public uint baseInputTick;
        public PlayerSimState state;
    }

    /// <summary>客户端请求放弃旧历史，重新建立预测起点。</summary>
    public struct PredictionResyncRequest : NetworkMessage
    {
        public uint netId;
        public uint predictionEpoch;
    }
}
