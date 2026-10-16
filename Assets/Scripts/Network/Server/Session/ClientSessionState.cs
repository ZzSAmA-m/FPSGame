namespace Network.Server.Session
{
    /// <summary>
    /// 一条客户端连接在比赛服务器中的生命周期阶段。
    /// </summary>
    public enum ClientSessionState
    {
        /// <summary>Transport 已连接，但尚未通过项目协议握手。</summary>
        Connected,

        /// <summary>客户端协议版本与服务器兼容。</summary>
        ProtocolVerified,

        /// <summary>客户端已完成场景加载，并通过 Mirror Ready 流程。</summary>
        Ready,

        /// <summary>服务器已经为连接创建并绑定玩家实体。</summary>
        Playing,

        /// <summary>连接正在退出；不应再接受新的游戏请求。</summary>
        Disconnecting
    }
}
