// DestroyMe（延时自毁组件）：
// 挂载后经过 timeToDestroy 秒自动销毁自身游戏物体，
// 常用于子弹弹壳、临时特效、一次性物体等需要定时清除的对象。
/// <summary>
/// This script belongs to cowsins™ as a part of the cowsins´ FPS Engine. All rights reserved. 
/// </summary>
using UnityEngine;
namespace cowsins
{
    /// <summary>
    /// 延时自毁组件：在指定时间后销毁自身游戏物体。
    /// </summary>
    public class DestroyMe : MonoBehaviour
    {
        // 销毁前等待的时间（秒）
        public float timeToDestroy;

        // 启动后延时调用销毁方法
        private void Start() => Invoke("DestroyMeObj", timeToDestroy);

        // 实际销毁自身游戏物体
        private void DestroyMeObj() => Destroy(this.gameObject);

    }
}