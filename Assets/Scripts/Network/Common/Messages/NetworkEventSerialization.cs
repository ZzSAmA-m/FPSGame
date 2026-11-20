using System;
using System.IO;
using GamePlay.HoldSystem;
using Mirror;

namespace Network.Common.Messages
{
    /// <summary>网络协议中的稳定事件类型码；已发布的值不可修改或复用。</summary>
    public enum NetworkEventKind : byte
    {
        Fire = 1,
        WeaponSwitch = 2,
        Reload = 3,
        Prepare = 4,
        ActionCanceled = 5,
        KnifeAttack = 6
    }

    /// <summary>显式编码事件类型和字段，不依赖 CLR 类型名或运行时哈希。</summary>
    public static class NetworkEventSerialization
    {
        public static void WriteNetworkEventMessage(this NetworkWriter writer, NetworkEventMessage message)
        {
            if (message == null)
                throw new ArgumentNullException(nameof(message));
            NetworkEventKind kind = message switch
            {
                FireEventMessage => NetworkEventKind.Fire,
                WeaponSwitchEventMessage => NetworkEventKind.WeaponSwitch,
                ReloadEventMessage => NetworkEventKind.Reload,
                PrepareEventMessage => NetworkEventKind.Prepare,
                WeaponActionCanceledEventMessage => NetworkEventKind.ActionCanceled,
                KnifeAttackEventMessage => NetworkEventKind.KnifeAttack,
                _ => throw new ArgumentException($"不支持的网络事件类型：{message.GetType().FullName}。", nameof(message))
            };

            writer.WriteByte((byte)kind);
            writer.WriteULong(message.eventId);
            writer.WriteUInt(message.serverTick);
            writer.WriteUInt(message.netId);
            switch (message)
            {
                case FireEventMessage fire:
                    writer.WriteUInt(fire.predictionEpoch);
                    writer.WriteUInt(fire.inputTick);
                    writer.WriteByte((byte)fire.weaponSlot);
                    writer.WriteVector3(fire.position);
                    writer.WriteVector3(fire.origin);
                    writer.WriteVector3(fire.direction);
                    break;
                case WeaponSwitchEventMessage weaponSwitch:
                    writer.WriteUInt(weaponSwitch.predictionEpoch);
                    writer.WriteUInt(weaponSwitch.inputTick);
                    writer.WriteByte((byte)weaponSwitch.weaponSlot);
                    writer.WriteBool(weaponSwitch.serverInitiated);
                    break;
                case ReloadEventMessage reload:
                    writer.WriteUInt(reload.predictionEpoch);
                    writer.WriteUInt(reload.inputTick);
                    writer.WriteByte((byte)reload.weaponSlot);
                    writer.WriteBool(reload.serverInitiated);
                    break;
                case PrepareEventMessage prepare:
                    writer.WriteUInt(prepare.predictionEpoch);
                    writer.WriteUInt(prepare.inputTick);
                    writer.WriteByte((byte)prepare.weaponSlot);
                    writer.WriteBool(prepare.serverInitiated);
                    break;
                case WeaponActionCanceledEventMessage canceled:
                    writer.WriteUInt(canceled.predictionEpoch);
                    writer.WriteUInt(canceled.inputTick);
                    writer.WriteByte((byte)canceled.weaponSlot);
                    writer.WriteByte((byte)canceled.canceledState);
                    writer.WriteBool(canceled.serverInitiated);
                    break;
                case KnifeAttackEventMessage attack:
                    writer.WriteUInt(attack.predictionEpoch);
                    writer.WriteUInt(attack.inputTick);
                    writer.WriteByte((byte)attack.weaponSlot);
                    writer.WriteByte((byte)attack.attackKind);
                    break;
            }
        }

        public static NetworkEventMessage ReadNetworkEventMessage(this NetworkReader reader)
        {
            var kind = (NetworkEventKind)reader.ReadByte();
            NetworkEventMessage message = kind switch
            {
                NetworkEventKind.Fire => new FireEventMessage(),
                NetworkEventKind.WeaponSwitch => new WeaponSwitchEventMessage(),
                NetworkEventKind.Reload => new ReloadEventMessage(),
                NetworkEventKind.Prepare => new PrepareEventMessage(),
                NetworkEventKind.ActionCanceled => new WeaponActionCanceledEventMessage(),
                NetworkEventKind.KnifeAttack => new KnifeAttackEventMessage(),
                _ => throw new InvalidDataException($"未知的网络事件类型码：{(byte)kind}。")
            };
            message.eventId = reader.ReadULong();
            message.serverTick = reader.ReadUInt();
            message.netId = reader.ReadUInt();
            switch (message)
            {
                case FireEventMessage fire:
                    fire.predictionEpoch = reader.ReadUInt();
                    fire.inputTick = reader.ReadUInt();
                    fire.weaponSlot = (WeaponSlot)reader.ReadByte();
                    fire.position = reader.ReadVector3();
                    fire.origin = reader.ReadVector3();
                    fire.direction = reader.ReadVector3();
                    break;
                case WeaponSwitchEventMessage weaponSwitch:
                    weaponSwitch.predictionEpoch = reader.ReadUInt();
                    weaponSwitch.inputTick = reader.ReadUInt();
                    weaponSwitch.weaponSlot = (WeaponSlot)reader.ReadByte();
                    weaponSwitch.serverInitiated = reader.ReadBool();
                    break;
                case ReloadEventMessage reload:
                    reload.predictionEpoch = reader.ReadUInt();
                    reload.inputTick = reader.ReadUInt();
                    reload.weaponSlot = (WeaponSlot)reader.ReadByte();
                    reload.serverInitiated = reader.ReadBool();
                    break;
                case PrepareEventMessage prepare:
                    prepare.predictionEpoch = reader.ReadUInt();
                    prepare.inputTick = reader.ReadUInt();
                    prepare.weaponSlot = (WeaponSlot)reader.ReadByte();
                    prepare.serverInitiated = reader.ReadBool();
                    break;
                case WeaponActionCanceledEventMessage canceled:
                    canceled.predictionEpoch = reader.ReadUInt();
                    canceled.inputTick = reader.ReadUInt();
                    canceled.weaponSlot = (WeaponSlot)reader.ReadByte();
                    canceled.canceledState = (GunState)reader.ReadByte();
                    canceled.serverInitiated = reader.ReadBool();
                    break;
                case KnifeAttackEventMessage attack:
                    attack.predictionEpoch = reader.ReadUInt();
                    attack.inputTick = reader.ReadUInt();
                    attack.weaponSlot = (WeaponSlot)reader.ReadByte();
                    attack.attackKind = (KnifeAttackKind)reader.ReadByte();
                    break;
            }
            return message;
        }
    }
}
