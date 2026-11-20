using GamePlay.HoldSystem;

namespace Network.Common.Messages
{
    /// <summary>服务器确认枪械已开始换弹的事件。</summary>
    public sealed class ReloadEventMessage : NetworkEventMessage
    {
        public uint predictionEpoch;
        public uint inputTick;
        public WeaponSlot weaponSlot;
        public bool serverInitiated;
    }
}
