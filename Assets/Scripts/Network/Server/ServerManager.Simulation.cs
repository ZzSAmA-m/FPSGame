using System.Collections.Generic;
using Core.ZLog;
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
        readonly List<NetworkConnectionToClient> _predictionTimeouts = new();

        void SimulateTick()
        {
            _tick++;
            _predictionTimeouts.Clear();

            foreach (var pair in Sessions.Clients)
            {
                ClientSession session = pair.Value;
                if (session.State != SessionState.Playing || session.Player == null)
                    continue;
                if (!session.Player.TryGetComponent(out PlayerSimulation simulation))
                    continue;

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
                PlayerStepResult result = simulation.Step(frame);
                session.Inputs.Commit(frame.tick);
                if (session.Player.TryGetComponent(out PlayerWeaponController weapon))
                    weapon.Present(result);
            }

            // 断线回调会移除会话，放在枚举结束后处理。
            foreach (NetworkConnectionToClient connection in _predictionTimeouts)
            {
                ZLog.LogWarning<ServerManager>($"连接 {connection.connectionId} 等待首批预测输入超时，已断开。");
                connection.Disconnect();
            }

            if (_tick % SimTime.SnapshotInterval == 0)
                Publish();
        }

        void Publish()
        {
            var poses = new List<PlayerSnapshotPose>();
            foreach (var pair in Sessions.Clients)
            {
                ClientSession session = pair.Value;
                if (session.State != SessionState.Playing || session.Player == null || session.PredictionEpoch == 0)
                    continue;
                if (!session.Player.TryGetComponent(out IPlayerSimulation simulation))
                    continue;

                poses.Add(new PlayerSnapshotPose
                {
                    netId = session.Player.netId,
                    predictionEpoch = session.PredictionEpoch,
                    lastProcessedInputTick = session.Inputs.LastProcessedTick,
                    state = simulation.State
                });
            }

            if (poses.Count == 0)
                return;

            var snapshot = new PlayerSnapshotMessage
            {
                serverTick = _tick,
                players = poses.ToArray()
            };
            _bridge.SendPlayerSnapshot(ref snapshot);
        }
    }
}
