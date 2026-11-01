using UnityEngine;

namespace GamePlay.Player.Simulation
{
    /// <summary>
    /// 一拍模拟使用的原始输入。位移和朝向由 <see cref="PlayerMotor"/> 计算。
    /// </summary>
    public struct PlayerInputFrame
    {
        public uint tick;
        public Vector2 move;
        public Vector2 look;
        public bool jump;
        public bool fire;
        public bool key1;
        public bool key2;
        public bool key3;
    }
}
