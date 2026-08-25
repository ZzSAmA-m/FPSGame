// ============================================================================
// PlayerGraphics.cs —— 玩家图形（外观）同步组件
// 作用：让挂载本脚本的物体（通常是玩家模型/图形层）跟随 PlayerDependencies
//       所在的玩家根物体，每帧同步其位置与朝向，使视觉表现与逻辑体保持一致。
// ============================================================================
using UnityEngine;
namespace cowsins
{
    /// <summary>玩家图形同步组件：每帧将自身 Transform 同步到玩家根物体的位置与朝向。</summary>
    public class PlayerGraphics : MonoBehaviour
    {
        // 玩家依赖组件引用：用于获取玩家根物体与移动状态。
        [SerializeField] private PlayerDependencies playerDependencies;

        // 缓存的玩家根物体 Transform，避免每帧重复查询。
        private Transform playerTransform;
        // 玩家移动状态提供者：用于读取玩家的朝向旋转。
        private IPlayerMovementStateProvider playerMovementStateProvider; // IPlayerMovementStateProvider is implemented in PlayerMovement.cs

        // 初始化：缓存玩家根物体 Transform，并获取玩家移动状态提供者。
        private void Start()
        {
            playerTransform = playerDependencies.transform;
            playerMovementStateProvider = playerDependencies.PlayerMovementState;
        }
        // 每帧执行：将本物体的位置与旋转同步为玩家根物体的位置与朝向。
        private void Update()
        {
            transform.position = playerTransform.position;
            transform.rotation = playerMovementStateProvider.Orientation.Rotation;
        }
    }
}