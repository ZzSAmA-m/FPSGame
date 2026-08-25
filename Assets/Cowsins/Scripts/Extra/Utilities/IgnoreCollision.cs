// IgnoreCollision（忽略与玩家的碰撞）：
// 当该物体与玩家发生碰撞时，忽略自身碰撞体与玩家碰撞体之间的物理碰撞。
// 典型用途：地面上的弹壳等杂物，仍与场景交互但不与玩家碰撞，避免踩踏时产生不必要的摩擦。
/// <summary>
/// This script belongs to cowsins™ as a part of the cowsins´ FPS Engine. All rights reserved. 
/// </summary>
using UnityEngine;
namespace cowsins
{
    /// <summary>
    /// Ignore collisions with player
    /// Use it if you need to, but you might find it handy regarding to bullet shells on the ground.
    /// They will interact with the world but not the player, hence we won�t suffer from extra unnecessary friction when walking on top of them.
    /// </summary>
    public class IgnoreCollision : MonoBehaviour
    {
        // 与玩家碰撞时：忽略双方碰撞体之间的碰撞
        void OnCollisionEnter(Collision collision)
        {

            if (collision.transform.CompareTag("Player"))
            {
                Physics.IgnoreCollision(collision.collider, GetComponent<Collider>());
            }
        }

    }
}