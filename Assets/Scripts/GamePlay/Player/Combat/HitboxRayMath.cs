using System;
using UnityEngine;

namespace GamePlay.Player.Combat
{
    /// <summary>历史形状的射线求交，不依赖当前物理世界中的 Collider。</summary>
    public static class HitboxRayMath
    {
        private const double AxisEpsilonSquared = 1e-10;

        public static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        public static bool IsFinite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
        public static bool IsFinite(Vector3 value) => IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);

        public static bool TryNormalize(Ray source, out Ray ray)
        {
            ray = default;
            if (!IsFinite(source.origin) || !IsFinite(source.direction))
                return false;
            double lengthSquared = Dot(source.direction, source.direction);
            if (lengthSquared <= 1e-12)
                return false;
            double inverseLength = 1d / Math.Sqrt(lengthSquared);
            ray = new Ray(source.origin, new Vector3((float)(source.direction.x * inverseLength),
                (float)(source.direction.y * inverseLength), (float)(source.direction.z * inverseLength)));
            return true;
        }

        public static bool RaycastSphere(Ray source, HitSphere sphere, float maxDistance, out float distance)
        {
            distance = 0f;
            if (!sphere.IsValid || !IsFinite(maxDistance) || maxDistance < 0f || !TryNormalize(source, out Ray ray))
                return false;

            return SphereIntersection(ray, sphere.Center, sphere.Radius, maxDistance, out distance);
        }

        public static bool RaycastCapsule(Ray source, HitCapsule capsule, float maxDistance, out float distance)
        {
            distance = 0f;
            if (!capsule.IsValid || !IsFinite(maxDistance) || maxDistance < 0f || !TryNormalize(source, out Ray ray))
                return false;

            Vector3 axis = capsule.PointB - capsule.PointA;
            double axisLengthSquared = Dot(axis, axis);
            if (axisLengthSquared <= AxisEpsilonSquared)
                return SphereIntersection(ray, (capsule.PointA + capsule.PointB) * 0.5f,
                    capsule.Radius, maxDistance, out distance);

            Vector3 fromA = ray.origin - capsule.PointA;
            double axisOrigin = Dot(axis, fromA);
            double closestFraction = Math.Max(0d, Math.Min(1d, axisOrigin / axisLengthSquared));
            Vector3 nearestAxisPoint = capsule.PointA + axis * (float)closestFraction;
            Vector3 fromAxis = ray.origin - nearestAxisPoint;
            if (Dot(fromAxis, fromAxis) <= (double)capsule.Radius * capsule.Radius)
                return true;

            double nearest = double.PositiveInfinity;
            if (SphereIntersection(ray, capsule.PointA, capsule.Radius, maxDistance, out float firstCap))
                nearest = firstCap;
            if (SphereIntersection(ray, capsule.PointB, capsule.Radius, maxDistance, out float secondCap))
                nearest = Math.Min(nearest, secondCap);

            // 圆柱侧面的二次方程；平行于胶囊轴的射线由两个端球处理。
            double axisDirection = Dot(axis, ray.direction);
            double directionOrigin = Dot(ray.direction, fromA);
            double a = axisLengthSquared * Dot(ray.direction, ray.direction) - axisDirection * axisDirection;
            double b = axisLengthSquared * directionOrigin - axisOrigin * axisDirection;
            double c = axisLengthSquared * Dot(fromA, fromA) - axisOrigin * axisOrigin -
                       (double)capsule.Radius * capsule.Radius * axisLengthSquared;
            double discriminant = b * b - a * c;
            if (a > axisLengthSquared * 1e-12 && discriminant >= 0d)
            {
                double root = Math.Sqrt(discriminant);
                IncludeCylinderRoot((-b - root) / a, axisOrigin, axisDirection, axisLengthSquared,
                    maxDistance, ref nearest);
                IncludeCylinderRoot((-b + root) / a, axisOrigin, axisDirection, axisLengthSquared,
                    maxDistance, ref nearest);
            }

            if (double.IsPositiveInfinity(nearest))
                return false;
            distance = (float)nearest;
            return true;
        }

        private static void IncludeCylinderRoot(double root, double axisOrigin, double axisDirection,
            double axisLengthSquared, float maxDistance, ref double nearest)
        {
            if (root < 0d || root > maxDistance || root >= nearest)
                return;
            double axisPosition = axisOrigin + root * axisDirection;
            if (axisPosition >= 0d && axisPosition <= axisLengthSquared)
                nearest = root;
        }

        private static bool SphereIntersection(Ray ray, Vector3 center, float radius, float maxDistance,
            out float distance)
        {
            distance = 0f;
            Vector3 offset = ray.origin - center;
            double c = Dot(offset, offset) - (double)radius * radius;
            if (c <= 0d)
                return true;
            double b = Dot(offset, ray.direction);
            if (b >= 0d)
                return false;
            double a = Dot(ray.direction, ray.direction);
            double discriminant = b * b - a * c;
            if (discriminant < 0d)
                return false;
            double root = (-b - Math.Sqrt(discriminant)) / a;
            if (root < 0d || root > maxDistance)
                return false;
            distance = (float)root;
            return true;
        }

        private static double Dot(Vector3 first, Vector3 second) =>
            (double)first.x * second.x + (double)first.y * second.y + (double)first.z * second.z;
    }
}
