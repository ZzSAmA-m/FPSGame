namespace GamePlay.HoldSystem
{
    /// <summary>使用通用枪械状态机的 AK47。</summary>
    public sealed class AK47 : Gun
    {
        /// <summary>枪械名称。</summary>
        public override string Name => "AK47";

        /// <summary>枪械介绍。</summary>
        public override string Info => "这是一把AK47";

        /// <summary>使用默认配置创建独立枪械。</summary>
        public AK47() : this(GunConfig.Default)
        {
        }

        /// <summary>使用指定配置创建独立枪械。</summary>
        public AK47(GunConfig config) : base(config)
        {
        }
    }
}
