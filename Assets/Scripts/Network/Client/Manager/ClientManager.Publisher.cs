using System;
using Core.ZLog;
using Network.Common;
using Core.Utility;
using UnityEngine;
using Core.Input;
using Network.Common.Messages;

namespace Network.Client
{
    public sealed partial class ClientManager : Singleton<ClientManager>
    {
        public void PublishPlayerInputCmd(ref PlayerCommandMessage message)
        {
            _bridge.SendPlayerCommand(ref message);
        }
    }
}
