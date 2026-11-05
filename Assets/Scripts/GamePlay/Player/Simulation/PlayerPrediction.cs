using System;
using System.Diagnostics;
using GamePlay.Player.Simulation.Reconciliation;
using Mirror;
using Network.Client;
using UnityEngine;

namespace GamePlay.Player.Simulation
{
    /// <summary>本地玩家按拍预测，先比较历史误差，再按需恢复和重放。</summary>
    public sealed class PlayerPrediction : MonoBehaviour
    {
        [SerializeField] ReconciliationSettings _settings = new();

        PlayerBrain _brain;
        NetworkIdentity _identity;
        ClientManager _client;
        PredictionHistory _history;
        PlayerReconciler _reconciler;
        PlayerAuthoritativeSnapshot _pendingSnapshot;
        bool _hasSnapshot;
        bool _hasReceivedTick;
        uint _lastReceivedServerTick;
        uint _tick;
        int _lastReconcileFrame = -1;
        double _lastResyncRequest = double.NegativeInfinity;

        public uint Epoch { get; private set; }
        public uint CurrentInputTick => _tick;
        public uint LastAckInputTick { get; private set; }
        public bool IsInitialized { get; private set; }
        public bool IsResyncing { get; private set; }
        public int SnapshotChecks { get; private set; }
        public int MatchedSnapshotCount { get; private set; }
        public int RollbackCount { get; private set; }
        public int TotalReplayedTicks { get; private set; }
        public int ResyncCount { get; private set; }
        public int LastReplayedTicks { get; private set; }
        public float LastPositionError { get; private set; }
        public double LastReconcileMilliseconds { get; private set; }
        public double PeakReconcileMilliseconds { get; private set; }

        void Awake()
        {
            _brain = GetComponent<PlayerBrain>();
            _identity = GetComponent<NetworkIdentity>();
            _settings ??= new ReconciliationSettings();
            _settings.historyCapacity = Mathf.Clamp(_settings.historyCapacity, 2, 4096);
            _settings.Validate();
            _history = new PredictionHistory(_settings.historyCapacity);
            _reconciler = new PlayerReconciler(GetComponent<PlayerSimulation>(), _history, _settings);
        }

        void OnEnable()
        {
            _client = ClientManager.Instance;
            if (_client != null)
                _client.SimTick += OnSimTick;
        }

        void OnDisable()
        {
            if (_client != null)
                _client.SimTick -= OnSimTick;
            _client = null;
            ResetPrediction();
        }

        public void ResetPrediction()
        {
            Epoch = 0;
            _tick = 0;
            LastAckInputTick = 0;
            IsInitialized = false;
            IsResyncing = false;
            _hasSnapshot = false;
            _hasReceivedTick = false;
            _lastReconcileFrame = -1;
            _history?.Clear();
        }

        /// <summary>可靠基准开启新一轮预测，旧轮次的数据全部作废。</summary>
        public void InitializePrediction(uint epoch, uint baseInputTick, in PlayerSimState state)
        {
            if (epoch == 0 || !PlayerStateValidation.IsFinite(state) ||
                (Epoch != 0 && !TickMath.IsNewer(epoch, Epoch)))
                return;

            _brain.Simulation.RestoreState(state);
            _history.Clear();
            _history.Seed(baseInputTick, state);
            Epoch = epoch;
            _tick = baseInputTick;
            LastAckInputTick = baseInputTick;
            IsInitialized = true;
            IsResyncing = false;
            _hasSnapshot = false;
            _hasReceivedTick = false;
            _lastReconcileFrame = -1;
            _brain.CorrectionSmoother.ResetOffset();
            PlayerInputDriver.Instance?.ResetPendingInput();
            _client?.ResetSimulationClock();
        }

        /// <summary>接收时只保留最新有效快照，不在网络回调里重放。</summary>
        public void QueueSnapshot(in PlayerAuthoritativeSnapshot snapshot)
        {
            if (!IsInitialized || IsResyncing || snapshot.Epoch != Epoch ||
                !PlayerStateValidation.IsFinite(snapshot.State) ||
                (_hasReceivedTick && !TickMath.IsNewer(snapshot.ServerTick, _lastReceivedServerTick)) ||
                TickMath.IsOlder(snapshot.StateInputTick, LastAckInputTick))
                return;

            _pendingSnapshot = snapshot;
            _hasSnapshot = true;
            _hasReceivedTick = true;
            _lastReceivedServerTick = snapshot.ServerTick;
        }

        void OnSimTick()
        {
            if (!IsInitialized || PlayerInputDriver.Instance == null)
                return;
            if (IsResyncing)
            {
                RequestResync();
                return;
            }

            if (_hasSnapshot && _lastReconcileFrame != Time.frameCount && !ReconcilePending())
                return;

            if (_tick == uint.MaxValue)
            {
                BeginResync();
                return;
            }

            PlayerInputFrame frame = PlayerInputDriver.Instance.Consume(++_tick);
            PlayerStepResult result = _brain.Simulation.Step(frame);
            _history.Record(frame, _brain.Simulation.State);
            _brain.Weapon.Present(result);
            _client.SendInputs(Epoch, _history.CopyRecentInputs(SimTime.InputWindow));
        }

        bool ReconcilePending()
        {
            _hasSnapshot = false;
            _lastReconcileFrame = Time.frameCount;
            SnapshotChecks++;
            Vector3 visualPosition = _brain.CorrectionSmoother.VisualPosition;
            long started = Stopwatch.GetTimestamp();
            ReconcileResult result;

            _brain.Simulation.SetReplayMode(true);
            try
            {
                result = _reconciler.Reconcile(_pendingSnapshot, _tick);
            }
            catch (Exception exception)
            {
                UnityEngine.Debug.LogException(exception, this);
                BeginResync();
                return false;
            }
            finally
            {
                _brain.Simulation.SetReplayMode(false);
                LastReconcileMilliseconds =
                    (Stopwatch.GetTimestamp() - started) * 1000d / Stopwatch.Frequency;
                PeakReconcileMilliseconds = Math.Max(PeakReconcileMilliseconds, LastReconcileMilliseconds);
            }

            LastReplayedTicks = result.ReplayedTicks;
            LastPositionError = result.PositionError;
            if (result.NeedsResync)
            {
                BeginResync();
                return false;
            }

            if (result.Status == ReconcileStatus.Matched || result.Status == ReconcileStatus.Replayed)
                LastAckInputTick = result.AcknowledgedTick;
            if (result.Status == ReconcileStatus.Matched)
                MatchedSnapshotCount++;
            if (result.Status == ReconcileStatus.Replayed)
            {
                RollbackCount++;
                TotalReplayedTicks += result.ReplayedTicks;
                _brain.CorrectionSmoother.ApplyCorrection(visualPosition);
            }
            return true;
        }

        void BeginResync()
        {
            if (!IsResyncing)
                ResyncCount++;
            IsResyncing = true;
            _hasSnapshot = false;
            _lastResyncRequest = double.NegativeInfinity;
            RequestResync();
        }

        void RequestResync()
        {
            double now = Time.unscaledTimeAsDouble;
            if (now - _lastResyncRequest < 1d || _client == null || _identity == null)
                return;
            _lastResyncRequest = now;
            _client.RequestPredictionResync(_identity.netId, Epoch);
        }
    }
}
