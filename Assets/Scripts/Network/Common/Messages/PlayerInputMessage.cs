using GamePlay.Player.Simulation;
using Mirror;

namespace Network.Common.Messages
{
    /// <summary>同一轮预测的近期输入，重复拍由服务器去重。</summary>
    public struct PlayerInputMessage : NetworkMessage
    {
        public uint predictionEpoch;
        public PlayerInputFrame[] frames;
    }
}
