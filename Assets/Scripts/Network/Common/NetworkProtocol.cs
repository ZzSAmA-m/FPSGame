namespace Network.Common
{
    /// <summary>自定义消息结构或语义不兼容时，递增协议版本。</summary>
    public static class NetworkProtocol
    {
        /// <summary>当前项目协议版本。</summary>
        public const int Version = 2;

        /// <summary>日志中允许保留的客户端版本字符串最大长度。</summary>
        public const int MaxClientVersionLength = 32;
    }
}
