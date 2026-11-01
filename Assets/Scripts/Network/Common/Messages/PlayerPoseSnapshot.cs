using System.Text;
using GamePlay.Player.Simulation;
using Mirror;

namespace Network.Common.Messages
{
    public struct PlayerSnapshotPose
    {
        public uint netId;
        public uint predictionEpoch;
        // 此状态对应这一拍执行结束后，替代输入也计入确认。
        public uint lastProcessedInputTick;
        public PlayerSimState state;
    }

    public struct PlayerSnapshotMessage : NetworkMessage
    {
        public uint serverTick;
        public PlayerSnapshotPose[] players;

        public override string ToString()
        {
            var text = new StringBuilder();
            text.Append("PlayerSnapshot tick=").Append(serverTick);
            if (players == null)
                return text.ToString();

            for (int i = 0; i < players.Length; i++)
            {
                PlayerSnapshotPose pose = players[i];
                text.Append("\n id=").Append(pose.netId)
                    .Append(" epoch=").Append(pose.predictionEpoch)
                    .Append(" ack=").Append(pose.lastProcessedInputTick)
                    .Append(" pos=").Append(pose.state.position);
            }

            return text.ToString();
        }
    }
}
