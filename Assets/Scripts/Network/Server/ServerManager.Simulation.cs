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
        ulong _eventSequence;
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
                if (!session.Player.TryGetComponent(out PlayerBrain brain))
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
                PlayerLiveStepResult result = brain.StepServer(frame);
                session.Inputs.Commit(frame.tick);
                if (result.Fired)
                    PublishFire(session, frame.tick, result);
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

        void PublishFire(ClientSession session, uint inputTick, in PlayerLiveStepResult result)
        {
            var fire = new FireEventMessage
            {
                eventId = ++_eventSequence,
                serverTick = _tick,
                netId = session.Player.netId,
                predictionEpoch = session.PredictionEpoch,
                inputTick = inputTick,
                position = result.State.position,
                origin = result.FireOrigin,
                direction = result.FireDirection
            };
            _bridge.BroadcastEvent(fire);
#if UNITY_EDITOR
            Debug.DrawRay(fire.origin, fire.direction * 100f, Color.red, 3f, false);
#endif
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
                    state = brain.State
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
