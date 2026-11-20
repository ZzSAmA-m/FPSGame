using System;

namespace GamePlay.HoldSystem
{
    /// <summary>按固定 Tick 推进拔刀和攻击动作。</summary>
    internal sealed class KnifeStateMachine
    {
        private readonly Knife _owner;
        private uint _tick;
        private uint _enteredAt;
        private bool _hasTick;
        private bool _notifying;
        private bool _unequipPending;

        public KnifeState State { get; private set; }
        public float ActionProgress
        {
            get
            {
                uint duration = _owner.Config.GetDuration(State);
                return duration == 0 ? 0f : Math.Min(1f, unchecked(_tick - _enteredAt) / (float)duration);
            }
        }

        public KnifeStateMachine(Knife owner) => _owner = owner;

        public void Tick(uint tick)
        {
            if (_notifying || (_hasTick && unchecked((int)(tick - _tick)) <= 0))
                return;
            _tick = tick;
            _hasTick = true;
            uint duration = _owner.Config.GetDuration(State);
            if (duration > 0 && unchecked(tick - _enteredAt) >= duration)
                ChangeState(KnifeState.Ready);
        }

        public bool TryPrepare()
        {
            if (_notifying || State != KnifeState.None)
                return false;
            ChangeState(KnifeState.Preparing);
            return true;
        }

        public bool TryAttack(KnifeAttackKind kind)
        {
            if (_notifying || State != KnifeState.Ready)
                return false;
            switch (kind)
            {
                case KnifeAttackKind.Light:
                    ChangeState(KnifeState.LightAttack);
                    return true;
                case KnifeAttackKind.Heavy:
                    ChangeState(KnifeState.HeavyAttack);
                    return true;
                default:
                    return false;
            }
        }

        public void Unequip()
        {
            if (State == KnifeState.None)
                return;
            // 装备可在动作回调里被切走，等本次通知发完再通知收刀。
            if (_notifying)
                _unequipPending = true;
            else
                ChangeState(KnifeState.None);
        }

        private void ChangeState(KnifeState next)
        {
            State = next;
            _enteredAt = _tick;
            _hasTick = next != KnifeState.None;
            _notifying = true;
            try
            {
                _owner.NotifyStateChanged(next);
            }
            finally
            {
                _notifying = false;
                if (_unequipPending)
                {
                    _unequipPending = false;
                    Unequip();
                }
            }
        }
    }
}
