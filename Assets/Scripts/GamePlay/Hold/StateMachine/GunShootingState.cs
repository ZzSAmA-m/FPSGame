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

        /// <summary>射击间隔结束后恢复就绪，不再次发射。</summary>
        internal override void Tick(uint tick)
        {
            if (Machine.HasReached(_endTick))
                Machine.ChangeState(GunState.Normal);
        }
    }
}
