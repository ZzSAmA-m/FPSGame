using System;
using System.Collections.Generic;
using GamePlay.Player.Simulation;
using UnityEngine;

namespace GamePlay.Player
{
    /// <summary>持有玩家独立数值，服务端结算修改，客户端应用权威结果。</summary>
    [DisallowMultipleComponent]
    public sealed class PlayerStatsController : MonoBehaviour
    {
        [SerializeField] private PlayerBasicDataSO _playerBasicData;

        private PlayerStatsState _state;
        private bool _hasState;
        private bool _isAuthoritative;
        private bool _isOwner;
        private bool _initialized;
        private ulong _lifeRevision;
        private float _armorAbsorptionRatio;
        private int _spawnHealth;
        private int _spawnArmor;
        private readonly Queue<Action> _notifications = new();
        private bool _notifying;
        private int _lifecycle;

        public PlayerBasicDataSO Config => _playerBasicData;
        public PlayerLifeState LifeState { get; private set; }
        public uint SpawnSequence { get; private set; }
        public event Action<PlayerStatsChange> OnStatsChanged;
        public event Action<PlayerLifeState> OnLifeStateChanged;
        public event Action OnDied;
        public event Action Stopped;

        /// <summary>由网络服务器生命周期初始化，配置只在初始化时读入。</summary>
        public void InitializeServer()
        {
            if (_initialized)
                return;
            PlayerBrain brain = GetComponent<PlayerBrain>();
            if (brain == null || brain.Role != PawnRole.Authoritative)
                throw new InvalidOperationException("玩家数值只能由权威角色初始化服务器状态。");
            if (_playerBasicData == null)
                throw new InvalidOperationException("PlayerStatsController 缺少玩家基础数值 SO。");

            _state = CreateSpawnState(1);
            if (!_state.IsValid || _state.Health == 0)
                throw new InvalidOperationException("玩家初始数值配置无效。");
            _spawnHealth = _state.Health;
            _spawnArmor = _state.Armor;
            _armorAbsorptionRatio = _playerBasicData.ArmorAbsorptionRatio;
            _isAuthoritative = true;
            _initialized = true;
            _lifecycle++;
            _hasState = true;
            _lifeRevision = _state.Revision;
            LifeState = PlayerLifeState.Alive;
        }

        /// <summary>客户端等待服务端基准，本地玩家身份在注册对象前确定。</summary>
        public void InitializeClient(bool isOwner)
        {
            if (_initialized)
                return;
            _isOwner = isOwner;
            _initialized = true;
            _lifecycle++;
        }

        public bool TryGetState(out PlayerStatsState state)
        {
            state = _state;
            return _hasState;
        }

        public PlayerStatsChange ApplyDamage(int damage)
        {
            EnsureAuthority();
            if (damage <= 0 || LifeState != PlayerLifeState.Alive)
                return new PlayerStatsChange(_state, _state);

            int armorLoss = (int)Math.Min(_state.Armor, Math.Floor(damage * (double)_armorAbsorptionRatio));
            int healthLoss = Math.Min(_state.Health, damage - armorLoss);
            return Commit(_state.Health - healthLoss, _state.Armor - armorLoss);
        }

        public PlayerStatsChange Heal(int amount)
        {
            EnsureAuthority();
            if (amount <= 0 || LifeState != PlayerLifeState.Alive)
                return new PlayerStatsChange(_state, _state);
            int added = Math.Min(amount, _state.MaxHealth - _state.Health);
            return Commit(_state.Health + added, _state.Armor);
        }

        public PlayerStatsChange RestoreArmor(int amount)
        {
            EnsureAuthority();
            if (amount <= 0 || LifeState != PlayerLifeState.Alive)
                return new PlayerStatsChange(_state, _state);
            int added = Math.Min(amount, _state.MaxArmor - _state.Armor);
            return Commit(_state.Health, _state.Armor + added);
        }

        public PlayerStatsChange ResetForSpawn()
        {
            EnsureAuthority();
            SpawnSequence = checked(SpawnSequence + 1);
            return Commit(_spawnHealth, _spawnArmor);
        }

