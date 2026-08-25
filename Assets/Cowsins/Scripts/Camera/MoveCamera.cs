// MoveCamera.cs：相机位置跟随脚本。
// 使挂载本组件的相机每帧跟随指定“相机头部”变换（cameraHead）的位置，
// 从而让相机始终保持在角色头部处，实现第一人称相机的定位。
namespace cowsins
{
    /// <summary>
    /// This script belongs to cowsins™ as a part of the cowsins´ FPS Engine. All rights reserved. 
    /// </summary>
    using UnityEngine;

    // 保持相机位置的组件：每帧将相机位置对齐到相机头部。
    /// <summary>
    /// Keep camera in place
    /// </summary>
    public class MoveCamera : MonoBehaviour
    {
        // 相机头部变换引用，定义相机应放置的位置（可在 Inspector 中配置）。
        [Tooltip("Reference to our Camera Head Transform, that defines the placement of our Camera"), SerializeField] private Transform cameraHead;

        // 每帧将相机位置同步到相机头部位置，保持相机始终跟随头部移动。
        private void Update() => transform.position = cameraHead.transform.position;
    }
}