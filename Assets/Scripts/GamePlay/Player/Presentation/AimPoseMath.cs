using System;
using UnityEngine;

namespace GamePlay.Player.Presentation
{
    /// <summary>一条手臂的世界旋转解；只返回结果，不写入骨骼。</summary>
    public readonly struct ArmSolution
    {
        public readonly Quaternion UpperRotation;
        public readonly Quaternion LowerRotation;
        public readonly Quaternion HandRotation;

        public ArmSolution(Quaternion upperRotation, Quaternion lowerRotation, Quaternion handRotation)
        {
            UpperRotation = upperRotation;
            LowerRotation = lowerRotation;
            HandRotation = handRotation;
        }
    }

    /// <summary>TP 瞄准的几何求解，不依赖 Animator，也不修改 Transform 或原始动画数据。</summary>
    public static class AimPoseMath
    {
        private const float ReachMargin = 0.00001f;
        private const float ReachTolerance = 0.000002f;
        private const float PositionTolerance = 0.0005f;

        /// <summary>按本帧动画骨长求两段 IK。poleHint 是世界弯肘方向，不是目标点。</summary>
        public static bool TrySolveArm(Vector3 shoulder, Vector3 elbow, Vector3 hand,
            Quaternion upperRotation, Quaternion lowerRotation, Quaternion handRotation,
            Vector3 targetPosition, Quaternion targetRotation, Vector3 poleHint, out ArmSolution solution)
        {
            solution = default;
            if (!Finite(shoulder) || !Finite(elbow) || !Finite(hand) || !Finite(targetPosition) || !Finite(poleHint) ||
                !TryNormalize(upperRotation, out upperRotation) || !TryNormalize(lowerRotation, out lowerRotation) ||
                !TryNormalize(handRotation, out _) || !TryNormalize(targetRotation, out targetRotation))
                return false;

            Vector3 upper = elbow - shoulder;
            Vector3 lower = hand - elbow;
            Vector3 towardTarget = targetPosition - shoulder;
            if (!TryLength(upper, out float upperLength) || !TryLength(lower, out float lowerLength) ||
                !TryLength(towardTarget, out float targetDistance) ||
                !TryReach(upperLength, lowerLength, out float inner, out float outer) ||
                targetDistance < inner - ReachTolerance || targetDistance > outer + ReachTolerance || targetDistance <= 0f)
                return false;

            Vector3 direction = towardTarget / targetDistance;
            Vector3 bend = poleHint - direction * Vector3.Dot(poleHint, direction);
            if (!TryNormalize(bend, out bend))
            {
                // 当前动画仍有有效肘平面时可直接使用；完全共线则由调用者选择缓存或备用 pole。
                bend = upper - direction * Vector3.Dot(upper, direction);
                if (!TryNormalize(bend, out bend)) return false;
            }

            double distance = Math.Max(inner, Math.Min(outer, targetDistance));
            double along = ((double)upperLength * upperLength - (double)lowerLength * lowerLength + distance * distance) /
                (2d * distance);
            double heightSquared = (double)upperLength * upperLength - along * along;
            if (!Finite(along) || !Finite(heightSquared) || heightSquared < -1e-10d) return false;
            Vector3 desiredElbow = shoulder + direction * (float)along + bend * (float)Math.Sqrt(Math.Max(0d, heightSquared));
            Vector3 solvedTarget = shoulder + direction * (float)distance;
            if (!Finite(desiredElbow) || !Finite(solvedTarget) ||
                !TryAlign(upper, desiredElbow - shoulder, bend, out Quaternion upperDelta) ||
                !TryAlign(upperDelta * lower, solvedTarget - desiredElbow, bend, out Quaternion lowerDelta) ||
                !TryNormalize(upperDelta * upperRotation, out Quaternion solvedUpper) ||
                !TryNormalize(lowerDelta * upperDelta * lowerRotation, out Quaternion solvedLower))
                return false;

            // 以对齐方向的最小旋转保留本帧动画的骨轴扭转；手掌朝向由共同目标直接指定。
            Vector3 reconstructed = shoulder + upperDelta * upper + lowerDelta * (upperDelta * lower);
            if (!Finite(reconstructed) || SquaredLength(reconstructed - targetPosition) > PositionTolerance * PositionTolerance)
                return false;
            solution = new ArmSolution(solvedUpper, solvedLower, targetRotation);
            return true;
        }

