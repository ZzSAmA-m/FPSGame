using UnityEngine;

namespace GamePlay.Player.Simulation
{
    /// <summary>
    /// 一拍模拟使用的原始输入。由模拟入口交给 <see cref="PlayerMovementController"/> 执行移动。
    /// </summary>
    public struct PlayerInputFrame
    {
        public uint tick;
        // 采样输入时最近实际呈现的公共服务器 Tick；缺少时钟基准时为 NaN。
        public double viewServerTick;
        public uint viewFromTick;
        public uint viewToTick;
        public Vector2 move;
        public Vector2 look;
        public bool jump;
        public bool fire; // 本拍开火意图；是否真正发射由两端各自的枪械状态机判断。
        public bool reload; // R 按下沿，仅在消费本拍时尝试一次。
        public bool lightAttackPressed;
        public bool heavyAttackPressed;
        public bool key1;
        public bool key2;
        public bool key3;

        internal readonly PlayerInputFrame WithoutActions() => new()
        {
            tick = tick,
            viewServerTick = viewServerTick,
            viewFromTick = viewFromTick,
            viewToTick = viewToTick
        };
    }
}
