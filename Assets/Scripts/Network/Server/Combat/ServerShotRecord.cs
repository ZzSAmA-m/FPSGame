using GamePlay.Player;
using GamePlay.Player.Combat;
using UnityEngine;

namespace Network.Server.Combat
{
    /// <summary>保存一发权威射击的时间、几何查询和实际伤害。</summary>
    public sealed class ServerShotRecord
    {
        public ulong ShotId { get; internal set; }
        public uint ShooterId { get; internal set; }
        public uint InputTick { get; internal set; }
        public uint ServerTick { get; internal set; }
        public double RequestedTick { get; internal set; }
        public double ReceiptTick { get; internal set; }
        public double RoundTripSeconds { get; internal set; }
        public ValidatedShotTime Time { get; internal set; }
        public ServerShotResult Result { get; internal set; }
        public PlayerStatsChange Damage { get; internal set; }
        public bool Applied { get; internal set; }
        public bool Killed => Applied && Damage.LifeStateChanged && Damage.After.Health == 0;
        public Ray Ray { get; internal set; }
        public PlayerHitboxSnapshot TargetGeometry { get; internal set; }
    }
}
