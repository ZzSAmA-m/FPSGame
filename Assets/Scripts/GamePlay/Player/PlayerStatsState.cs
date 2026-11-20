namespace GamePlay.Player
{
    public enum PlayerLifeState : byte
    {
        Uninitialized = 0,
        Alive = 1,
        Dead = 2
    }

    /// <summary>单个玩家的数值快照，按值读取不会修改 Controller 中的状态。</summary>
    public readonly struct PlayerStatsState
    {
        public int Health { get; }
        public int MaxHealth { get; }
        public int Armor { get; }
        public int MaxArmor { get; }
        public ulong Revision { get; }

        public bool IsValid => Revision > 0 && MaxHealth > 0 && MaxArmor >= 0 &&
                               Health >= 0 && Health <= MaxHealth && Armor >= 0 && Armor <= MaxArmor;

        public PlayerStatsState(int health, int maxHealth, int armor, int maxArmor, ulong revision)
        {
            Health = health;
            MaxHealth = maxHealth;
            Armor = armor;
            MaxArmor = maxArmor;
            Revision = revision;
        }
    }
}
