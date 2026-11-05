namespace GamePlay.Weapon
{
    /// <summary>定义枪械状态的动作入口和生命周期。</summary>
    public abstract class GunStateBase
    {
        /// <summary>当前状态所属的枪械。</summary>
        protected Gun Owner { get; }

        /// <summary>负责管理当前状态的状态机。</summary>
        protected GunStateMachine Machine { get; }

        /// <summary>当前状态对应的枚举标识。</summary>
        public abstract GunState Id { get; }

        /// <summary>保存状态使用的枪械和状态机。</summary>
        protected GunStateBase(Gun owner, GunStateMachine machine)
        {
            Owner = owner;
            Machine = machine;
        }

        /// <summary>进入状态时初始化本次动作。</summary>
        internal virtual void Enter(uint tick) { }

        /// <summary>推进当前动作并检查完成条件。</summary>
        internal virtual void Tick(uint tick) { }

        /// <summary>退出状态时取消未完成动作。</summary>
        internal virtual void Exit() { }

        /// <summary>默认拒绝准备，由允许的状态重写。</summary>
        internal virtual bool TryPrepare() => false;

        /// <summary>默认拒绝射击，由允许的状态重写。</summary>
        internal virtual bool TryFire() => false;

        /// <summary>默认拒绝换弹，由允许的状态重写。</summary>
        internal virtual bool TryReload() => false;
    }
}
