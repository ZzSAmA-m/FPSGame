namespace GamePlay.Player.Simulation.Reconciliation
{
    public enum ReconcileStatus
    {
        Ignored,
        Matched,
        Replayed,
        NeedsResync
    }

    /// <summary>对账结果及纠偏前后的当前状态。</summary>
    public struct ReconcileResult
    {
        public ReconcileStatus Status;
        public uint AcknowledgedTick;
        public int ReplayedTicks;
        public float PositionError;
        public float VelocityError;
        public float AngleError;
        public PlayerSimState Before;
        public PlayerSimState After;

        public bool NeedsResync => Status == ReconcileStatus.NeedsResync;
    }
}
