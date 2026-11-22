using Core;

namespace GamePlay.Events
{
    public static class PlayerInputEventIds
    {
        /// <summary>本机玩家是否可由设备输入控制。</summary>
        public static readonly int AvailabilityChanged = RuntimeID.ToRuntimeID("PlayerInputEventIds.AvailabilityChanged");
    }
}
