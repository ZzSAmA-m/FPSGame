using System;
using UnityEngine;

namespace Core.Timer
{
    /// <summary>
    /// 按固定间隔重复调用一个方法。
    /// 时刻用不受 timeScale 影响的双精度时间，掉帧时在本帧补调用，使长期触发次数与间隔一致。
    /// </summary>
    public sealed class Timer
    {
        private double _interval;
        private double _nextFireTime;
        private int _maxCatchUp = 8;

        /// <summary>两次触发之间的秒数。60 次/秒应写成 <c>1d / 60d</c>，<c>1 / 60</c> 的结果是 0。</summary>
        public double Time
        {
            get => _interval;
            set => _interval = value;
        }

        /// <summary>每次间隔到达时调用的方法。</summary>
        public Action EventMethod { get; set; }

        /// <summary>单帧最多补多少次。限制卡顿之后在一帧内连续调用的次数。</summary>
        public int MaxCatchUp
        {
            get => _maxCatchUp;
            set => _maxCatchUp = value < 1 ? 1 : value;
        }

        /// <summary>是否正在按间隔触发。</summary>
        public bool IsRunning { get; private set; }

        /// <summary>从下一次间隔开始重复触发。已经在运行时再次调用不会重设计时。</summary>
        public void StartInvoke()
        {
            if (_interval <= 0d)
            {
                throw new ArgumentOutOfRangeException(nameof(Time), "触发间隔必须大于 0。");
            }

            if (IsRunning)
            {
                return;
            }

            IsRunning = true;
            _nextFireTime = UnityEngine.Time.unscaledTimeAsDouble + _interval;
            TimerDriver.Ensure().Add(this);
        }

        /// <summary>停止后续触发。本次已经开始的调用不会被打断。</summary>
        public void StopInvoke()
        {
            IsRunning = false;
        }

        internal void Tick(double now)
        {
            if (!IsRunning)
            {
                return;
            }

            int fired = 0;
            while (IsRunning && fired < _maxCatchUp && now >= _nextFireTime)
            {
                _nextFireTime += _interval;
                fired++;
                try
                {
                    EventMethod?.Invoke();
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                }
            }

            // 落后超过补调用上限时，对齐到下一个未到期的节拍，避免之后每帧都在补旧账。
            if (IsRunning && _interval > 0d && now >= _nextFireTime)
            {
                double missed = Math.Floor((now - _nextFireTime) / _interval) + 1d;
                _nextFireTime += missed * _interval;
            }
        }
    }
}
