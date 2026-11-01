namespace GamePlay.Player.Simulation.Reconciliation
{
    /// <summary>保存一拍输入及其结束后的状态。</summary>
    public struct PredictionFrame
    {
        public PlayerInputFrame Input;
        public PlayerSimState PostState;
        public bool HasInput;
    }
}
