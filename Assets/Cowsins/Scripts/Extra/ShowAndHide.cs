// ShowAndHide（显示/隐藏面板）：
// 监听一个输入动作（InputAction），当该动作被触发时切换指定 UI 面板的激活状态，
// 常用于按某个按键（如 Tab）显示/隐藏计分板或说明面板。
using UnityEngine;
using UnityEngine.InputSystem;
namespace cowsins
{
    /// <summary>
    /// 面板显示/隐藏组件：监听输入动作以切换面板的显示状态。
    /// </summary>
    public class ShowAndHide : MonoBehaviour
    {
        // 需要被切换显示/隐藏的面板对象
        [SerializeField] private GameObject panel;
        // 触发切换的输入动作引用
        [SerializeField] private InputActionReference toggleAction;

        // 启用时开启输入动作并订阅触发回调
        private void OnEnable()
        {
            toggleAction.action.Enable();
            toggleAction.action.performed += TogglePanel;
        }

        // 禁用时关闭输入动作并取消订阅
        private void OnDisable()
        {
            toggleAction.action.Disable();
            toggleAction.action.performed -= TogglePanel;
        }

        // 输入触发时翻转面板激活状态
        private void TogglePanel(InputAction.CallbackContext context) => panel.SetActive(!panel.activeSelf);
    }
}