using System;

namespace GamePlay.Player
{
    /// <summary>一次已提交的数值变更，供服务端可靠同步和实例事件消费。</summary>
    public readonly struct PlayerStatsChange
    {
        public PlayerStatsState Before { get; }
        public PlayerStatsState After { get; }
        public int HealthLost => Math.Max(0, Before.Health - After.Health);
        public int ArmorLost => Math.Max(0, Before.Armor - After.Armor);
        public bool LifeStateChanged => (Before.Health > 0) != (After.Health > 0);
        public bool Changed => After.Revision != Before.Revision;

        public PlayerStatsChange(PlayerStatsState before, PlayerStatsState after)
        {
            Before = before;
            After = after;
        }
    }
}
