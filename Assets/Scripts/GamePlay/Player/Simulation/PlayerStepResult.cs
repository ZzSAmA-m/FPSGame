namespace GamePlay.Player.Simulation
{
    /// <summary>
    /// 这一拍产生的玩法边沿。表现层只消费实时步进的结果，重放时可以丢掉它。
    /// </summary>
    public struct PlayerStepResult
    {
        public bool jumped;
        public bool fired; // 运动输入中的 Fire 意图；实际发射成功使用 PlayerLiveStepResult.Fired。
        public bool key1;
        public bool key2;
        public bool key3;
    }
}
