using GamePlay.Player;
using GamePlay.Player.Simulation;

namespace Network.Server
{
    /// <summary>调度层为一个服务器玩家解析出的本拍输入及其标识。</summary>
    public readonly struct ServerPlayerInput
    {
        public uint PlayerId { get; }
        public uint PredictionEpoch { get; }
        public PlayerBrain Brain { get; }
        public PlayerInputFrame Frame { get; }
        public double ReceiptTick { get; }
        public double RoundTripSeconds { get; }

        public ServerPlayerInput(uint playerId, uint predictionEpoch, PlayerBrain brain, in PlayerInputFrame frame,
            double receiptTick = double.NaN, double roundTripSeconds = 0d)
        {
            PlayerId = playerId;
            PredictionEpoch = predictionEpoch;
            Brain = brain;
            Frame = frame;
            ReceiptTick = receiptTick;
            RoundTripSeconds = roundTripSeconds;
        }
    }
}
