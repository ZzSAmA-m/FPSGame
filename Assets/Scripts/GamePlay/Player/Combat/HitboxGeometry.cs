using UnityEngine;

namespace GamePlay.Player.Combat
{
    public enum HitRegion : byte
    {
        None = 0,
        Head = 1,
        Body = 2
    }

    /// <summary>世界空间球体，快照读取不会修改 Collider。</summary>
    public readonly struct HitSphere
    {
        public Vector3 Center { get; }
        public float Radius { get; }
        public bool IsValid => HitboxRayMath.IsFinite(Center) && HitboxRayMath.IsFinite(Radius) && Radius > 0f;

        public HitSphere(Vector3 center, float radius)
        {
            Center = center;
            Radius = radius;
        }

        public static HitSphere Interpolate(HitSphere from, HitSphere to, float fraction) => new(
            Vector3.LerpUnclamped(from.Center, to.Center, fraction),
            Mathf.LerpUnclamped(from.Radius, to.Radius, fraction));
    }

    /// <summary>世界空间胶囊，端点为沿固定轴顺序排列的两端球心。</summary>
    public readonly struct HitCapsule
    {
        public Vector3 PointA { get; }
        public Vector3 PointB { get; }
        public float Radius { get; }
        public bool IsValid => HitboxRayMath.IsFinite(PointA) && HitboxRayMath.IsFinite(PointB) &&
                               HitboxRayMath.IsFinite(Radius) && Radius > 0f;

        public HitCapsule(Vector3 pointA, Vector3 pointB, float radius)
        {
            PointA = pointA;
            PointB = pointB;
            Radius = radius;
        }

        public static HitCapsule Interpolate(HitCapsule from, HitCapsule to, float fraction) => new(
            Vector3.LerpUnclamped(from.PointA, to.PointA, fraction),
            Vector3.LerpUnclamped(from.PointB, to.PointB, fraction),
            Mathf.LerpUnclamped(from.Radius, to.Radius, fraction));
    }

    /// <summary>一名玩家在一次姿态求值结束后的独立命中几何。</summary>
    public readonly struct PlayerHitboxSnapshot
    {
        public HitSphere Head { get; }
        public HitCapsule Body { get; }
        public bool IsValid => Head.IsValid && Body.IsValid;

        public PlayerHitboxSnapshot(HitSphere head, HitCapsule body)
        {
            Head = head;
            Body = body;
        }

        public static PlayerHitboxSnapshot Interpolate(PlayerHitboxSnapshot from, PlayerHitboxSnapshot to,
            float fraction) => new(
            HitSphere.Interpolate(from.Head, to.Head, fraction),
            HitCapsule.Interpolate(from.Body, to.Body, fraction));
    }
}
