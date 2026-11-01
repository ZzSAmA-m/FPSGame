using System;
using UnityEngine;

namespace GamePlay.Player.Simulation.Reconciliation
{
    /// <summary>历史容量、重放预算和允许的运动误差。</summary>
    [Serializable]
    public sealed class ReconciliationSettings
    {
        [Min(2)] public int historyCapacity = 256;
        [Min(1)] public int maxReplayTicks = 64;
        [Min(0f)] public float positionTolerance = 0.02f;
        [Min(0f)] public float velocityTolerance = 0.05f;
        [Min(0f)] public float angleTolerance = 0.1f;

        public void Validate()
        {
            historyCapacity = Mathf.Clamp(historyCapacity, 2, 65536);
            maxReplayTicks = Mathf.Clamp(maxReplayTicks, 1, historyCapacity - 1);
            positionTolerance = SafeTolerance(positionTolerance, 0.02f);
            velocityTolerance = SafeTolerance(velocityTolerance, 0.05f);
            angleTolerance = Mathf.Min(SafeTolerance(angleTolerance, 0.1f), 180f);
        }

        static float SafeTolerance(float value, float fallback) =>
            PlayerStateValidation.IsFinite(value) ? Mathf.Max(0f, value) : fallback;
    }
}
