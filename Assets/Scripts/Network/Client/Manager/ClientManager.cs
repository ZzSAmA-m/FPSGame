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
        double _accumulator;
        readonly Dictionary<uint, PredictionBaselineMessage> _pendingBaselines = new();

        public GamePlayerManager Players { get; } = new();
        public event Action SimTick;

        public void Initialize(NetBridge bridge)
        {
            _bridge = bridge ?? throw new ArgumentNullException(nameof(bridge));
            _bridge.ClientHelloReceived += HandleHelloResponse;
            _bridge.PlayerSnapshotReceived += HandlePlayerSnapshot;
            _bridge.PredictionBaselineReceived += HandlePredictionBaseline;
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
            _accumulator = 0d;
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

            _accumulator += Time.unscaledDeltaTime;
            int steps = 0;
            while (_accumulator >= SimTime.Dt && steps < SimTime.MaxStepsPerFrame)
            {
                _accumulator -= SimTime.Dt;
                steps++;
                SimTick?.Invoke();
            }

            if (_accumulator >= SimTime.Dt)
                _accumulator = 0d;
        }

        [Button]
        void DebugShowPlayerCount()
        {
            ZLog.Log<ClientManager>(Players.Players.Count.ToString());
        }
    }
}
