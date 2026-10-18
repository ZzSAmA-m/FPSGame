using UnityEngine;

namespace Network.Common.Messages
{
    public struct PlayerPose
    {
        public int netId;
        public Vector3 position;
        public Vector3 facingDirection;
    }
}
