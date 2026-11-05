namespace GamePlay.Weapon
{
    /// <summary>管理枪械尚未装备的状态。</summary>
    public sealed class GunNoneState : GunStateBase
    {
        /// <summary>未装备状态的标识。</summary>
        public override GunState Id => GunState.None;

        /// <summary>关联当前枪械和状态机。</summary>
        internal GunNoneState(Gun owner, GunStateMachine machine) : base(owner, machine) { }

        /// <summary>装备枪械并开始准备。</summary>
        internal override bool TryPrepare()
        {
            Machine.ChangeState(GunState.Preparing);
            return true;
        }
    }
}
