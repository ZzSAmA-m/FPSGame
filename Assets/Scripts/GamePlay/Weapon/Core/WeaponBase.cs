namespace GamePlay.Weapon
{
    /// <summary>提供可手持武器的基础信息。</summary>
    public abstract class WeaponBase : IHoldable
    {
        /// <summary>武器名称。</summary>
        public abstract string Name { get; }

        /// <summary>武器介绍。</summary>
        public abstract string Info { get; }
    }

}
