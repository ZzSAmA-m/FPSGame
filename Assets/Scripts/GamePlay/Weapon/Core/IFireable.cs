namespace GamePlay.Weapon
{
    /// <summary>提供尝试开火的能力。</summary>
    public interface IFireable
    {
        /// <summary>尝试发射一发，成功时返回 true。</summary>
        public bool TryFire();
    }
}
