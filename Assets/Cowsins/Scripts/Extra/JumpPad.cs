// JumpPad（弹跳板）：
// 当玩家进入触发器时，根据配置的受力模式向玩家施加一个瞬时冲量将其弹起，
// 并播放弹跳音效。支持三种水平力方向模式（玩家朝向 / 玩家移动方向 / 弹跳板自身轴向）。
using UnityEngine;

namespace cowsins
{

    /// <summary>
    /// 弹跳板的水平受力方向模式枚举。
    /// </summary>
    public enum ForceModeType
    {
        // 水平力沿玩家当前朝向
        PlayerOrientation,
        // 水平力沿玩家当前移动速度方向
        PlayerMovementDirection,
        // 水平力沿弹跳板自身的局部轴
        JumpPadLocalAxis
    }

    /// <summary>
    /// 弹跳板组件：玩家踏入后施加垂直与水平冲量，并播放音效。
    /// </summary>
    public class JumpPad : MonoBehaviour
    {
        // 水平力方向模式，默认为玩家朝向
        [SerializeField] private ForceModeType forceModeType = ForceModeType.PlayerOrientation;

        // 垂直与水平冲量的大小
        [SerializeField] private float verticalForceMagnitude = 200f, horizontalForceMagnitude = 200;

        // 弹跳时播放的音效
        [SerializeField] private AudioClip jumpSound;

        // 用于播放音效的音频源
        private AudioSource source;

        // 初始化：获取音频源并设置待播放的音效
        private void Start()
        {
            source = GetComponent<AudioSource>();
            source.clip = jumpSound;
        }

        // 玩家进入触发器时：根据模式计算受力方向并施加冲量
        private void OnTriggerEnter(Collider other)
        {
            if (!other.CompareTag("Player")) return;
            
            Rigidbody playerRigidbody = other.GetComponent<Rigidbody>();
            IPlayerMovementStateProvider player = other.GetComponent<IPlayerMovementStateProvider>();

            if (playerRigidbody != null)
            {
                // 先清空玩家当前速度，避免与已有速度叠加
                playerRigidbody.linearVelocity = Vector3.zero;

                PlayerMovement playerMovement = other.GetComponent<PlayerMovement>();
                if (playerMovement != null)
                {
                    // 标记玩家已处于跳跃状态
                    playerMovement.movementContext.HasJumped = true;
                }

                Vector3 hForceDirection = Vector3.zero;
                Vector3 vForceDirection = Vector3.zero;

                switch (forceModeType)
                {
                    case ForceModeType.PlayerOrientation:
                        hForceDirection = player.Orientation.Forward;
                        vForceDirection = other.transform.up;
                        break;
                    case ForceModeType.PlayerMovementDirection:
                        // 注意：此时速度已被清零，取 normalized 可能得到零向量
                        hForceDirection = playerRigidbody.linearVelocity.normalized;
                        vForceDirection = other.transform.up;
                        break;
                    case ForceModeType.JumpPadLocalAxis:
                        hForceDirection = transform.right;
                        vForceDirection = transform.up;
                        break;
                    default:
                        vForceDirection = other.transform.up;
                        break;
                }

                // 分别施加垂直与水平方向的瞬时冲量
                playerRigidbody.AddForce(vForceDirection * verticalForceMagnitude, ForceMode.Impulse);
                playerRigidbody.AddForce(hForceDirection * horizontalForceMagnitude, ForceMode.Impulse);

                source.Play();
            }
        }
    }
}