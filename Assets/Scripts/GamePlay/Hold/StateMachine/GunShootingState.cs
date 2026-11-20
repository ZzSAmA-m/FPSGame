namespace GamePlay.HoldSystem
{
    /// <summary>等待本次射击后的间隔结束。</summary>
    public sealed class GunShootingState : GunStateBase
    {
        /// <summary>本次射击间隔的结束拍。</summary>
        private uint _endTick;

        /// <summary>射击间隔状态的标识。</summary>
        public override GunState Id => GunState.Shooting;

        /// <summary>关联当前枪械和状态机。</summary>
        internal GunShootingState(Gun owner, GunStateMachine machine) : base(owner, machine) { }

        /// <summary>读取本次射击已经记录的截止拍。</summary>
        internal override void Enter(uint tick)
        {
            _endTick = Owner.NextAllowedShotTick;
        }

        /// <summary>结束当前动作并重新准备，已发射结果和冷却继续保留。</summary>
        internal override bool TryPrepare()
        {
            Machine.ChangeState(GunState.Preparing);
            return true;
        }

        /// <summary>射击间隔内也可开始换弹，不改变已记录的射击冷却。</summary>
        internal override bool TryReload()
        {
            if (!Owner.Magazine.CanReload)
                return false;

            Machine.ChangeState(GunState.Reloading);
            return true;
        }

        /// <summary>提前结束当前动作并恢复就绪，独立射击冷却继续保留。</summary>
        internal override bool TryReturnToNormal()
        {
            Machine.ChangeState(GunState.Normal);
            return true;
        }

        /// <summary>射击间隔结束后恢复就绪，不再次发射。</summary>
        internal override void Tick(uint tick)
        {
            if (Machine.HasReached(_endTick))
                Machine.ChangeState(GunState.Normal, GunActionEndReason.Completed);
        }
    }
}
