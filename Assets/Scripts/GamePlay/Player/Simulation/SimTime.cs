namespace GamePlay.Player.Simulation
{
    /// <summary>客户端预测和服务器模拟共用的固定步长。</summary>
    public static class SimTime
    {
        public const double Dt = 1d / 60d;
        public const float StepSeconds = 1f / 60f;
        public const int MaxStepsPerFrame = 16;
        public const int SnapshotInterval = 2;
        public const int InputWindow = 12;
        public const float RemoteDelaySeconds = 0.03f;
    }
}
