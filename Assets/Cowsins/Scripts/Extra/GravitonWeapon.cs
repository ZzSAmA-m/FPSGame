// GravitonWeapon（重力武器 / 引力枪）：
// 挂载在武器或交互物体上，用于把射线命中的目标物体“吸附”到玩家前方，
// 实现类似“隔空抓取并平滑拖拽物体”的物理效果。该效果由外部（如武器开火逻辑）调用 Graviton() 方法触发。
using UnityEngine;
namespace cowsins
{
    /// <summary>
    /// 引力武器组件：从玩家摄像机发射射线，命中后将目标物体平滑地拖拽到射线末端。
    /// </summary>
    public class GravitonWeapon : MonoBehaviour
    {
        // 玩家摄像机引用，用于确定射线起点与朝向
        [SerializeField] private Transform playerCamera;

        // 射线检测的最大距离，同时也决定物体被拖拽到的目标位置
        [SerializeField] private float maxDistance;

        // 射线可命中的层遮罩，用于过滤可被吸附的物体
        [SerializeField] private LayerMask hitLayer;

        // 物体被拖拽时的插值速度（越大吸附越快）
        [SerializeField] private float lerpSpeed;

        // 当前被吸附的目标物体的射线命中信息
        RaycastHit obj;
        // 执行一次引力吸附：检测射线命中并更新目标物体位置
        public void Graviton()
        {
            RaycastHit hit;
            Ray ray = new Ray(playerCamera.position, playerCamera.forward);
            // 发射射线，若命中可吸附层则记录命中信息
            if (Physics.Raycast(ray, out hit, maxDistance, hitLayer)) obj = hit;
            // 没有目标，或目标已超出两倍最大距离时不再吸附（防止拖拽过远物体）
            if (obj.transform == null || obj.transform != null && Vector3.Distance(obj.transform.position, transform.position) > maxDistance * 2) return;
            // 将目标物体位置向射线末端进行插值移动，实现平滑拖拽
            obj.transform.position = Vector3.Lerp(obj.transform.position, ray.GetPoint(maxDistance), lerpSpeed * Time.deltaTime);
        }
    }
}