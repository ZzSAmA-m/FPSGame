using Mirror;
using UnityEngine;

namespace Network.Common.Messages
{
    /// <summary>
    /// 本地玩家在一个采样窗口内产生的操作命令。
    /// </summary>
    public struct PlayerCommandMessage : NetworkMessage
    {
        public uint sequence;
        /// <summary>世界空间水平位移。x 为世界 X，y 为世界 Z。</summary>
        public Vector2 move;
        /// <summary>窗口内的视角增量。</summary>
        public Vector2 look;
        public bool jump;
        public bool fire;
        public bool key1;
        public bool key2;
        public bool key3;

        public override string ToString()
        {
            return $"PlayerCommand(sequence={sequence}, move={move}, look={look}, jump={jump}, fire={fire}, key1={key1}, key2={key2}, key3={key3})";
        }
    }
}
