using System;
using System.Collections.Generic;
using Core.ZLog;
using GamePlay.Player.Simulation;
using Network.Common;
using Network.Common.Messages;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Network.Client
{
    public sealed partial class ClientManager : MonoBehaviour
    {
        public static ClientManager Instance { get; private set; }

        NetBridge _bridge;
        readonly SimulationClock _simulationClock = new();
        readonly Dictionary<uint, PredictionBaselineMessage> _pendingBaselines = new();

        public GamePlayerManager Players { get; } = new();
        public event Action SimTick;

        public void Initialize(NetBridge bridge)
        {
            _bridge = bridge ?? throw new ArgumentNullException(nameof(bridge));
            _bridge.ClientHelloReceived += HandleHelloResponse;
            _bridge.PlayerSnapshotReceived += HandlePlayerSnapshot;
            _bridge.PredictionBaselineReceived += HandlePredictionBaseline;
            ResetSimulationClock();
            Instance = this;
            ZLog.Log<ClientManager>("客户端应用层已初始化");
        }

        public void ShutDown()
        {
            if (_bridge == null)
                return;

            _bridge.ClientHelloReceived -= HandleHelloResponse;
            _bridge.PlayerSnapshotReceived -= HandlePlayerSnapshot;
            _bridge.PredictionBaselineReceived -= HandlePredictionBaseline;
            _bridge = null;
            ResetSimulationClock();
            _pendingBaselines.Clear();
            if (Instance == this)
                Instance = null;
        }

        public void SendInputs(uint predictionEpoch, PlayerInputFrame[] frames)
        {
            if (_bridge == null || frames == null || frames.Length == 0)
                return;

            var message = new PlayerInputMessage { predictionEpoch = predictionEpoch, frames = frames };
            _bridge.SendPlayerInput(ref message);
        }

        /// <summary>新预测从收到基准的时刻计时，不补算旧轮次的积压时间。</summary>
        public void ResetSimulationClock()
        {
            _simulationClock.Reset(Time.unscaledTimeAsDouble);
        }

        public bool RequestPredictionResync(uint netId, uint predictionEpoch)
        {
            return _bridge != null && _bridge.SendPredictionResyncRequest(new PredictionResyncRequest
            {
                netId = netId,
                predictionEpoch = predictionEpoch
            });
        }

        public static void RegisterPlayer(uint netId, GameObject player)
        {
            Instance?.Players.Add(netId, player);
            Instance?.ApplyPendingBaseline(netId, player);
        }

        public static void UnregisterPlayer(uint netId)
        {
            Instance?.Players.Remove(netId);
            Instance?._pendingBaselines.Remove(netId);
        }

        void LateUpdate()
        {
            if (_bridge == null)
                return;

            _simulationClock.Advance(Time.unscaledTimeAsDouble);
            int steps = 0;
            while (steps < SimTime.MaxStepsPerFrame && _simulationClock.TryStep())
            {
                steps++;
                SimTick?.Invoke();
            }
        }

        [Button]
        void DebugShowPlayerCount()
        {
            ZLog.Log<ClientManager>(Players.Players.Count.ToString());
        }
    }
}
