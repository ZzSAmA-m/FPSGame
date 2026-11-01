using UnityEngine;

namespace GamePlay.Player.Simulation
{
    /// <summary>
    /// 一拍模拟结束后的完整运动状态。按值复制即可留作以后的回滚快照。
    /// </summary>
    public struct PlayerSimState
    {
        public Vector3 position;
        public Vector3 velocity;
        public float verticalVelocity;
        public float yaw;
        public float pitch;
        public bool grounded;
    }
}