        /// <summary>寻找同时适合双臂的最小公共平移，枪与双手的相对姿态不变，不拉伸骨长。</summary>
        public static bool TryFindSharedTranslation(Vector3 leftShoulder, Vector3 leftTarget,
            float leftUpperLength, float leftLowerLength, Vector3 rightShoulder, Vector3 rightTarget,
            float rightUpperLength, float rightLowerLength, out Vector3 translation)
        {
            translation = default;
            if (!Finite(leftShoulder) || !Finite(leftTarget) || !Finite(rightShoulder) || !Finite(rightTarget) ||
                !TryReach(leftUpperLength, leftLowerLength, out float leftInner, out float leftOuter) ||
                !TryReach(rightUpperLength, rightLowerLength, out float rightInner, out float rightOuter))
                return false;

            // 在平移量 t 的空间求解：|t - (shoulder - target)| 位于该臂的可达壳内。
            var left = new ReachRegion(leftShoulder - leftTarget, leftInner, leftOuter);
            var right = new ReachRegion(rightShoulder - rightTarget, rightInner, rightOuter);
            if (!Finite(left.Center) || !Finite(right.Center)) return false;
            if (left.Contains(Vector3.zero) && right.Contains(Vector3.zero)) return true;

            bool found = false;
            double bestSquared = double.PositiveInfinity;
            ConsiderSphere(left.Center, left.Outer, left, right, ref found, ref bestSquared, ref translation);
            ConsiderSphere(right.Center, right.Outer, left, right, ref found, ref bestSquared, ref translation);
            ConsiderSphere(left.Center, left.Inner, left, right, ref found, ref bestSquared, ref translation);
            ConsiderSphere(right.Center, right.Inner, left, right, ref found, ref bestSquared, ref translation);

            // 最近可行点若不在单一球面的最近点上，就在两个边界球面的交圆上。
            // 同时枚举内、外边界，避免满足外球后却把某只手推到最小折叠距离以内。
            ConsiderIntersection(left.Center, left.Outer, right.Center, right.Outer, left, right,
                ref found, ref bestSquared, ref translation);
            ConsiderIntersection(left.Center, left.Outer, right.Center, right.Inner, left, right,
                ref found, ref bestSquared, ref translation);
            ConsiderIntersection(left.Center, left.Inner, right.Center, right.Outer, left, right,
                ref found, ref bestSquared, ref translation);
            ConsiderIntersection(left.Center, left.Inner, right.Center, right.Inner, left, right,
                ref found, ref bestSquared, ref translation);
            return found;
        }

        private readonly struct ReachRegion
        {
            public readonly Vector3 Center;
            public readonly float Inner;
            public readonly float Outer;

            public ReachRegion(Vector3 center, float inner, float outer)
            {
                Center = center;
                Inner = inner;
                Outer = outer;
            }

            public bool Contains(Vector3 point)
            {
                double squared = SquaredLength(point - Center);
                double min = Math.Max(0d, Inner - ReachTolerance);
                double max = (double)Outer + ReachTolerance;
                return Finite(squared) && squared >= min * min && squared <= max * max;
            }
        }

        private static void ConsiderSphere(Vector3 center, float radius, ReachRegion left, ReachRegion right,
            ref bool found, ref double bestSquared, ref Vector3 best)
        {
            Vector3 direction = TryNormalize(center, out Vector3 normalized) ? normalized : Vector3.right;
            Consider(center - direction * radius, left, right, ref found, ref bestSquared, ref best);
        }

