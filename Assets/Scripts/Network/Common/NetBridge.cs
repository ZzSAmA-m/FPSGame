using UnityEngine;

namespace Network.Common
{
    /// <summary>注册和收发网络消息，业务判断交给上层。</summary>
    [DisallowMultipleComponent]
    public sealed partial class NetBridge : MonoBehaviour
    {
        private bool _serverHandlersRegistered;
        private bool _clientHandlersRegistered;
    }
}
