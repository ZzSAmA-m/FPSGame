using System;
using UnityEngine;

namespace Core.ZLog
{
    /// <summary>
    /// 项目日志入口。Channel 和 Frequency 都有默认值，旧调用可以继续只传消息。
    /// 编辑器里关掉的分类或频率不会调用 Debug.Log；玩家构建默认全部输出。
    /// </summary>
    public static class ZLog
    {
        static readonly bool[] ChannelVisible;
        static readonly bool[] FrequencyVisible;

        public static bool Enable { get; set; } = true;

        static ZLog()
        {
            ChannelVisible = CreateVisibleMask(typeof(LogChannel));
            FrequencyVisible = CreateVisibleMask(typeof(LogFrequency));
        }

        public static void Log(
            string message,
            LogChannel channel = LogChannel.Default,
            LogFrequency frequency = LogFrequency.Default)
        {
            if (!Pass(channel, frequency)) return;
            Debug.Log(Format(message, channel, frequency));
        }

        public static void LogWarning(
            string message,
            LogChannel channel = LogChannel.Default,
            LogFrequency frequency = LogFrequency.Default)
        {
            if (!Pass(channel, frequency)) return;
            Debug.LogWarning(Format(message, channel, frequency));
        }

        public static void LogError(
            string message,
            LogChannel channel = LogChannel.Default,
            LogFrequency frequency = LogFrequency.Default)
        {
            if (!Pass(channel, frequency)) return;
            Debug.LogError(Format(message, channel, frequency));
        }

        public static void Log<T>(
            string message,
            LogChannel channel = LogChannel.Default,
            LogFrequency frequency = LogFrequency.Default)
        {
            if (!Pass(channel, frequency)) return;
            Debug.Log(Format<T>(message, channel, frequency));
        }

        public static void LogWarning<T>(
            string message,
            LogChannel channel = LogChannel.Default,
            LogFrequency frequency = LogFrequency.Default)
        {
            if (!Pass(channel, frequency)) return;
            Debug.LogWarning(Format<T>(message, channel, frequency));
        }

        public static void LogError<T>(
            string message,
            LogChannel channel = LogChannel.Default,
            LogFrequency frequency = LogFrequency.Default)
        {
            if (!Pass(channel, frequency)) return;
            Debug.LogError(Format<T>(message, channel, frequency));
        }

        public static bool IsChannelVisible(LogChannel channel)
        {
            return IsVisible(ChannelVisible, (int)channel);
        }

        public static bool IsFrequencyVisible(LogFrequency frequency)
        {
            return IsVisible(FrequencyVisible, (int)frequency);
        }

        public static void SetChannelVisible(LogChannel channel, bool visible)
        {
            SetVisible(ChannelVisible, (int)channel, visible);
        }

        public static void SetFrequencyVisible(LogFrequency frequency, bool visible)
        {
            SetVisible(FrequencyVisible, (int)frequency, visible);
        }

        public static void SetAllChannelsVisible(bool visible)
        {
            SetAll(ChannelVisible, visible);
        }

        public static void SetAllFrequenciesVisible(bool visible)
        {
            SetAll(FrequencyVisible, visible);
        }

        static bool Pass(LogChannel channel, LogFrequency frequency)
        {
            if (!Enable) return false;
            return IsChannelVisible(channel) && IsFrequencyVisible(frequency);
        }

        static string Format(string message, LogChannel channel, LogFrequency frequency)
        {
            return $"[{channel}][{frequency}] {message}";
        }

        static string Format<T>(string message, LogChannel channel, LogFrequency frequency)
        {
            return $"[{channel}][{frequency}][{typeof(T).Name}] {message}";
        }

        static bool[] CreateVisibleMask(Type enumType)
        {
            int length = 0;
            foreach (object value in Enum.GetValues(enumType))
            {
                int intValue = Convert.ToInt32(value);
                if (intValue + 1 > length)
                {
                    length = intValue + 1;
                }
            }

            bool[] mask = new bool[length];
            SetAll(mask, true);
            return mask;
        }

        static bool IsVisible(bool[] mask, int index)
        {
            if (index < 0 || index >= mask.Length) return true;
            return mask[index];
        }

        static void SetVisible(bool[] mask, int index, bool visible)
        {
            if (index < 0 || index >= mask.Length) return;
            mask[index] = visible;
        }

        static void SetAll(bool[] mask, bool visible)
        {
            for (int i = 0; i < mask.Length; i++)
            {
                mask[i] = visible;
            }
        }
    }
}
