using GamePlay.HoldSystem;

namespace Network.Common.Messages
{
    /// <summary>服务器确认枪械动作提前取消的事件。</summary>
    public sealed class WeaponActionCanceledEventMessage : NetworkEventMessage
    {
        public uint predictionEpoch;
        public uint inputTick;
        public WeaponSlot weaponSlot;
        public GunState canceledState;
        public bool serverInitiated;
    }
}
