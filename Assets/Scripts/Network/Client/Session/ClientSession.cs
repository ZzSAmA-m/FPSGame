using Mirror;
using Network.Server;

namespace Network.Client
{
    public enum SessionState
    {
        Connected,
        ProtocolVerified,
        Ready,
        Playing
    }

    public sealed class ClientSession
    {
        public int ConnectionId { get; }
        public NetworkConnectionToClient Connection { get; }
        public SessionState State { get; set; }
        public string ClientVersion { get; set; }
        public NetworkIdentity Player { get; set; }
        public PlayerInputQueue Inputs { get; } = new();
        public uint PredictionEpoch { get; set; }
        public bool AwaitingInput { get; set; }
        public uint InputStartServerTick { get; set; }
        public double PredictionBaselineSentTime { get; set; }
        public double LastResyncTime { get; set; } = double.NegativeInfinity;

        public ClientSession(int connectionId, NetworkConnectionToClient connection)
        {
            ConnectionId = connectionId;
            Connection = connection;
            State = SessionState.Connected;
            ClientVersion = "unknown";
        }

        public override string ToString()
        {
            uint netId = Player != null ? Player.netId : 0;
            return $"conn={ConnectionId} state={State} version={ClientVersion} netId={netId} epoch={PredictionEpoch} ack={Inputs.LastProcessedTick}";
        }
    }
}
