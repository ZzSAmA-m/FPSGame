using System;
using System.Text;
using Mirror;
using UnityEngine;

namespace Network.Common.Messages
{
    public struct PlayerSnapshotMessage:NetworkMessage
    {
        public uint tick;
        public PlayerPose[] playerPoses;
        public float durationTime;

        public override string ToString()
        {
            string msg=$"PlayerSnapshot: tick = {tick} , durationTime = {durationTime}\n";
            foreach(var pose in playerPoses)
            {
                msg+=$"id={pose.netId} position={pose.position} facingDirection={pose.facingDirection}\n ";
            }
            return msg;
        }
    }
}
