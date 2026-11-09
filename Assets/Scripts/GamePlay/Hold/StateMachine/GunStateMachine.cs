namespace GamePlay.HoldSystem
{
    /// <summary>分发枪械操作并统一管理状态转换。</summary>
    public sealed class GunStateMachine
    {
        /// <summary>当前状态机所属的枪械。</summary>
        private readonly Gun _owner;

        /// <summary>按状态标识保存可复用的状态实例。</summary>
        private readonly GunStateBase[] _states;

        /// <summary>当前正在执行的状态。</summary>
        private GunStateBase _current;

        /// <summary>是否已经建立模拟 Tick 基准。</summary>
        private bool _hasTick;

        /// <summary>是否正在处理动作或发送通知。</summary>
        private bool _isOperating;

        /// <summary>当前状态对应的枚举标识。</summary>
        public GunState CurrentId => _current.Id;

        /// <summary>当前已经接收的模拟 Tick。</summary>
        public uint CurrentTick { get; private set; }

        /// <summary>为当前枪械创建五个独立状态。</summary>
        internal GunStateMachine(Gun owner)
        {
            _owner = owner;
            _states = new GunStateBase[5];
            _states[(int)GunState.None] = new GunNoneState(owner, this);
            _states[(int)GunState.Preparing] = new GunPreparingState(owner, this);
            _states[(int)GunState.Shooting] = new GunShootingState(owner, this);
            _states[(int)GunState.Reloading] = new GunReloadingState(owner, this);
            _states[(int)GunState.Normal] = new GunNormalState(owner, this);
            _current = _states[(int)GunState.None];
        }

        /// <summary>将准备请求交给当前状态，拒绝回调重入。</summary>
        internal bool TryPrepare()
        {
            if (!BeginOperation())
                return false;

            try
            {
                return _current.TryPrepare();
            }
            finally
            {
                _isOperating = false;
            }
        }

        /// <summary>将开火请求交给当前状态，拒绝回调重入。</summary>
        internal bool TryFire()
        {
            if (!BeginOperation())
                return false;

            try
            {
                return _current.TryFire();
            }
            finally
            {
                _isOperating = false;
            }
        }

        /// <summary>将换弹请求交给当前状态，拒绝回调重入。</summary>
        internal bool TryReload()
        {
            if (!BeginOperation())
                return false;

            try
            {
                return _current.TryReload();
            }
            finally
            {
                _isOperating = false;
            }
        }

        /// <summary>接收新的模拟拍，忽略重复、旧拍和回调重入。</summary>
        internal void Tick(uint tick)
        {
            if (_isOperating || (_hasTick && unchecked((int)(tick - CurrentTick)) <= 0))
                return;

            _isOperating = true;
            try
            {
                CurrentTick = tick;
                _hasTick = true;
                _owner.AdvanceCooldown();
                _current.Tick(tick);
            }
            finally
            {
                _isOperating = false;
            }
        }

        /// <summary>取消当前动作并收起枪械，保留射击冷却。</summary>
        internal void Unequip()
        {
            if (!BeginOperation())
                return;

            try
            {
                ChangeState(GunState.None);
            }
            finally
            {
                _isOperating = false;
            }
        }

        /// <summary>在受控动作内执行退出、切换、进入和通知。</summary>
        internal void ChangeState(GunState state)
        {
            if (!_isOperating || CurrentId == state)
                return;

            GunState previous = CurrentId;
            _hasTick = true;
            _current.Exit();
            _current = _states[(int)state];
            _current.Enter(CurrentTick);
            _owner.NotifyStateChanged(previous, state);
        }

        /// <summary>判断截止拍是否到达，支持 uint 回绕。</summary>
        internal bool HasReached(uint deadline)
        {
            return unchecked((int)(CurrentTick - deadline)) >= 0;
        }

        /// <summary>锁定本次操作，失败请求不建立 Tick 基准。</summary>
        private bool BeginOperation()
        {
            if (_isOperating)
                return false;

            _isOperating = true;
            return true;
        }
    }
}
