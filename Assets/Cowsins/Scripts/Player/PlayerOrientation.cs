// ============================================================================
// PlayerOrientation.cs —— 玩家朝向数据类
// 作用：用纯数据方式保存玩家的位置与旋转（朝向），替代原先场景中绕 Y 轴旋转的
//       朝向 Transform。PlayerMovement 会创建自己的 PlayerOrientation 实例。
// ============================================================================
using UnityEngine;

namespace cowsins
{
    /// <summary>玩家朝向数据类：保存玩家的位置、旋转并提供朝向向量与偏航角。</summary>
    /// <summary>
    /// Introduced in 1.3.8. Orientation used to be a Transform in the scene that rotated around the y axis ( yaw ). 
    /// Now, orientation information is stored inside PlayerOrientation class. PlayerMovement creates its own instance of PlayerOrientation.
    /// </summary>
    public class PlayerOrientation
    {
        // 玩家位置。
        private Vector3 position;
        // 玩家旋转（朝向）。
        private Quaternion rotation;

        /// <summary>构造函数：用给定的初始位置与旋转初始化朝向数据。</summary>
        /// <param name="initialPosition">初始位置。</param>
        /// <param name="initialRotation">初始旋转。</param>
        public PlayerOrientation(Vector3 initialPosition, Quaternion initialRotation)
        {
            position = initialPosition;
            rotation = initialRotation;
        }

        /// <summary>玩家位置属性：读取时返回位置，写入时自动向上偏移 2 单位（补偿视线高度）。</summary>
        public Vector3 Position
        {
            get => position;
            set => position = value + Vector3.up * 2;
        }

        /// <summary>玩家旋转属性：读写玩家的朝向四元数。</summary>
        public Quaternion Rotation
        {
            get => rotation;
            set => rotation = value;
        }


        // 基于当前朝向计算的正前方方向向量。
        public Vector3 Forward => rotation * Vector3.forward;
        // 基于当前朝向计算的右侧方向向量。
        public Vector3 Right => rotation * Vector3.right;
        // 基于当前朝向计算的上方方向向量。
        public Vector3 Up => rotation * Vector3.up;
        // 偏航角：当前朝向绕 Y 轴的欧拉角。
        public float Yaw => rotation.eulerAngles.y;


        /// <summary>直接设置朝向旋转。</summary>
        /// <param name="newRotation">新的旋转四元数。</param>
        public void SetRotation(Quaternion newRotation)
        {
            rotation = newRotation;
        }

        /// <summary>在当前旋转基础上叠加一段欧拉角旋转。</summary>
        /// <param name="eulerAngles">要叠加的欧拉角（度）。</param>
        public void Rotate(Vector3 eulerAngles)
        {
            rotation *= Quaternion.Euler(eulerAngles);
        }

        /// <summary>更新位置与朝向：设置位置，并按期望的偏航角重建绕 Y 轴旋转。</summary>
        /// <param name="pos">新的位置。</param>
        /// <param name="desiredX">期望的偏航角（度）。</param>
        public void UpdateOrientation(Vector3 pos, float desiredX)
        {
            position = pos;
            rotation = Quaternion.Euler(0, desiredX, 0);
        }
    }
}