namespace GamePlay.Player
{
    /// <summary>仅在本地实时步进中执行，不进入移动输入历史或网络消息。</summary>
    public readonly struct LocalWeaponCommand
    {
        public readonly uint Tick;
        public readonly bool FireRequested;

        public LocalWeaponCommand(uint tick, bool fireRequested)
        {
            Tick = tick;
            FireRequested = fireRequested;
        }
    }
}
