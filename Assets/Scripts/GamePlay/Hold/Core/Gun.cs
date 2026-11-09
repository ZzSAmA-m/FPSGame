using System;

namespace GamePlay.HoldSystem
{
    /// <summary>通过状态机管理枪械动作。</summary>
    public abstract class Gun : WeaponBase, IFireable, IReloadable
    {
        /// <summary>当前枪械独立使用的状态机。</summary>
        private readonly GunStateMachine _stateMachine;

        /// <summary>是否还有未结束的射击冷却。</summary>
        private bool _hasShotCooldown;

        /// <summary>枪械使用的固定配置。</summary>
        public GunConfig Config { get; }

        /// <summary>枪械的弹药只读状态。</summary>
        public Magazine Magazine { get; }

        /// <summary>当前枪械状态的标识。</summary>
        public GunState GunState => _stateMachine.CurrentId;

        /// <summary>下一次允许射击的 Tick。</summary>
        internal uint NextAllowedShotTick { get; private set; }

        /// <summary>是否有弹药且射击冷却已经结束。</summary>
        internal bool CanShoot => !Magazine.Empty && (!_hasShotCooldown || _stateMachine.HasReached(NextAllowedShotTick));

        /// <summary>状态提交后通知新的状态。</summary>
        public event Action<GunState> OnSwitchState;

        /// <summary>本次扣弹和射击状态提交后通知。</summary>
        public event Action OnShot;

        /// <summary>成功进入装备准备状态后通知。</summary>
        public event Action OnPrepare;

        /// <summary>准备完成并进入就绪状态后通知。</summary>
        public event Action OnPrepareCompleted;

        /// <summary>成功进入换弹状态后通知。</summary>
        public event Action OnReload;

        /// <summary>装填完成并进入就绪状态后通知。</summary>
        public event Action OnReloadCompleted;

        /// <summary>当前射击周期自然结束并进入就绪状态后通知，不代表动画结束。</summary>
        public event Action OnShotCooldownEnd;

        /// <summary>动作被收枪或射击打断后通知被取消的状态，不发送正常完成事件。</summary>
        public event Action<GunState> OnActionCanceled;

        /// <summary>创建当前枪械的配置、弹匣和状态机。</summary>
        protected Gun(GunConfig config)
        {
            Config = config ?? throw new ArgumentNullException(nameof(config));
            Magazine = new Magazine(config.InitialTotalBullets, config.Capacity);
            _stateMachine = new GunStateMachine(this);
        }

        /// <summary>尝试发射一发，成功时立即扣弹。</summary>
        public bool TryFire()
        {
            return _stateMachine.TryFire();
        }

        /// <summary>尝试开始换弹，完成前不修改弹量。</summary>
        public bool TryReload()
        {
            return _stateMachine.TryReload();
        }

        /// <summary>尝试将未装备的枪械切入准备状态。</summary>
        public bool TryPrepare()
        {
            return _stateMachine.TryPrepare();
        }

        /// <summary>先推进这一拍的动作，再由调用方处理输入。</summary>
        public void Tick(uint tick)
        {
            _stateMachine.Tick(tick);
        }

        /// <summary>收起枪械并取消动作，忽略事件中的重入调用。</summary>
        public void Unequip()
        {
            _stateMachine.Unequip();
        }

        /// <summary>扣除本次射击弹药并记录冷却截止拍。</summary>
        internal bool TryCommitShot()
        {
            if (!CanShoot || !Magazine.TryConsumeRound())
                return false;

            NextAllowedShotTick = unchecked(_stateMachine.CurrentTick + Config.ShotIntervalTicks);
            _hasShotCooldown = true;
            return true;
        }

        /// <summary>清除已到期的冷却，避免旧截止拍再次参与比较。</summary>
        internal void AdvanceCooldown()
        {
            if (_hasShotCooldown && _stateMachine.HasReached(NextAllowedShotTick))
                _hasShotCooldown = false;
        }

        /// <summary>在枪械状态提交后通知射击表现。</summary>
        internal void NotifyShot()
        {
            Magazine.NotifyConsumed();
            OnShot?.Invoke();
        }

        /// <summary>通知准备动作已经完成。</summary>
        internal void NotifyPrepareCompleted()
        {
            OnPrepareCompleted?.Invoke();
        }

        /// <summary>通知本次装填量及换弹完成。</summary>
        internal void NotifyReloadCompleted(int loaded)
        {
            Magazine.NotifyReloaded(loaded);
            OnReloadCompleted?.Invoke();
        }

        /// <summary>状态提交后通知切换和动作生命周期，完成通知仍由对应状态提交结果后发出。</summary>
        internal void NotifyStateChanged(GunState previous, GunState state)
        {
            OnSwitchState?.Invoke(state);

            bool canceledByUnequip = state == GunState.None &&
                previous is GunState.Preparing or GunState.Reloading or GunState.Shooting;
            bool canceledByShot = previous == GunState.Reloading && state == GunState.Shooting;
            if (canceledByUnequip || canceledByShot)
                OnActionCanceled?.Invoke(previous);

            if (state == GunState.Preparing)
                OnPrepare?.Invoke();
            else if (state == GunState.Reloading)
                OnReload?.Invoke();
            else if (previous == GunState.Shooting && state == GunState.Normal)
                // 只通知自然完成的射击周期；收枪保留冷却，但重新装备不补发旧周期的结束。
                OnShotCooldownEnd?.Invoke();
        }
    }
}
