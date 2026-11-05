namespace GamePlay.Weapon
{
    /// <summary>处理就绪状态下的开火和换弹请求。</summary>
    public sealed class GunNormalState : GunStateBase
    {
        /// <summary>就绪状态的标识。</summary>
        public override GunState Id => GunState.Normal;

        /// <summary>关联当前枪械和状态机。</summary>
        internal GunNormalState(Gun owner, GunStateMachine machine) : base(owner, machine) { }

        /// <summary>成功扣弹后进入射击间隔并通知。</summary>
        internal override bool TryFire()
        {
            if (!Owner.TryCommitShot())
                return false;

            Machine.ChangeState(GunState.Shooting);
            Owner.NotifyShot();
            return true;
        }

        /// <summary>弹匣有缺口且有备弹时开始换弹。</summary>
        internal override bool TryReload()
        {
            if (!Owner.Magazine.CanReload)
                return false;

            Machine.ChangeState(GunState.Reloading);
            return true;
        }
    }
}
