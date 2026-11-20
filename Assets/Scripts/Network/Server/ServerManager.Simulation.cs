using System.Collections.Generic;
using Core.ZLog;
using GamePlay.Player;
using GamePlay.Player.Simulation;
using Mirror;
using Network.Client;
using Network.Common.Messages;
using UnityEngine;

namespace Network.Server
{
    public sealed partial class ServerManager : MonoBehaviour
    {
        uint _tick;
        readonly List<ServerPlayerInput> _tickInputs = new();
        readonly Dictionary<uint, PlayerBrain> _tickPlayers = new();
        readonly Dictionary<uint, ClientSession> _tickSessions = new();
        readonly List<NetworkConnectionToClient> _predictionTimeouts = new();

        void SimulateTick()
        {
            _tick++;
            CollectTickInputs();

            GameTickResult result = _gameLogic.Step(_tick, _tickInputs, _tickPlayers);
            foreach (ProcessedServerInput processed in result.ProcessedInputs)
            {
                if (TryGetTickSession(processed.PlayerId, processed.PredictionEpoch, out ClientSession session))
                    session.Inputs.Commit(processed.InputTick);
            }

            foreach (ServerInputFailure failure in result.Failures)
            {
                if (!_tickSessions.TryGetValue(failure.PlayerId, out var session) ||
                    (failure.PredictionEpoch != 0 && session.PredictionEpoch != failure.PredictionEpoch))
                    continue;

                ZLog.LogError<ServerManager>(
                    $"连接 {session.ConnectionId} 处理输入 {failure.InputTick} 失败，已结束会话。\n{failure.Exception}");
                // 先移出模拟，再等待传输层完成断线回调，避免下一拍重复执行失败输入。
                RemoveClient(session.Connection);
                session.Connection.Disconnect();
            }

            _eventPublisher.Publish(result.Events);
            if (result.Shots.Count > 0) LastShots = result.Shots;
            foreach (var shot in result.Shots)
            {
                if (shot.Applied)
                    _bridge.BroadcastHitConfirm(new HitConfirmMessage
                    {
                        shotId = shot.ShotId,
                        serverTick = shot.ServerTick,
                        shooterNetId = shot.ShooterId,
                        targetNetId = shot.Result.TargetId,
                        region = shot.Result.Region,
                        point = shot.Result.Point,
                        killed = shot.Killed
                    });
                if (ShotResolved != null)
                    foreach (System.Action<Combat.ServerShotRecord> observer in ShotResolved.GetInvocationList())
                        try { observer(shot); }
                        catch (System.Exception error) { Debug.LogException(error, this); }
            }

            foreach (NetworkConnectionToClient connection in _predictionTimeouts)
            {
                ZLog.LogWarning<ServerManager>($"连接 {connection.connectionId} 等待首批预测输入超时，已断开。");
                RemoveClient(connection);
                connection.Disconnect();
            }

            if (_tick % SimTime.SnapshotInterval == 0)
                Publish();

            FlushPlayerStatsChanges();
        }

        void CollectTickInputs()
        {
            _tickInputs.Clear();
            _tickPlayers.Clear();
            _tickSessions.Clear();
            _predictionTimeouts.Clear();

            foreach (var pair in Sessions.Clients)
            {
                ClientSession session = pair.Value;
                if (session.State != SessionState.Playing || session.Player == null)
                    continue;
                if (!session.Player.TryGetComponent(out PlayerBrain brain))
                    continue;
                uint playerId = session.Player.netId;
                _tickPlayers.Add(playerId, brain);
                _tickSessions.Add(playerId, session);

                if (session.AwaitingInput)
                {
                    if (!session.Inputs.HasPending)
                    {
                        if (Time.unscaledTimeAsDouble - session.PredictionBaselineSentTime >=
                            Mathf.Max(1f, _firstInputTimeoutSeconds))
                            _predictionTimeouts.Add(session.Connection);
                        continue;
                    }
                    if (unchecked((int)(_tick - session.InputStartServerTick)) < 0)
                        continue;
                    session.AwaitingInput = false;
                }

                PlayerInputFrame frame = session.Inputs.ResolveNext();
                _tickInputs.Add(new ServerPlayerInput(playerId, session.PredictionEpoch, brain, frame,
                    session.Inputs.ResolvedReceiptTick, session.Connection.rtt));
            }
        }

        bool TryGetTickSession(uint playerId, uint predictionEpoch, out ClientSession session)
        {
            if (!_tickSessions.TryGetValue(playerId, out session))
                return false;

            return session.PredictionEpoch == predictionEpoch &&
                   Sessions.TryGet(session.ConnectionId, out ClientSession current) &&
                   ReferenceEquals(current, session);
        }

        void Publish()
        {
            var poses = new List<PlayerSnapshotPose>();
            foreach (var pair in Sessions.Clients)
            {
                ClientSession session = pair.Value;
                if (session.State != SessionState.Playing || session.Player == null || session.PredictionEpoch == 0)
                    continue;
                if (!session.Player.TryGetComponent(out PlayerBrain brain))
                    continue;

                poses.Add(new PlayerSnapshotPose
                {
                    netId = session.Player.netId,
                    predictionEpoch = session.PredictionEpoch,
                    lastProcessedInputTick = session.Inputs.LastProcessedTick,
                    state = brain.State,
                    combatPose = brain.CombatPose != null ? brain.CombatPose.CapturePose() : default,
                    combatContinuity = brain.Hitboxes != null ? brain.Hitboxes.ContinuityGeneration : 0
                });
            }

            if (poses.Count == 0)
                return;

            var snapshot = new PlayerSnapshotMessage
            {
                serverTick = _tick,
                serverTime = SimulationServerTime,
                players = poses.ToArray()
            };
            _bridge.SendPlayerSnapshot(ref snapshot);
        }

#if UNITY_EDITOR
        void OnDrawGizmosSelected()
        {
            foreach (var shot in LastShots)
            {
                Gizmos.color = shot.Applied ? Color.green : Color.yellow;
                Gizmos.DrawRay(shot.Ray.origin, shot.Ray.direction * Mathf.Min(200f, shot.Result.Distance));
                var geometry = shot.TargetGeometry;
                if (!geometry.IsValid) continue;
                Gizmos.color = Color.red;
                Gizmos.DrawWireSphere(geometry.Head.Center, geometry.Head.Radius);
                Gizmos.color = Color.cyan;
                var body = geometry.Body;
                Gizmos.DrawWireSphere(body.PointA, body.Radius);
                Gizmos.DrawWireSphere(body.PointB, body.Radius);
                Vector3 axis = (body.PointB - body.PointA).normalized;
                Vector3 right = Vector3.Cross(axis, Vector3.up);
                if (right.sqrMagnitude < 0.001f) right = Vector3.Cross(axis, Vector3.right);
                right = right.normalized * body.Radius;
                Vector3 forward = Vector3.Cross(axis, right).normalized * body.Radius;
                foreach (Vector3 offset in new[] { right, -right, forward, -forward })
                    Gizmos.DrawLine(body.PointA + offset, body.PointB + offset);
            }
        }
#endif
    }
}
