using System;
using GamePlay.Player.Simulation;
using GamePlay.Player.Simulation.Reconciliation;

namespace GamePlay.Player.Presentation
{
    /// <summary>按玩家快照的到达节奏维护全体远端角色共用的显示 Tick。</summary>
    public sealed class ClientWorldTimeline
    {
        private const double MaxClockCorrectionRate = 0.1d;
        private uint _anchorTick;
        private double _anchorServerTime;
        private double _anchorReceiveTime;
        private double _clockOffsetAtAnchor;
        private double _clockResetTime = double.NaN;

        public bool HasAnchor { get; private set; }
        public uint LatestAnchorTick => _anchorTick;
        public double LastPresentedServerTick { get; private set; } = double.NaN;
        public uint LastPresentedFromTick { get; private set; }
        public uint LastPresentedToTick { get; private set; }

        public bool ObserveAnchor(uint serverTick, double serverTime, double localReceiveTime)
        {
            if (!IsFinite(serverTime) || serverTime < 0d || !IsFinite(localReceiveTime) || localReceiveTime < 0d ||
                (HasAnchor && (!TickMath.IsNewer(serverTick, _anchorTick) || localReceiveTime < _anchorReceiveTime)))
                return false;

            double offset = HasAnchor ? EstimateServerTime(localReceiveTime) - serverTime : 0d;
            // 首批快照或时钟偏差已超过两个缓冲窗口时，按这批最新快照重新蓄满缓冲。
            if (!HasAnchor || localReceiveTime == _clockResetTime || Math.Abs(offset) > 2d * SimTime.RemoteDelaySeconds)
            {
                _clockResetTime = localReceiveTime;
                offset = 0d;
            }
            _clockOffsetAtAnchor = offset;
            _anchorTick = serverTick;
            _anchorServerTime = serverTime;
            _anchorReceiveTime = localReceiveTime;
            HasAnchor = true;
            return true;
        }

        private double EstimateServerTime(double localTime)
        {
            double elapsed = Math.Max(0d, localTime - _anchorReceiveTime);
            double maxCorrection = elapsed * MaxClockCorrectionRate;
            double correction = Math.Clamp(_clockOffsetAtAnchor, -maxCorrection, maxCorrection);
            return _anchorServerTime + elapsed + (_clockOffsetAtAnchor - correction);
        }

        public bool TryGetRenderTick(double localTime, out double tick)
        {
            tick = double.NaN;
            if (!HasAnchor || !IsFinite(localTime))
                return false;

            tick = Math.Max(0d, _anchorTick +
                (EstimateServerTime(localTime) - _anchorServerTime - SimTime.RemoteDelaySeconds) / SimTime.Dt);
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
            _anchorReceiveTime = 0d;
            _clockOffsetAtAnchor = 0d;
            _clockResetTime = double.NaN;
            LastPresentedServerTick = double.NaN;
            LastPresentedFromTick = 0;
            LastPresentedToTick = 0;
        }

        private static bool IsFinite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
    }
}