        internal bool ApplyState(PlayerStatsState state)
        {
            if (!_initialized || _isAuthoritative || !_isOwner || !state.IsValid ||
                (_hasState && state.Revision <= _state.Revision))
                return false;

            PlayerStatsState before = _state;
            _state = state;
            _hasState = true;
            var change = new PlayerStatsChange(before, state);
            Publish(() => Notify(OnStatsChanged, change));
            return true;
        }

        internal bool ApplyLifeState(PlayerLifeState lifeState, ulong revision)
        {
            if (!_initialized || _isAuthoritative || revision == 0 || revision <= _lifeRevision ||
                (lifeState != PlayerLifeState.Alive && lifeState != PlayerLifeState.Dead))
                return false;

            PlayerLifeState before = LifeState;
            _lifeRevision = revision;
            LifeState = lifeState;
            int lifecycle = _lifecycle;
            Publish(() => NotifyLifeState(before, lifeState, lifecycle));
            return true;
        }

        public void Shutdown()
        {
            if (!_initialized)
                return;
            _initialized = false;
            _lifecycle++;
            _isAuthoritative = false;
            _isOwner = false;
            _hasState = false;
            _state = default;
            _lifeRevision = 0;
            LifeState = PlayerLifeState.Uninitialized;
            _notifications.Clear();
            Notify(Stopped);
        }

        private void OnDestroy() => Shutdown();

        private PlayerStatsState CreateSpawnState(ulong revision) => new(
            _playerBasicData.InitialHealth, _playerBasicData.MaxHealth,
            _playerBasicData.InitialArmor, _playerBasicData.MaxArmor, revision);

        private void EnsureAuthority()
        {
            if (!_initialized || !_isAuthoritative)
                throw new InvalidOperationException("玩家数值修改需要已初始化的服务端权威状态。");
        }

        private PlayerStatsChange Commit(int health, int armor)
        {
            if (health == _state.Health && armor == _state.Armor)
                return new PlayerStatsChange(_state, _state);

            PlayerStatsState before = _state;
            ulong revision = checked(before.Revision + 1);
            _state = new PlayerStatsState(health, before.MaxHealth, armor, before.MaxArmor, revision);
            LifeState = health > 0 ? PlayerLifeState.Alive : PlayerLifeState.Dead;
            _lifeRevision = revision;
            var change = new PlayerStatsChange(before, _state);
            int lifecycle = _lifecycle;
            Publish(() =>
            {
                Notify(OnStatsChanged, change);
                if (lifecycle == _lifecycle && change.LifeStateChanged)
                    NotifyLifeState(before.Health > 0 ? PlayerLifeState.Alive : PlayerLifeState.Dead,
                        change.After.Health > 0 ? PlayerLifeState.Alive : PlayerLifeState.Dead, lifecycle);
            });
            return change;
        }

        // 回调内再次结算时，所有监听者仍按提交顺序收到变更。
        private void Publish(Action notification)
        {
            _notifications.Enqueue(notification);
            if (_notifying)
                return;
            _notifying = true;
            try
            {
                while (_notifications.Count > 0)
                    _notifications.Dequeue()();
            }
            finally { _notifying = false; }
        }

        private void NotifyLifeState(PlayerLifeState before, PlayerLifeState after, int lifecycle)
        {
            if (before == after)
                return;
            Notify(OnLifeStateChanged, after);
            if (lifecycle == _lifecycle && before == PlayerLifeState.Alive && after == PlayerLifeState.Dead)
                Notify(OnDied);
        }

        // 监听者失败不能阻止后续网络订阅者收到已提交的变更。
        private void Notify<T>(Action<T> listeners, T value)
        {
            if (listeners == null)
                return;
            int lifecycle = _lifecycle;
            foreach (Action<T> listener in listeners.GetInvocationList())
            {
                if (lifecycle != _lifecycle)
                    return;
                try { listener(value); }
                catch (Exception exception) { Debug.LogException(exception, this); }
            }
        }

        private void Notify(Action listeners)
        {
            if (listeners == null)
                return;
            int lifecycle = _lifecycle;
            foreach (Action listener in listeners.GetInvocationList())
            {
                if (lifecycle != _lifecycle)
                    return;
                try { listener(); }
                catch (Exception exception) { Debug.LogException(exception, this); }
            }
        }
    }
}
