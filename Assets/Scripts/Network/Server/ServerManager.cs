using System;
using Core.ZLog;
using GamePlay.Player.Simulation;
using Mirror;
using Network.Client;
using Network.Common;
using UnityEngine;

namespace Network.Server
{
    /// <summary>管理服务器会话、输入队列和固定步长模拟。</summary>
    public sealed partial class ServerManager : MonoBehaviour
    {
        NetBridge _bridge;
        bool _initialized;
        readonly SimulationClock _simulationClock = new();
        uint _predictionEpoch;
        [SerializeField, Range(0, 6)] int _inputBufferTicks = 2;
        [SerializeField, Min(1f)] float _firstInputTimeoutSeconds = 10f;

        public ClientSessions Sessions { get; } = new();
        uint PendingSimulationTicks => _simulationClock.PendingTicks(Time.unscaledTimeAsDouble);

        public void Initialize(NetBridge bridge)
        {
            if (_initialized)
                throw new InvalidOperationException("[ServerManager] 不能重复初始化服务器运行时。");

            _bridge = bridge ?? throw new ArgumentNullException(nameof(bridge));
            _bridge.ServerHelloReceived += HandleHelloRequest;
            _bridge.PlayerInputReceived += HandlePlayerInput;
            _bridge.PredictionResyncRequested += HandlePredictionResync;
            _tick = 0;
            _predictionEpoch = 0;
            _simulationClock.Reset(Time.unscaledTimeAsDouble);
            _initialized = true;
            ZLog.Log<ServerManager>("服务器应用层已初始化");
        }

        public void Shutdown()
        {
            if (!_initialized)
                return;

            _bridge.ServerHelloReceived -= HandleHelloRequest;
            _bridge.PlayerInputReceived -= HandlePlayerInput;
            _bridge.PredictionResyncRequested -= HandlePredictionResync;
            _bridge = null;
            _initialized = false;
            _simulationClock.Reset(Time.unscaledTimeAsDouble);
            _tick = 0;
        }

        public void AddClient(NetworkConnectionToClient connection)
        {
            Sessions.Add(connection);
        }

        public void RemoveClient(NetworkConnectionToClient connection)
        {
            Sessions.Remove(connection);
        }

        void LateUpdate()
        {
            if (!_initialized)
                return;

            _simulationClock.Advance(Time.unscaledTimeAsDouble);
            int steps = 0;
            while (steps < SimTime.MaxStepsPerFrame && _simulationClock.TryStep())
            {
                steps++;
                SimulateTick();
            }
        }

        void OnDestroy()
        {
            Shutdown();
        }
    }
}