        private static void ConsiderIntersection(Vector3 firstCenter, float firstRadius,
            Vector3 secondCenter, float secondRadius, ReachRegion left, ReachRegion right,
            ref bool found, ref double bestSquared, ref Vector3 best)
        {
            Vector3 between = secondCenter - firstCenter;
            double separation = Math.Sqrt(SquaredLength(between));
            if (!Finite(separation) || separation < 1e-10d) return; // 同心边界由单球候选覆盖。
            if (separation > (double)firstRadius + secondRadius + ReachTolerance ||
                separation < Math.Abs((double)firstRadius - secondRadius) - ReachTolerance)
                return;

            double along = ((double)firstRadius * firstRadius - (double)secondRadius * secondRadius + separation * separation) /
                (2d * separation);
            double radiusSquared = (double)firstRadius * firstRadius - along * along;
            if (!Finite(along) || !Finite(radiusSquared) || radiusSquared < -1e-10d) return;
            Vector3 axis = between / (float)separation;
            Vector3 center = firstCenter + axis * (float)along;
            Vector3 radial = -center + axis * Vector3.Dot(center, axis);
            if (!TryNormalize(radial, out radial)) radial = Perpendicular(axis);
            Vector3 point = center + radial * (float)Math.Sqrt(Math.Max(0d, radiusSquared));
            Consider(point, left, right, ref found, ref bestSquared, ref best);
        }

        private static void Consider(Vector3 point, ReachRegion left, ReachRegion right,
            ref bool found, ref double bestSquared, ref Vector3 best)
        {
            if (!Finite(point) || !left.Contains(point) || !right.Contains(point)) return;
            double squared = SquaredLength(point);
            if (found && squared >= bestSquared) return;
            found = true;
            bestSquared = squared;
            best = point;
        }

        private static bool TryReach(float upper, float lower, out float inner, out float outer)
        {
            inner = outer = 0f;
            if (!Finite(upper) || !Finite(lower) || upper <= ReachMargin || lower <= ReachMargin) return false;
            inner = Mathf.Abs(upper - lower) + ReachMargin;
            outer = upper + lower - ReachMargin;
            return Finite(inner) && Finite(outer) && inner <= outer;
        }

        private static bool TryAlign(Vector3 from, Vector3 to, Vector3 pole, out Quaternion rotation)
        {
            rotation = Quaternion.identity;
            if (!TryNormalize(from, out from) || !TryNormalize(to, out to)) return false;
            float dot = Mathf.Clamp(Vector3.Dot(from, to), -1f, 1f);
            if (1f + dot > 0.0000001f)
            {
                Vector3 cross = Vector3.Cross(from, to);
                return TryNormalize(new Quaternion(cross.x, cross.y, cross.z, 1f + dot), out rotation);
            }
            Vector3 axis = Vector3.Cross(from, pole);
            if (!TryNormalize(axis, out axis)) axis = Perpendicular(from);
            rotation = new Quaternion(axis.x, axis.y, axis.z, 0f);
            return true;
        }

        private static Vector3 Perpendicular(Vector3 direction)
        {
            Vector3 basis = Mathf.Abs(direction.x) < Mathf.Abs(direction.y)
                ? (Mathf.Abs(direction.x) < Mathf.Abs(direction.z) ? Vector3.right : Vector3.forward)
                : (Mathf.Abs(direction.y) < Mathf.Abs(direction.z) ? Vector3.up : Vector3.forward);
            return Vector3.Cross(direction, basis).normalized;
        }

        private static bool TryNormalize(Vector3 value, out Vector3 normalized)
        {
            normalized = default;
            double squared = SquaredLength(value);
            if (!Finite(squared) || squared <= 1e-16d) return false;
            normalized = value * (float)(1d / Math.Sqrt(squared));
            return Finite(normalized);
        }

        private static bool TryNormalize(Quaternion value, out Quaternion normalized)
        {
            normalized = default;
            double squared = (double)value.x * value.x + (double)value.y * value.y +
                (double)value.z * value.z + (double)value.w * value.w;
            if (!Finite(squared) || squared <= 1e-16d) return false;
            float inverse = (float)(1d / Math.Sqrt(squared));
            normalized = new Quaternion(value.x * inverse, value.y * inverse, value.z * inverse, value.w * inverse);
            return Finite(normalized.x) && Finite(normalized.y) && Finite(normalized.z) && Finite(normalized.w);
        }

        private static bool TryLength(Vector3 value, out float length)
        {
            double result = Math.Sqrt(SquaredLength(value));
            length = (float)result;
            return Finite(result) && Finite(length) && length > 0f;
        }

        private static double SquaredLength(Vector3 value) =>
            (double)value.x * value.x + (double)value.y * value.y + (double)value.z * value.z;

        private static bool Finite(Vector3 value) => Finite(value.x) && Finite(value.y) && Finite(value.z);
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
    }
}
