using System;
using Core.ZLog;
using Network.Common;

using Core.Utility;
using Network.Common.Messages;
using UnityEngine;
using GamePlay.Player;

namespace Network.Client
{
    public sealed partial class ClientManager : Singleton<ClientManager>
    {
        private void HandleHelloResponse(HelloResponse response)
        {
            if (!response.accepted)
            {
                Debug.LogError(
                    $"[FPSNetworkManager] 服务器拒绝协议握手：{response.message} " +
                    $"(serverProtocol={response.protocolVersion})");
                return;
            }

            Debug.Log(
                $"[FPSNetworkManager] 协议握手完成：{response.message} " +
                $"(protocol={response.protocolVersion})");
        }
        
        private void HandlePlayerSnapshot(PlayerSnapshotMessage message)
        {
            var players=PlayerManager.Players;
            foreach(var msg in message.playerPoses)
            {
                if (players.ContainsKey(msg.netId))
                {
                    players[msg.netId].GetComponent<PlayerController>().NetworkManager.NetPlayerMovement.UpdateWithSnapshot(msg);
                }
            }
            ZLog.Log<ClientManager>("Received Snapshot: "+message.ToString());
        }
        private void HandleGetClientId(int id)
        {
            ClientId=id;
            ZLog.Log<ClientManager>("收到服务器分发的ClientId: "+id);
        }
    }
}
