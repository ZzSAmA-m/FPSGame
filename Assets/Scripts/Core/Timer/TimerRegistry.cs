using System;
using System.Collections.Generic;
using UnityEngine;

namespace Core.Timer
{
    /// <summary>
    /// 按名字保存计时器，便于在不同对象之间取用同一个计时器。
    /// </summary>
    public static class TimerRegistry
    {
        private static readonly Dictionary<string, Timer> Timers = new();

        /// <summary>
        /// 进入 Play 模式时清空上一轮留下的名字和正在运行的计时器。
        /// 关闭 Domain Reload 时静态数据不会自动重置，所以要在这里清。
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ClearOnEnterPlayMode()
        {
            foreach (Timer timer in Timers.Values)
            {
                timer.StopInvoke();
            }

            Timers.Clear();
            TimerDriver.Reset();
        }

        /// <summary>创建计时器并按名字登记。</summary>
        public static Timer Register(string name)
        {
            Timer timer = new();
            Register(name, timer);
            return timer;
        }

        /// <summary>按名字登记一个已经创建的计时器。</summary>
        public static void Register(string name, Timer timer)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("计时器名字不能为空。", nameof(name));
            }

            if (timer == null)
            {
                throw new ArgumentNullException(nameof(timer));
            }

            if (Timers.ContainsKey(name))
            {
                throw new InvalidOperationException($"计时器已经注册：{name}");
            }

            Timers.Add(name, timer);
        }

        /// <summary>按名字取得计时器。</summary>
        public static Timer Get(string name)
        {
            if (TryGet(name, out Timer timer))
            {
                return timer;
            }

            throw new InvalidOperationException($"找不到计时器：{name}");
        }

        /// <summary>按名字查找计时器。</summary>
        public static bool TryGet(string name, out Timer timer)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                timer = null;
                return false;
            }

            return Timers.TryGetValue(name, out timer);
        }

        /// <summary>停止并移除这个名字对应的计时器。</summary>
        public static bool Unregister(string name)
        {
            if (!TryGet(name, out Timer timer))
            {
                return false;
            }

            timer.StopInvoke();
            return Timers.Remove(name);
        }
    }
}
