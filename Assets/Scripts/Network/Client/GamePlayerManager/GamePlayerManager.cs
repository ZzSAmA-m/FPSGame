using System.Collections.Generic;
using UnityEngine;

namespace Network.Client
{
    public sealed class GamePlayerManager
    {
        public Dictionary<uint, GameObject> Players { get; } = new();

        public void Add(uint netId, GameObject player)
        {
            Players[netId] = player;
        }

        public void Remove(uint netId)
        {
            Players.Remove(netId);
        }
    }
}
