using Mirror;

namespace Network.Common.Messages
{
    /// <summary>
    /// 服务器对协议握手的响应。
    /// </summary>
    public struct HelloResponse : NetworkMessage
    {
        /// <summary>服务器当前使用的项目网络协议版本。</summary>
        public int protocolVersion;

        /// <summary>当前客户端是否通过协议兼容性检查。</summary>
        public bool accepted;

        /// <summary>面向开发者的简短结果说明，不承载业务数据。</summary>
        public string message;
    }
}
