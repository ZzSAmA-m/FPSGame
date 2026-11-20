using System;
using System.Collections.Generic;
using Network.Server.Combat;

namespace Network.Server
{
    /// <summary>一次服务器玩法步进完成后交给调度层的独立只读结果。</summary>
    public sealed class GameTickResult
    {
        public IReadOnlyList<ProcessedServerInput> ProcessedInputs { get; }
        public IReadOnlyList<ServerGameEvent> Events { get; }
        public IReadOnlyList<ServerInputFailure> Failures { get; }
        public IReadOnlyList<ServerShotRecord> Shots { get; }

        internal GameTickResult(
            List<ProcessedServerInput> processedInputs,
            List<ServerGameEvent> events,
            List<ServerInputFailure> failures, List<ServerShotRecord> shots = null)
        {
            ProcessedInputs = processedInputs.Count == 0
                ? Array.Empty<ProcessedServerInput>()
                : processedInputs.AsReadOnly();
            Events = events.Count == 0
                ? Array.Empty<ServerGameEvent>()
                : events.AsReadOnly();
            Failures = failures.Count == 0
                ? Array.Empty<ServerInputFailure>()
                : failures.AsReadOnly();
            Shots = shots == null || shots.Count == 0 ? Array.Empty<ServerShotRecord>() : shots.AsReadOnly();
        }
    }

    /// <summary>已完成玩家步进、可以由调度层确认的输入标识。</summary>
    public readonly struct ProcessedServerInput
    {
        public uint PlayerId { get; }
        public uint PredictionEpoch { get; }
        public uint InputTick { get; }

        public ProcessedServerInput(uint playerId, uint predictionEpoch, uint inputTick)
        {
            PlayerId = playerId;
            PredictionEpoch = predictionEpoch;
            InputTick = inputTick;
        }
    }

    /// <summary>某个玩家输入处理失败时的标识和原始异常。</summary>
    public readonly struct ServerInputFailure
    {
        public uint PlayerId { get; }
        public uint PredictionEpoch { get; }
        public uint InputTick { get; }
        public Exception Exception { get; }

        public ServerInputFailure(uint playerId, uint predictionEpoch, uint inputTick, Exception exception)
        {
            PlayerId = playerId;
            PredictionEpoch = predictionEpoch;
            InputTick = inputTick;
            Exception = exception;
        }
    }
}
