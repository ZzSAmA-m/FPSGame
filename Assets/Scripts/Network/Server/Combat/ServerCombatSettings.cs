using System;
using UnityEngine;

namespace Network.Server.Combat
{
    [Serializable]
    public sealed class ServerCombatSettings
    {
        [Min(2)] public int HistoryCapacity = 64;
        [Min(0.01f)] public float MaxRange = 200f;
        [Min(0f)] public double MaxRewindSeconds = 0.2d;
        [Min(0f)] public double TimeToleranceSeconds = 0.05d;
        [Min(0f)] public float HeadTieTolerance = 0.001f;
        public LayerMask StaticCollisionMask = Physics.DefaultRaycastLayers;
    }
}
