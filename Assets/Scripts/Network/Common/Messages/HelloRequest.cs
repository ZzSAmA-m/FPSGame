using Mirror;

namespace Network.Common.Messages
{
    /// <summary>
    /// 客户端完成 Mirror 连接后发送的协议握手消息。
    /// </summary>
    /// <remarks>
    /// 这不是登录或账号认证。它只用于确认客户端与服务器使用兼容的网络协议，
    /// 并为以后接入真正的 Authenticator 留出一个稳定入口。
    /// </remarks>
    public struct HelloRequest : NetworkMessage
    {
        /// <summary>客户端使用的项目网络协议版本。</summary>
        public int protocolVersion;

        /// <summary>
        /// 客户端构建版本，仅用于日志和问题排查，服务器不能把它当作可信身份。
        /// </summary>
        public string clientVersion;
    }
}
