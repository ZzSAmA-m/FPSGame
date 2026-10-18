using System;
using System.Collections.Generic;
using Core.ZLog;
using Mirror;
using Network.Common;
using Network.Common.Messages;
using Network.Server.Session;
using UnityEngine;

namespace Network.Server
{
    public sealed partial class ServerManager:MonoBehaviour
    {
        /// <summary>
        /// 处理本地玩家上报的一个采样窗口。
        /// 只接受正在对局的会话，并按序号丢掉重复或过期命令。
        /// </summary>
        private void HandlePlayerCommand(NetworkConnectionToClient sender, PlayerCommandMessage command)
        {
            if (!Sessions.TryGet(sender, out ClientSession session) ||
                session.State != ClientSessionState.Playing ||
                session.PlayerIdentity == null)
            {
                return;
            }

            if (_lastCommandSequence.TryGetValue(sender.connectionId, out uint lastSequence) &&
                command.sequence <= lastSequence)
            {
                return;
            }

            _lastCommandSequence[sender.connectionId] = command.sequence;
            ApplyCommand(session.PlayerIdentity, command);
            ZLog.Log<ServerManager>("Receive Command: "+command);
        }


        private void HandleHelloRequest(NetworkConnectionToClient connection, HelloRequest request)
        {
            if (!Sessions.TryGet(connection, out ClientSession session))
            {
                RejectAndDisconnect(connection, "服务器没有找到对应会话。");
                return;
            }

            if (request.protocolVersion != NetworkProtocol.Version)
            {
                RejectAndDisconnect(
                    connection,
                    $"协议不兼容:client={request.protocolVersion}, server={NetworkProtocol.Version}");
                return;
            }

            string clientVersion = SanitizeClientVersion(request.clientVersion);
            if (!session.VerifyProtocol(clientVersion))
            {
                RejectAndDisconnect(connection, "握手重复或会话阶段不正确。");
                return;
            }

            _bridge.SendHelloToClient(connection, new HelloResponse
            {
                protocolVersion = NetworkProtocol.Version,
                accepted = true,
                message = "协议握手成功。"
            });

            Debug.Log($"[ServerManager] 协议握手通过：{session}");
        }


        /// <summary>
        /// 在服务器上表现一个采样窗口：位移、转向，以及跳跃、开火和切枪。
        /// </summary>
        private static void ApplyCommand(NetworkIdentity player, PlayerCommandMessage command)
        {
            if (!player.TryGetComponent(out IServerCommandTarget target))
            {
                ZLog.LogError<ServerManager>($"[ServerManager] 玩家对象没有 {nameof(IServerCommandTarget)}:{player}");
                return;
            }

            target.ApplyServerCommand(ref command);
        }
    }
}
