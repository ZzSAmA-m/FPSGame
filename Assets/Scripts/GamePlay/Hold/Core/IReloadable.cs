namespace GamePlay.HoldSystem
{
    /// <summary>提供尝试换弹的能力。</summary>
    public interface IReloadable
    {
        /// <summary>尝试开始换弹，成功时返回 true。</summary>
        public bool TryReload();
    }

}
