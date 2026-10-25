namespace Core.ZLog
{
    /// <summary>
    /// 日志的噪声等级。编辑器里关掉某一级后，这一级的调用不会打印。不会按帧抽样。
    /// </summary>
    public enum LogFrequency
    {
        /// <summary>未标注等级。</summary>
        Default,

        /// <summary>低。</summary>
        Low,

        /// <summary>中。</summary>
        Medium,

        /// <summary>高。</summary>
        High,

        /// <summary>极高。</summary>
        Extreme,
    }
}
