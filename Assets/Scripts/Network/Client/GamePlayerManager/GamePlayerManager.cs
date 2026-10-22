using System.Collections.Generic;
using GamePlay.Player;
using GamePlay.Player.Network;
using Mirror;
using UnityEngine;
namespace Network
{
    public class GamePlayerManager
    {
        public Dictionary<int,GameObject> Players{get;private set;}=new();
        public void AddPlayer(GameObject obj)
        {
            Players.Add(obj.GetComponent<PlayerController>().NetworkManager.GetComponent<NetworkId>().Id,obj);
        }
    }
}
