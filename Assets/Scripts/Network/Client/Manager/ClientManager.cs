using System;
using System.Collections.Generic;
using Core.ZLog;
using GamePlay.Player;
using GamePlay.Player.Presentation;
using GamePlay.Player.Simulation;
using Network.Common;
using Network.Common.Messages;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Network.Client
{
    [DefaultExecutionOrder(-1000)]
    public sealed partial class ClientManager : MonoBehaviour
    {
        public static ClientManager Instance { get; private set; }

        NetBridge _bridge;
        EventMessageHandler _eventMessageHandler;
        readonly SimulationClock _simulationClock = new();
        readonly Dictionary<uint, PredictionBaselineMessage> _pendingBaselines = new();
        double _pendingPresentedTick = double.NaN;
        uint _pendingPresentedFromTick;
        uint _pendingPresentedToTick;

        public GamePlayerManager Players { get; } = new();
        public ClientWorldTimeline WorldTimeline { get; } = new();
        public double LastPresentedServerTick => WorldTimeline.LastPresentedServerTick;
        public uint LastPresentedFromTick => WorldTimeline.LastPresentedFromTick;
        public uint LastPresentedToTick => WorldTimeline.LastPresentedToTick;
        public event Action SimTick;

        public void Initialize(NetBridge bridge)
        {
            _bridge = bridge ?? throw new ArgumentNullException(nameof(bridge));
            _eventMessageHandler = new EventMessageHandler(Players);
            _bridge.ClientHelloReceived += HandleHelloResponse;
            _bridge.PlayerSnapshotReceived += HandlePlayerSnapshot;
            _bridge.PredictionBaselineReceived += HandlePredictionBaseline;
            _bridge.NetworkEventReceived += HandleNetworkEvent;
            _bridge.PlayerStatsStateReceived += HandlePlayerStatsState;
            _bridge.PlayerLifeStateReceived += HandlePlayerLifeState;
            _bridge.HitConfirmReceived += HandleHitConfirm;
            WorldTimeline.Reset();
            _pendingPresentedTick = double.NaN;
            _lastHitConfirmShotId = 0;
            ResetSimulationClock();
            Instance = this;
            ZLog.Log<ClientManager>("客户端应用层已初始化");
        }

        public void ShutDown()
        {
            _eventMessageHandler = null;
            if (_bridge != null)
            {
                _bridge.ClientHelloReceived -= HandleHelloResponse;
                _bridge.PlayerSnapshotReceived -= HandlePlayerSnapshot;
                _bridge.PredictionBaselineReceived -= HandlePredictionBaseline;
                _bridge.NetworkEventReceived -= HandleNetworkEvent;
                _bridge.PlayerStatsStateReceived -= HandlePlayerStatsState;
                _bridge.PlayerLifeStateReceived -= HandlePlayerLifeState;
                _bridge.HitConfirmReceived -= HandleHitConfirm;
            }
            _bridge = null;
            ResetSimulationClock();
            _pendingBaselines.Clear();
            ClearPendingStats();
            WorldTimeline.Reset();
            _pendingPresentedTick = double.NaN;
            _lastHitConfirmShotId = 0;
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
            Instance?.ApplyPendingStats(netId);
        }

        public static void UnregisterPlayer(uint netId)
        {
            Instance?.Players.Remove(netId);
            Instance?._pendingBaselines.Remove(netId);
            Instance?._pendingStatsStates.Remove(netId);
            Instance?._pendingLifeStates.Remove(netId);
        }

        void LateUpdate()
        {
            if (_bridge == null)
                return;

            WorldTimeline.MarkPresented(_pendingPresentedTick, _pendingPresentedFromTick, _pendingPresentedToTick);
            _simulationClock.Advance(Time.unscaledTimeAsDouble);
            int steps = 0;
            while (steps < SimTime.MaxStepsPerFrame && _simulationClock.TryStep())
            {
                steps++;
                SimTick?.Invoke();
            }
        }

        void Update()
        {
            _pendingPresentedTick = double.NaN;
            if (_bridge == null)
                return;

            if (!WorldTimeline.TryGetRenderTick(Time.unscaledTimeAsDouble, out double renderTick))
                return;

            double oldest = 0d;
            double latest = double.PositiveInfinity;
            bool hasRemoteSamples = false;
            foreach (GameObject player in Players.Players.Values)
            {
                if (player == null || !player.TryGetComponent(out PlayerBrain brain) ||
                    brain.Role != PawnRole.Interpolated || brain.Interpolator == null ||
                    !brain.Interpolator.TryGetRange(out double playerOldest, out double playerLatest))
                    continue;
                oldest = Math.Max(oldest, playerOldest);
                latest = Math.Min(latest, playerLatest);
                hasRemoteSamples = true;
            }

            if (!hasRemoteSamples)
            {
                _pendingPresentedTick = WorldTimeline.LatestAnchorTick;
                _pendingPresentedFromTick = WorldTimeline.LatestAnchorTick;
                _pendingPresentedToTick = WorldTimeline.LatestAnchorTick;
                return;
            }
            if (oldest > latest || latest < WorldTimeline.LastPresentedServerTick)
                return;
            renderTick = Math.Max(oldest, Math.Min(latest, renderTick));

            bool hasBracket = false;
            uint fromTick = 0;
            uint toTick = 0;
            foreach (GameObject player in Players.Players.Values)
            {
                if (player == null || !player.TryGetComponent(out PlayerBrain brain) ||
                    brain.Role != PawnRole.Interpolated || brain.Interpolator == null ||
                    !brain.Interpolator.TryGetRange(out _, out _))
                    continue;
                if (!brain.Interpolator.TryGetBracket(renderTick, out uint playerFrom, out uint playerTo))
                    return;
                if (hasBracket && (fromTick != playerFrom || toTick != playerTo))
                    return;
                fromTick = playerFrom;
                toTick = playerTo;
                hasBracket = true;
            }
            foreach (GameObject player in Players.Players.Values)
            {
                if (player != null && player.TryGetComponent(out PlayerBrain brain) &&
                    brain.Role == PawnRole.Interpolated && brain.Interpolator != null)
                    brain.Interpolator.Present(renderTick);
            }
            _pendingPresentedTick = renderTick;
            _pendingPresentedFromTick = fromTick;
            _pendingPresentedToTick = toTick;
        }

        [Button]
        void DebugShowPlayerCount()
        {
            ZLog.Log<ClientManager>(Players.Players.Count.ToString());
        }
    }
}
