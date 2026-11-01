using Core.ZLog;
using GamePlay.Player;
using GamePlay.Player.Presentation;
using GamePlay.Player.Simulation;
using GamePlay.Player.Simulation.Reconciliation;
using Network.Common.Messages;
using UnityEngine;

namespace Network.Client
{
    public sealed partial class ClientManager : MonoBehaviour
    {
        void HandleHelloResponse(HelloResponse response)
        {
            if (!response.accepted)
            {
                Debug.LogError($"[ClientManager] 服务器拒绝协议握手：{response.message} (serverProtocol={response.protocolVersion})");
                return;
            }

            Debug.Log($"[ClientManager] 协议握手完成：{response.message} (protocol={response.protocolVersion})");
        }

        void HandlePlayerSnapshot(PlayerSnapshotMessage message)
        {
            if (message.players == null)
                return;

            for (int i = 0; i < message.players.Length; i++)
            {
                PlayerSnapshotPose pose = message.players[i];
                if (!Players.Players.TryGetValue(pose.netId, out GameObject player))
                    continue;

                PlayerBrain brain = player.GetComponent<PlayerBrain>();
                if (brain.Role == PawnRole.Predicted)
                    brain.Prediction.QueueSnapshot(new PlayerAuthoritativeSnapshot
                    {
                        Epoch = pose.predictionEpoch,
                        ServerTick = message.serverTick,
                        StateInputTick = pose.lastProcessedInputTick,
                        State = pose.state
                    });
                else if (brain.Role == PawnRole.Interpolated)
                    brain.Interpolator.Push(message.serverTick, pose.state, pose.predictionEpoch);
            }

            ZLog.Log<ClientManager>("Received Snapshot: " + message, frequency: LogFrequency.Extreme);
        }

        /// <summary>基准可能先于玩家对象到达，暂存到对象注册完成。</summary>
        void HandlePredictionBaseline(PredictionBaselineMessage message)
        {
            if (message.predictionEpoch == 0 || !PlayerStateValidation.IsFinite(message.state))
                return;
            if (!Players.Players.TryGetValue(message.netId, out GameObject player))
            {
                if (!_pendingBaselines.TryGetValue(message.netId, out var previous) ||
                    TickMath.IsNewer(message.predictionEpoch, previous.predictionEpoch))
                    _pendingBaselines[message.netId] = message;
                return;
            }

            PlayerBrain brain = player.GetComponent<PlayerBrain>();
            if (brain.Role == PawnRole.Predicted)
                brain.Prediction.InitializePrediction(message.predictionEpoch, message.baseInputTick, message.state);
        }

        void ApplyPendingBaseline(uint netId, GameObject player)
        {
            if (!_pendingBaselines.TryGetValue(netId, out var message))
                return;
            _pendingBaselines.Remove(netId);
            HandlePredictionBaseline(message);
        }
    }
}
