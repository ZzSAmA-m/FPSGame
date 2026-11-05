using System;

namespace GamePlay.Player.Simulation
{
    /// <summary>按实际经过时间积累模拟步数，超出本帧预算的部分留到后续帧。</summary>
    public sealed class SimulationClock
    {
        const double StepEpsilon = 1e-9d;
        double _lastSampleTime;
        double _accumulator;
        bool _hasSample;

        /// <summary>从新的时间起点开始，丢掉上一轮尚未模拟的时间。</summary>
        public void Reset(double now)
        {
            _accumulator = 0d;
            _hasSample = IsFinite(now);
            _lastSampleTime = _hasSample ? now : 0d;
        }

        public void Advance(double now)
        {
            if (!IsFinite(now))
                return;
            if (!_hasSample || now < _lastSampleTime)
            {
                Reset(now);
                return;
            }

            _accumulator += now - _lastSampleTime;
            _lastSampleTime = now;
        }

        public bool TryStep()
        {
            if (_accumulator + StepEpsilon < SimTime.Dt)
                return false;

            _accumulator = Math.Max(0d, _accumulator - SimTime.Dt);
            return true;
        }

        /// <summary>包括本帧还没累计的时间，供网络回调安排新玩家的起步时刻。</summary>
        public uint PendingTicks(double now)
        {
            if (!_hasSample || (IsFinite(now) && now < _lastSampleTime))
                return 0;

            double elapsed = IsFinite(now) ? now - _lastSampleTime : 0d;
            double ticks = Math.Floor((_accumulator + elapsed + StepEpsilon) / SimTime.Dt);
            return ticks >= uint.MaxValue ? uint.MaxValue : (uint)ticks;
        }

        static bool IsFinite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
    }
}
