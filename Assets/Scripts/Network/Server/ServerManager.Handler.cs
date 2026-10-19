using System;
using System.Collections.Generic;
using Core.ZLog;
using Mirror;
using Network.Common;
using Network.Common.Messages;
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
            // if (!Sessions.TryGet(sender, out ClientSession session) ||
            //     session.State != ClientSessionState.Playing ||
            //     session.PlayerIdentity == null)
            // {
            //     return;
            // }
            var session=Sessions.GetClientSession(sender.connectionId);

            if (_lastCommandSequence.TryGetValue(sender.connectionId, out uint lastSequence) &&
                command.sequence <= lastSequence)
            {
                return;
            }

            _lastCommandSequence[sender.connectionId] = command.sequence;
            ApplyCommand(session.Conn.identity, command);
            ZLog.Log<ServerManager>("Receive Command: "+command);
        }


        private void HandleHelloRequest(NetworkConnectionToClient connection, HelloRequest request)
        {

            var session=Sessions.GetClientSession(connection.connectionId);
            if (request.protocolVersion != NetworkProtocol.Version)
            {
                RejectAndDisconnect(
                    connection,
                    $"协议不兼容:client={request.protocolVersion}, server={NetworkProtocol.Version}");
                return;
            }

            string clientVersion = SanitizeClientVersion(request.clientVersion);

            _bridge.SendHelloToClient(connection, new HelloResponse
            {
                protocolVersion = NetworkProtocol.Version,
                accepted = true,
                message = "协议握手成功。"
            });

            Debug.Log($"[ServerManager] 协议握手通过：{session}");
        }


        private void HandleGetClientIdMsg(NetworkConnectionToClient client)
        {
            var id = Sessions.GetClientSession(client.connectionId).Id;
            var result=new GetClientIdResponse{Id=id};
            _bridge.SendClientId(client,ref result);
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
