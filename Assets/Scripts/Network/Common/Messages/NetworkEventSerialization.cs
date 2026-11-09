using System;
using System.IO;
using Mirror;

namespace Network.Common.Messages
{
    /// <summary>网络协议中的稳定事件类型码；已发布的值不可修改或复用。</summary>
    public enum NetworkEventKind : byte
    {
        Fire = 1
    }

    /// <summary>显式编码事件类型和字段，不依赖 CLR 类型名或运行时哈希。</summary>
    public static class NetworkEventSerialization
    {
        public static void WriteNetworkEventMessage(this NetworkWriter writer, NetworkEventMessage message)
        {
            if (message == null)
                throw new ArgumentNullException(nameof(message));
            if (message is not FireEventMessage fire)
                throw new ArgumentException($"不支持的网络事件类型：{message.GetType().FullName}。", nameof(message));

            writer.WriteByte((byte)NetworkEventKind.Fire);
            writer.WriteULong(fire.eventId);
            writer.WriteUInt(fire.serverTick);
            writer.WriteUInt(fire.netId);
            writer.WriteUInt(fire.predictionEpoch);
            writer.WriteUInt(fire.inputTick);
            writer.WriteVector3(fire.position);
            writer.WriteVector3(fire.origin);
            writer.WriteVector3(fire.direction);
        }

        public static NetworkEventMessage ReadNetworkEventMessage(this NetworkReader reader)
        {
            var kind = (NetworkEventKind)reader.ReadByte();
            if (kind != NetworkEventKind.Fire)
                throw new InvalidDataException($"未知的网络事件类型码：{(byte)kind}。");

            return new FireEventMessage
            {
                eventId = reader.ReadULong(),
                serverTick = reader.ReadUInt(),
                netId = reader.ReadUInt(),
                predictionEpoch = reader.ReadUInt(),
                inputTick = reader.ReadUInt(),
                position = reader.ReadVector3(),
                origin = reader.ReadVector3(),
                direction = reader.ReadVector3()
            };
        }
    }
}
