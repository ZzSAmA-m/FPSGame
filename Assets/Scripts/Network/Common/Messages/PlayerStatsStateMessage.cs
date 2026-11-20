using GamePlay.Player;
using Mirror;

namespace Network.Common.Messages
{
    /// <summary>可靠发送给所属客户端的玩家数值完整状态。</summary>
    public struct PlayerStatsStateMessage : NetworkMessage
    {
        public uint netId;
        public PlayerStatsState state;
    }

    /// <summary>显式序列化只读数值状态，固定字段顺序。</summary>
    public static class PlayerStatsStateSerialization
    {
        public static void WritePlayerStatsState(this NetworkWriter writer, PlayerStatsState state)
        {
            writer.WriteInt(state.Health);
            writer.WriteInt(state.MaxHealth);
            writer.WriteInt(state.Armor);
            writer.WriteInt(state.MaxArmor);
            writer.WriteULong(state.Revision);
        }

        public static PlayerStatsState ReadPlayerStatsState(this NetworkReader reader)
        {
            int health = reader.ReadInt();
            int maxHealth = reader.ReadInt();
            int armor = reader.ReadInt();
            int maxArmor = reader.ReadInt();
            ulong revision = reader.ReadULong();
            return new PlayerStatsState(health, maxHealth, armor, maxArmor, revision);
        }
    }
}
