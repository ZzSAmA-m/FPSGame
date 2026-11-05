namespace GamePlay.HoldSystem
{
    /// <summary>标识枪械当前所处的状态。</summary>
    public enum GunState
    {
        /// <summary>枪械尚未装备。</summary>
        None,
        /// <summary>正在准备装备。</summary>
        Preparing,
        /// <summary>正在等待射击间隔结束。</summary>
        Shooting,
        /// <summary>正在等待换弹完成。</summary>
        Reloading,
        /// <summary>准备完成，可以接收动作。</summary>
        Normal
    }
}
