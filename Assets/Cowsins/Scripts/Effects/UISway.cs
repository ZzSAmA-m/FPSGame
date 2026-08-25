// ============================================================
// UISway.cs —— UI 鼠标摆动（视差）效果
// 使挂载此组件的 UI 元素（RectTransform）跟随鼠标位置做反向偏移，
// 从而在界面中形成轻微的视差 / 摆动观感。偏移强度由 amount 控制，
// speed 控制平滑插值速度，逐帧通过 Lerp 平滑逼近目标位置。
// ============================================================
using UnityEngine;
using UnityEngine.InputSystem;
namespace cowsins
{
    /// <summary>UI 摆动效果：根据鼠标相对屏幕中心的位置，反向平移 UI 元素，产生视差摆动。</summary>
    public class UISway : MonoBehaviour
    {
        // 鼠标移动对 UI 偏移的影响强度（数值越大摆动越明显）。
        [SerializeField] private float amount;
        // UI 位置逼近目标值的平滑插值速度。
        [SerializeField] private float speed;

        // 缓存的 RectTransform 引用，避免每帧重复调用 GetComponent。
        private RectTransform rect;

        // 初始化：缓存自身的 RectTransform 组件引用。
        private void Start() => rect = GetComponent<RectTransform>();

        // 每帧更新：根据鼠标位置对 UI 施加反向偏移，实现平滑的视差摆动。
        private void Update()
        {
            if (Mouse.current == null) return;

            // 以屏幕中心为原点取鼠标位置的相反方向，乘以 amount/100 作为摆动幅度，再用 Lerp 平滑过渡到目标位置。
            rect.localPosition = Vector3.Lerp(rect.localPosition, (-Mouse.current.position.ReadValue() + new Vector2(Screen.width / 2, Screen.height / 2)) * amount / 100, Time.deltaTime * speed);
        }
    }
}