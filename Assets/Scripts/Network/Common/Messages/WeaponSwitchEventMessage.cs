using GamePlay.HoldSystem;

namespace Network.Common.Messages
{
    /// <summary>服务器确认玩家成功切换到指定装备栏位后产生的事件。</summary>
    public sealed class WeaponSwitchEventMessage : NetworkEventMessage
    {
        public uint predictionEpoch;
        public uint inputTick;
        public WeaponSlot weaponSlot;
        public bool serverInitiated;
    }
}
