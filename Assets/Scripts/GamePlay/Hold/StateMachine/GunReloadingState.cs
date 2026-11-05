namespace GamePlay.HoldSystem
{
    /// <summary>等待换弹完成后一次性装填。</summary>
    public sealed class GunReloadingState : GunStateBase
    {
        /// <summary>本次换弹动作的结束拍。</summary>
        private uint _endTick;

        /// <summary>换弹状态的标识。</summary>
        public override GunState Id => GunState.Reloading;

        /// <summary>关联当前枪械和状态机。</summary>
        internal GunReloadingState(Gun owner, GunStateMachine machine) : base(owner, machine) { }

        /// <summary>每次进入时重新计算装填结束拍。</summary>
        internal override void Enter(uint tick)
        {
            _endTick = unchecked(tick + Owner.Config.ReloadTicks);
        }

        /// <summary>成功开火时打断换弹，失败时继续换弹。</summary>
        internal override bool TryFire()
        {
            if (!Owner.TryCommitShot())
                return false;

            Machine.ChangeState(GunState.Shooting);
            Owner.NotifyShot();
            return true;
        }

        /// <summary>到期装填后恢复就绪并通知，取消时不会执行。</summary>
        internal override void Tick(uint tick)
        {
            if (!Machine.HasReached(_endTick))
                return;

            int roundsBefore = Owner.Magazine.Rounds;
            bool reloaded = Owner.Magazine.Reload();
            Machine.ChangeState(GunState.Normal);
            if (reloaded)
                Owner.NotifyReloadCompleted(Owner.Magazine.Rounds - roundsBefore);
        }
    }
}
