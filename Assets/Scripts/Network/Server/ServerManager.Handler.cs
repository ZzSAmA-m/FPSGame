using Core.ZLog;
using GamePlay.Player.Simulation;
using Mirror;
using Network.Client;
using Network.Common;
using Network.Common.Messages;
using UnityEngine;

namespace Network.Server
{
    public sealed partial class ServerManager : MonoBehaviour
    {
        void HandleHelloRequest(NetworkConnectionToClient connection, HelloRequest request)
        {
            if (!Sessions.TryGet(connection.connectionId, out ClientSession session))
                return;

            if (request.protocolVersion != NetworkProtocol.Version)
            {
                RejectAndDisconnect(
                    connection,
                    $"协议不兼容:client={request.protocolVersion}, server={NetworkProtocol.Version}");
                return;
            }

            session.ClientVersion = SanitizeClientVersion(request.clientVersion);
            session.State = SessionState.ProtocolVerified;
            _bridge.SendHelloToClient(connection, new HelloResponse
            {
                protocolVersion = NetworkProtocol.Version,
                accepted = true,
                message = "协议握手成功。"
            });
            ZLog.Log<ServerManager>($"协议握手通过：{session}");
        }

        public void MarkReady(NetworkConnectionToClient connection)
        {
            if (!Sessions.TryGet(connection.connectionId, out ClientSession session))
                return;
            if (session.State == SessionState.ProtocolVerified)
                session.State = SessionState.Ready;
        }

        public void BindPlayer(NetworkConnectionToClient connection)
        {
            if (!Sessions.TryGet(connection.connectionId, out ClientSession session))
                return;
            if (session.State != SessionState.Ready)
            {
                ZLog.LogWarning<ServerManager>($"连接 {connection.connectionId} 当前是 {session.State}，玩家生成后不会进入模拟。");
                return;
            }

            session.Player = connection.identity;
            session.State = SessionState.Playing;
            BeginPredictionEpoch(session);
        }

        void HandlePlayerInput(NetworkConnectionToClient sender, PlayerInputMessage message)
        {
            if (!Sessions.TryGet(sender.connectionId, out ClientSession session))
                return;
            if (session.State != SessionState.Playing || message.frames == null
                || message.predictionEpoch != session.PredictionEpoch)
                return;

            bool hadPending = session.Inputs.HasPending;
            int count = Mathf.Min(message.frames.Length, SimTime.InputWindow);
            for (int i = 0; i < count; i++)
                session.Inputs.Push(message.frames[i]);

            if (session.AwaitingInput && !hadPending && session.Inputs.HasPending)
                session.InputStartServerTick = unchecked(_tick + (uint)_inputBufferTicks);
        }

        void HandlePredictionResync(NetworkConnectionToClient sender, PredictionResyncRequest request)
        {
            if (!Sessions.TryGet(sender.connectionId, out ClientSession session)
                || session.State != SessionState.Playing || session.Player == null
                || request.netId != session.Player.netId || request.predictionEpoch != session.PredictionEpoch)
                return;

            double now = Time.unscaledTimeAsDouble;
            if (now - session.LastResyncTime < 0.25d)
                return;

            session.LastResyncTime = now;
            BeginPredictionEpoch(session);
        }

        void BeginPredictionEpoch(ClientSession session)
        {
            if (session.Player == null || !session.Player.TryGetComponent(out IPlayerSimulation simulation))
                return;

            _predictionEpoch = unchecked(_predictionEpoch + 1);
            if (_predictionEpoch == 0)
                _predictionEpoch = 1;
            session.PredictionEpoch = _predictionEpoch;
            session.Inputs.Reset();
            session.AwaitingInput = true;
            session.InputStartServerTick = 0;
            session.PredictionBaselineSentTime = Time.unscaledTimeAsDouble;

            _bridge.SendPredictionBaseline(session.Connection, new PredictionBaselineMessage
            {
                netId = session.Player.netId,
                predictionEpoch = session.PredictionEpoch,
                baseInputTick = 0,
                state = simulation.State
            });
        }

        void RejectAndDisconnect(NetworkConnectionToClient connection, string reason)
        {
            Debug.LogWarning($"[ServerManager] 拒绝连接 {connection}:{reason}");
            _bridge.SendHelloToClient(connection, new HelloResponse
            {
                protocolVersion = NetworkProtocol.Version,
                accepted = false,
                message = reason
            });
            connection?.Disconnect();
        }

        static string SanitizeClientVersion(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "unknown";

            string trimmed = value.Trim();
            return trimmed.Length <= NetworkProtocol.MaxClientVersionLength
                ? trimmed
                : trimmed.Substring(0, NetworkProtocol.MaxClientVersionLength);
        }
    }
}
