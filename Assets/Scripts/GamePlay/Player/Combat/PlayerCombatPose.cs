using GamePlay.Player.Simulation.Reconciliation;
using UnityEngine;

namespace GamePlay.Player.Combat
{
    /// <summary>命中骨骼相对玩家根节点的权威姿态，随同一显示时间的移动快照插值。</summary>
    public struct PlayerCombatPose
    {
        public bool valid;
        public Vector3 headPosition;
        public Vector3 pelvisPosition;
        public Quaternion headRotation;
        public Quaternion pelvisRotation;

        public bool IsValid => valid && PlayerStateValidation.IsFinite(headPosition) &&
                               PlayerStateValidation.IsFinite(pelvisPosition) &&
                               ValidRotation(headRotation) && ValidRotation(pelvisRotation);

        public static PlayerCombatPose Lerp(in PlayerCombatPose from, in PlayerCombatPose to, float fraction)
        {
            if (!from.IsValid || !to.IsValid || !PlayerStateValidation.IsFinite(fraction))
                return default;
            float t = Mathf.Clamp01(fraction);
            return new PlayerCombatPose
            {
                valid = true,
                headPosition = Vector3.Lerp(from.headPosition, to.headPosition, t),
                pelvisPosition = Vector3.Lerp(from.pelvisPosition, to.pelvisPosition, t),
                headRotation = Quaternion.Slerp(from.headRotation, to.headRotation, t),
                pelvisRotation = Quaternion.Slerp(from.pelvisRotation, to.pelvisRotation, t)
            };
        }

        private static bool ValidRotation(Quaternion rotation)
        {
            float length = Quaternion.Dot(rotation, rotation);
            return PlayerStateValidation.IsFinite(rotation.x) && PlayerStateValidation.IsFinite(rotation.y) &&
                   PlayerStateValidation.IsFinite(rotation.z) && PlayerStateValidation.IsFinite(rotation.w) &&
                   length > 0.999f && length < 1.001f;
        }
    }
}
