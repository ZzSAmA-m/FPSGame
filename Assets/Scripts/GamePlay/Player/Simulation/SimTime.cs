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
        // 一次快照间隔加一拍收包余量，客户端插值和服务器回溯共用。
        public const double RemoteDelaySeconds = (SnapshotInterval + 1) * Dt;
    }
}
