using System;
using GamePlay.Player.Simulation;
using GamePlay.Player.Simulation.Reconciliation;

namespace GamePlay.Player.Presentation
{
    /// <summary>将同步服务器时间映射为全体远端角色共同使用的显示 Tick。</summary>
    public sealed class ClientWorldTimeline
    {
        private uint _anchorTick;
        private double _anchorServerTime;

        public bool HasAnchor { get; private set; }
        public uint LatestAnchorTick => _anchorTick;
        public double LastPresentedServerTick { get; private set; } = double.NaN;
        public uint LastPresentedFromTick { get; private set; }
        public uint LastPresentedToTick { get; private set; }

        public bool ObserveAnchor(uint serverTick, double serverTime)
        {
            if (!IsFinite(serverTime) || serverTime < 0d ||
                (HasAnchor && !TickMath.IsNewer(serverTick, _anchorTick)))
                return false;

            _anchorTick = serverTick;
            _anchorServerTime = serverTime;
            HasAnchor = true;
            return true;
        }

        public bool TryGetRenderTick(double estimatedServerTime, out double tick)
        {
            tick = double.NaN;
            if (!HasAnchor || !IsFinite(estimatedServerTime))
                return false;

            tick = Math.Max(0d, _anchorTick +
                (estimatedServerTime - _anchorServerTime - SimTime.RemoteDelaySeconds) / SimTime.Dt);
            if (IsFinite(LastPresentedServerTick))
                tick = Math.Max(tick, LastPresentedServerTick);
            return IsFinite(tick);
        }

        public void MarkPresented(double tick, uint fromTick, uint toTick)
        {
            if (!HasAnchor || !IsFinite(tick) || fromTick > toTick || tick < fromTick || tick > toTick)
                return;
            if (double.IsNaN(LastPresentedServerTick) || tick >= LastPresentedServerTick)
            {
                LastPresentedServerTick = tick;
                LastPresentedFromTick = fromTick;
                LastPresentedToTick = toTick;
            }
        }

        public void Reset()
        {
            HasAnchor = false;
            _anchorTick = 0;
            _anchorServerTime = 0d;
            LastPresentedServerTick = double.NaN;
            LastPresentedFromTick = 0;
            LastPresentedToTick = 0;
        }

        private static bool IsFinite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
    }
}
