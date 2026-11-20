using System;
using System.Collections.Generic;
using Core.ZLog;
using GamePlay.Player;
using Mirror;
using Network.Client;
using Network.Common.Messages;

namespace Network.Server
{
    public sealed partial class ServerManager
    {
        readonly Dictionary<int, PlayerStatsBinding> _playerStatsBindings = new();
        readonly List<PendingPlayerStatsChange> _pendingPlayerStatsChanges = new();

        void BindPlayerStats(ClientSession session)
        {
            UnbindPlayerStats(session.Connection);
            if (session.Player == null || !session.Player.TryGetComponent(out PlayerBrain brain) ||
                brain.Stats == null || !brain.Stats.TryGetState(out PlayerStatsState state))
            {
                ZLog.LogError<ServerManager>($"玩家 {session.Player?.netId} 的服务端数值尚未初始化。");
                return;
            }

            var binding = new PlayerStatsBinding(session.Connection, session.Player.netId, brain.Stats);
            binding.Changed = change => QueuePlayerStatsChange(binding, change);
            binding.Stopped = () => UnbindPlayerStats(binding.Connection);
            _playerStatsBindings.Add(session.ConnectionId, binding);
            binding.Stats.OnStatsChanged += binding.Changed;
            binding.Stats.Stopped += binding.Stopped;

            _bridge.SendPlayerStatsState(binding.Connection, new PlayerStatsStateMessage
            {
                netId = binding.PlayerId,
                state = state
            });

            foreach (PlayerStatsBinding existing in _playerStatsBindings.Values)
            {
                if (ReferenceEquals(existing, binding) ||
                    !existing.Stats.TryGetState(out PlayerStatsState existingState))
                    continue;

                _bridge.SendPlayerLifeState(binding.Connection, CreateLifeStateMessage(existing.PlayerId, existingState));
            }

            // 广播同时给新客户端建立自身存活基准，其余玩家只接收公开状态。
            _bridge.BroadcastPlayerLifeState(CreateLifeStateMessage(binding.PlayerId, state));
        }

        void QueuePlayerStatsChange(PlayerStatsBinding binding, PlayerStatsChange change)
        {
            if (!_playerStatsBindings.TryGetValue(binding.Connection.connectionId, out PlayerStatsBinding current) ||
                !ReferenceEquals(current, binding))
                return;

            // 保留每次操作的结果，防止同一拍的死亡和重置被最终状态覆盖。
            _pendingPlayerStatsChanges.Add(new PendingPlayerStatsChange(binding, change.After, change.LifeStateChanged));
        }

        void FlushPlayerStatsChanges()
        {
            if (_pendingPlayerStatsChanges.Count == 0)
                return;

            PendingPlayerStatsChange[] changes = _pendingPlayerStatsChanges.ToArray();
            _pendingPlayerStatsChanges.Clear();
            foreach (PendingPlayerStatsChange change in changes)
            {
                PlayerStatsBinding binding = change.Binding;
                if (!_playerStatsBindings.TryGetValue(binding.Connection.connectionId, out PlayerStatsBinding current) ||
                    !ReferenceEquals(current, binding))
                    continue;

                _bridge.SendPlayerStatsState(binding.Connection, new PlayerStatsStateMessage
                {
                    netId = binding.PlayerId,
                    state = change.State
                });
                if (change.LifeStateChanged)
                    _bridge.BroadcastPlayerLifeState(CreateLifeStateMessage(binding.PlayerId, change.State));
            }
        }

        static PlayerLifeStateMessage CreateLifeStateMessage(uint playerId, PlayerStatsState state)
        {
            return new PlayerLifeStateMessage
            {
                netId = playerId,
                revision = state.Revision,
                lifeState = state.Health > 0 ? PlayerLifeState.Alive : PlayerLifeState.Dead
            };
        }

        void UnbindPlayerStats(NetworkConnectionToClient connection)
        {
            if (connection == null ||
                !_playerStatsBindings.TryGetValue(connection.connectionId, out PlayerStatsBinding binding) ||
                !ReferenceEquals(binding.Connection, connection))
                return;

            binding.Stats.OnStatsChanged -= binding.Changed;
            _gameLogic?.RemovePlayer(binding.PlayerId);
            binding.Stats.Stopped -= binding.Stopped;
            _playerStatsBindings.Remove(connection.connectionId);
            _pendingPlayerStatsChanges.RemoveAll(change => ReferenceEquals(change.Binding, binding));
        }

        void ClearPlayerStatsBindings()
        {
            foreach (PlayerStatsBinding binding in _playerStatsBindings.Values)
            {
                binding.Stats.OnStatsChanged -= binding.Changed;
                binding.Stats.Stopped -= binding.Stopped;
            }

            _playerStatsBindings.Clear();
            _pendingPlayerStatsChanges.Clear();
        }

        sealed class PlayerStatsBinding
        {
            public NetworkConnectionToClient Connection { get; }
            public uint PlayerId { get; }
            public PlayerStatsController Stats { get; }
            public Action<PlayerStatsChange> Changed { get; set; }
            public Action Stopped { get; set; }

            public PlayerStatsBinding(NetworkConnectionToClient connection, uint playerId, PlayerStatsController stats)
            {
                Connection = connection;
                PlayerId = playerId;
                Stats = stats;
            }
        }

        readonly struct PendingPlayerStatsChange
        {
            public PlayerStatsBinding Binding { get; }
            public PlayerStatsState State { get; }
            public bool LifeStateChanged { get; }

            public PendingPlayerStatsChange(PlayerStatsBinding binding, PlayerStatsState state, bool lifeStateChanged)
            {
                Binding = binding;
                State = state;
                LifeStateChanged = lifeStateChanged;
            }
        }
    }
}
