using GamePlay.HoldSystem;

namespace Network.Common.Messages
{
    /// <summary>服务器确认武器已开始装备准备。</summary>
    public sealed class PrepareEventMessage : NetworkEventMessage
    {
        public uint predictionEpoch;
        public uint inputTick;
        public WeaponSlot weaponSlot;
        public bool serverInitiated;
    }
}
