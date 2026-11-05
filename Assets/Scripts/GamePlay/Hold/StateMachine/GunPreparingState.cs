namespace GamePlay.HoldSystem
{
    /// <summary>等待装备准备动作结束。</summary>
    public sealed class GunPreparingState : GunStateBase
    {
        /// <summary>本次准备动作的结束拍。</summary>
        private uint _endTick;

        /// <summary>准备状态的标识。</summary>
        public override GunState Id => GunState.Preparing;

        /// <summary>关联当前枪械和状态机。</summary>
        internal GunPreparingState(Gun owner, GunStateMachine machine) : base(owner, machine) { }

        /// <summary>按当前拍重新计算准备结束拍。</summary>
        internal override void Enter(uint tick)
        {
            _endTick = unchecked(tick + Owner.Config.PrepareTicks);
        }

        /// <summary>准备完成后进入就绪状态并通知。</summary>
        internal override void Tick(uint tick)
        {
            if (!Machine.HasReached(_endTick))
                return;

            Machine.ChangeState(GunState.Normal);
            Owner.NotifyPrepareCompleted();
        }
    }
}
