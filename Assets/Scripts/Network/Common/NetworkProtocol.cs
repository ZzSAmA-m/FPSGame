namespace Network.Common
{
    /// <summary>
    /// 项目自定义网络协议的公共常量。
    /// </summary>
    /// <remarks>
    /// Mirror 自己有框架版本，但框架版本不能表达“本项目消息字段是否兼容”。
    /// 每当破坏性修改自定义消息结构或语义时，应递增 <see cref="Version"/>。
    /// </remarks>
    public static class NetworkProtocol
    {
        /// <summary>当前项目协议版本。</summary>
        public const int Version = 1;

        /// <summary>日志中允许保留的客户端版本字符串最大长度。</summary>
        public const int MaxClientVersionLength = 32;
    }
}
