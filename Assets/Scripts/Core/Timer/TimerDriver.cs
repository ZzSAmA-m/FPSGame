using System.Collections.Generic;
using UnityEngine;

namespace Core.Timer
{
    /// <summary>
    /// 所有正在运行的计时器共用一次 Update。
    /// 同一帧使用同一个时间戳，触发时刻只由间隔决定，不由脚本执行顺序决定。
    /// </summary>
    internal sealed class TimerDriver : MonoBehaviour
    {
        private static TimerDriver _driver;
        private readonly List<Timer> _running = new();

        internal static void Reset()
        {
            if (_driver != null)
            {
                _driver._running.Clear();
            }

            _driver = null;
        }

        internal static TimerDriver Ensure()
        {
            if (_driver != null)
            {
                return _driver;
            }

            GameObject driverObject = new(nameof(TimerDriver));
            DontDestroyOnLoad(driverObject);
            _driver = driverObject.AddComponent<TimerDriver>();
            return _driver;
        }

        internal void Add(Timer timer)
        {
            if (!_running.Contains(timer))
            {
                _running.Add(timer);
            }
        }

        private void Update()
        {
            double now = Time.unscaledTimeAsDouble;
            int count = _running.Count;
            for (int i = 0; i < count; i++)
            {
                _running[i].Tick(now);
            }

            for (int i = _running.Count - 1; i >= 0; i--)
            {
                if (!_running[i].IsRunning)
                {
                    _running.RemoveAt(i);
                }
            }
        }

        private void OnDestroy()
        {
            if (_driver == this)
            {
                _driver = null;
            }
        }
    }
}
