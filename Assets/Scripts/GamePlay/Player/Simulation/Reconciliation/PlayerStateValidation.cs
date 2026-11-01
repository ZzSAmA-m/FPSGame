using UnityEngine;

namespace GamePlay.Player.Simulation.Reconciliation
{
    /// <summary>拒绝会让模拟产生无效坐标的数值。</summary>
    public static class PlayerStateValidation
    {
        public static bool IsFinite(in PlayerSimState state) =>
            IsFinite(state.position) && IsFinite(state.velocity) &&
            IsFinite(state.verticalVelocity) && IsFinite(state.yaw) && IsFinite(state.pitch);

        public static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        public static bool IsFinite(Vector2 value) => IsFinite(value.x) && IsFinite(value.y);

        public static bool IsFinite(Vector3 value) =>
            IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
    }
}
