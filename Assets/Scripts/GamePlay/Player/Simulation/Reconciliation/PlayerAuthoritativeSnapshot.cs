namespace GamePlay.Player.Simulation.Reconciliation
{
    /// <summary>回滚所需的权威状态，不依赖网络消息类型。</summary>
    public struct PlayerAuthoritativeSnapshot
    {
        public uint Epoch;
        public uint ServerTick;
        public uint StateInputTick;
        public PlayerSimState State;
    }
}
