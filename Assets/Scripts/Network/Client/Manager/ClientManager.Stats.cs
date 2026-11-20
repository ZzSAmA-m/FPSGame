using System.Collections.Generic;
using GamePlay.Player;
using Network.Common.Messages;
using UnityEngine;

namespace Network.Client
{
    public sealed partial class ClientManager
    {
        readonly Dictionary<uint, PlayerStatsStateMessage> _pendingStatsStates = new();
        readonly Dictionary<uint, PlayerLifeStateMessage> _pendingLifeStates = new();

        void HandlePlayerStatsState(PlayerStatsStateMessage message)
        {
            if (message.netId == 0 || !message.state.IsValid)
                return;
            if (!Players.Players.TryGetValue(message.netId, out GameObject player) || player == null)
            {
                if (!_pendingStatsStates.TryGetValue(message.netId, out var previous) ||
                    message.state.Revision > previous.state.Revision)
                    _pendingStatsStates[message.netId] = message;
                return;
            }

            if (player.TryGetComponent(out PlayerController controller) && controller.isLocalPlayer &&
                player.TryGetComponent(out PlayerStatsController stats))
                stats.ApplyState(message.state);
        }

        void HandlePlayerLifeState(PlayerLifeStateMessage message)
        {
            if (message.netId == 0 || message.revision == 0 ||
                (message.lifeState != PlayerLifeState.Alive && message.lifeState != PlayerLifeState.Dead))
                return;
            if (!Players.Players.TryGetValue(message.netId, out GameObject player) || player == null)
            {
                if (!_pendingLifeStates.TryGetValue(message.netId, out var previous) ||
                    message.revision > previous.revision)
                    _pendingLifeStates[message.netId] = message;
                return;
            }

            if (player.TryGetComponent(out PlayerStatsController stats))
                stats.ApplyLifeState(message.lifeState, message.revision);
        }

        void ApplyPendingStats(uint netId)
        {
            if (_pendingStatsStates.Remove(netId, out var stats))
                HandlePlayerStatsState(stats);
            if (_pendingLifeStates.Remove(netId, out var life))
                HandlePlayerLifeState(life);
        }

        void ClearPendingStats()
        {
            _pendingStatsStates.Clear();
            _pendingLifeStates.Clear();
        }
    }
}
