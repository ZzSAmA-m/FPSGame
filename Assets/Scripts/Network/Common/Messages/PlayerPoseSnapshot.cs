using System;
using Mirror;
using UnityEngine;

namespace Network.Common.Messages
{
    public struct PlayerSnapshotMessage:NetworkMessage
    {
        public uint tick;
        public PlayerPose[] playerPoses;
        public float durationTime;
    }
}
