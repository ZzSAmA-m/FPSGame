using System;
using Mirror;
using Network.Common.Messages;
using UnityEngine;

namespace Network.Common
{
    /// <summary>
    /// Mirror 消息系统与项目业务层之间的薄适配器。
    /// </summary>
    /// <remarks>
    /// NetBridge 只负责“注册消息、发送消息、把收到的消息交给上层”，
    /// 不在这里处理玩家列表、比赛规则或伤害等业务。这样更换消息内容时，
    /// 不会迫使服务器业务跟着依赖 Mirror 的静态 API。
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed partial class NetBridge : MonoBehaviour
    {
        private bool _serverHandlersRegistered;
        private bool _clientHandlersRegistered;



       
    }
}
