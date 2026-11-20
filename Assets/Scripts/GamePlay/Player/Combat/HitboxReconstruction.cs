using UnityEngine;

namespace GamePlay.Player.Combat
{
    /// <summary>以客户端相同的根节点和骨骼插值重建命中几何。</summary>
    public readonly struct HitboxReconstruction
    {
        public Vector3 RootPosition { get; }
        public float RootYaw { get; }
        public Vector3 RootScale { get; }
        public PlayerCombatPose Pose { get; }
        public Vector3 HeadOffset { get; }
        public Vector3 BodyOffsetA { get; }
        public Vector3 BodyOffsetB { get; }
        public bool IsValid => Pose.IsValid;

        public HitboxReconstruction(Transform root, PlayerCombatPoseController pose, PlayerHitboxSnapshot geometry)
        {
            RootPosition = root.position;
            RootYaw = root.eulerAngles.y;
            RootScale = root.lossyScale;
            Pose = pose != null ? pose.CapturePose() : default;
            HeadOffset = pose != null && pose.HeadBone != null
                ? Quaternion.Inverse(pose.HeadBone.rotation) * (geometry.Head.Center - pose.HeadBone.position) : default;
            BodyOffsetA = pose != null && pose.PelvisBone != null
                ? Quaternion.Inverse(pose.PelvisBone.rotation) * (geometry.Body.PointA - pose.PelvisBone.position) : default;
            BodyOffsetB = pose != null && pose.PelvisBone != null
                ? Quaternion.Inverse(pose.PelvisBone.rotation) * (geometry.Body.PointB - pose.PelvisBone.position) : default;
        }

        public static PlayerHitboxSnapshot Interpolate(in HitboxReconstruction a, in HitboxReconstruction b,
            in PlayerHitboxSnapshot geometryA, in PlayerHitboxSnapshot geometryB, float t)
        {
            PlayerCombatPose pose = PlayerCombatPose.Lerp(a.Pose, b.Pose, t);
            Vector3 position = Vector3.Lerp(a.RootPosition, b.RootPosition, t);
            Quaternion rotation = Quaternion.Euler(0f, Mathf.LerpAngle(a.RootYaw, b.RootYaw, t), 0f);
            Vector3 scale = Vector3.Lerp(a.RootScale, b.RootScale, t);
            Vector3 head = position + rotation * Vector3.Scale(pose.headPosition, scale);
            Vector3 pelvis = position + rotation * Vector3.Scale(pose.pelvisPosition, scale);
            Quaternion headRotation = rotation * pose.headRotation;
            Quaternion pelvisRotation = rotation * pose.pelvisRotation;
            return new PlayerHitboxSnapshot(
                new HitSphere(head + headRotation * Vector3.Lerp(a.HeadOffset, b.HeadOffset, t),
                    Mathf.Lerp(geometryA.Head.Radius, geometryB.Head.Radius, t)),
                new HitCapsule(pelvis + pelvisRotation * Vector3.Lerp(a.BodyOffsetA, b.BodyOffsetA, t),
                    pelvis + pelvisRotation * Vector3.Lerp(a.BodyOffsetB, b.BodyOffsetB, t),
                    Mathf.Lerp(geometryA.Body.Radius, geometryB.Body.Radius, t)));
        }
    }
}
