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
        public bool fire; // 本拍开火意图；是否真正发射由两端各自的枪械状态机判断。
        public bool key1;
        public bool key2;
        public bool key3;
    }
}
