using GamePlay.HoldSystem;

namespace Network.Common.Messages
{
    /// <summary>服务器确认玩家开始轻击或重击。</summary>
    public sealed class KnifeAttackEventMessage : NetworkEventMessage
    {
        public uint predictionEpoch;
        public uint inputTick;
        public WeaponSlot weaponSlot;
        public KnifeAttackKind attackKind;
    }
}
