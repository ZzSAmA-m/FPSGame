// LookAt（朝向玩家组件）：
// 让物体在水平面上始终面向玩家（保持自身高度不变），
// 常用于监控摄像头、告示牌、雕像等需要“注视”玩家的物体。
/// <summary>
/// This script belongs to cowsins™ as a part of the cowsins´ FPS Engine. All rights reserved. 
/// </summary>
using UnityEngine;
namespace cowsins
{
    /// <summary>
    /// 朝向组件：使物体在水平面上持续面向玩家。
    /// </summary>
    public class LookAt : MonoBehaviour
    {
        // 需要朝向的玩家 Transform（为空时自动按标签查找）
        [SerializeField] private Transform player;

        // 计算出的目标朝向位置（缓存）
        private Vector3 lookAtPos;

        // 初始化：若未手动指定玩家，则按 "Player" 标签自动查找
        private void Awake()
        {
            if (player == null)
            {
                GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
                if (playerObj != null)
                    player = playerObj.transform;
            }
        }

        // 每帧更新旋转：仅在水平面上朝向玩家（保持自身高度）
        private void Update()
        {
            if (player == null) return;

            Vector3 currentPos = transform.position;
            // 取玩家水平位置与自身高度构成目标点，避免垂直倾斜
            lookAtPos = new Vector3(player.position.x, currentPos.y, player.position.z);
            transform.rotation = Quaternion.LookRotation(lookAtPos - currentPos);
        }
    }
}