using System;

namespace GamePlay.HoldSystem
{
    /// <summary>角色默认使用的独立刀具逻辑实例。</summary>
    public sealed class Knife : WeaponBase
    {
        private readonly KnifeStateMachine _stateMachine;

        public override string Name => "蝴蝶刀";
        public override string Info => "角色默认装备的近战刀具。";
        public KnifeConfig Config { get; }
        public KnifeState State => _stateMachine.State;
        public float ActionProgress => _stateMachine.ActionProgress;

        public event Action<KnifeState> OnStateChanged;

        public Knife() : this(KnifeConfig.Default) { }

        public Knife(KnifeConfig config)
        {
            Config = config ?? throw new ArgumentNullException(nameof(config));
            _stateMachine = new KnifeStateMachine(this);
        }

        public void Tick(uint tick) => _stateMachine.Tick(tick);
        public bool TryPrepare() => _stateMachine.TryPrepare();
        public bool TryAttack(KnifeAttackKind kind) => _stateMachine.TryAttack(kind);
        public void Unequip() => _stateMachine.Unequip();

        internal void NotifyStateChanged(KnifeState state) => OnStateChanged?.Invoke(state);
    }
}
