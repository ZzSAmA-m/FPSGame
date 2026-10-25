namespace Core.ZLog
{
    /// <summary>
    /// 日志分类。编辑器里可以按分类单独显隐。
    /// </summary>
    public enum LogChannel
    {
        /// <summary>未归类的日志。</summary>
        Default,

        /// <summary>输入。</summary>
        Input,

        /// <summary>网络。</summary>
        Network,

        /// <summary>玩法。</summary>
        GamePlay,
    }
}
