// ============================================================================
// RebindUI.cs —— 按键重绑定 UI 组件。
// 每个实例对应一个输入动作（InputAction），负责显示该动作当前绑定、提供
// 「重新绑定」与「重置」按钮，并通过 InputManager 完成实际的按键监听、
// 覆盖保存与取消等流程，同时同步显示绑定结果。
// ============================================================================
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;

namespace cowsins
{
    /// <summary>
    /// 按键重绑定 UI：显示输入动作名称与当前按键，提供重新绑定与重置功能。
    /// </summary>
    public class RebindUI : MonoBehaviour
    {
        [SerializeField]
        private InputActionReference inputActionReference; // 关联的输入动作引用

        [SerializeField]
        private bool excludeMouseFromBinding = true; // 重新绑定时是否排除鼠标

        [Range(0, 15), SerializeField]
        private int selectedBinding; // 选中的绑定索引（一个动作可有多个绑定）

        [SerializeField]
        private InputBinding.DisplayStringOptions displayStringOptions; // 绑定显示字符串的格式选项

        [SerializeField, ReadOnly]
        private InputBinding inputBinding; // 缓存的绑定数据（Inspector 只读）

        private int bindingIndex; // 运行时使用的绑定索引

        private string actionName; // 输入动作名称

        [SerializeField] private TextMeshProUGUI actionText; // 显示动作名称的文本

        [SerializeField] private Button rebindButton; // 重新绑定按钮

        [SerializeField] private TextMeshProUGUI rebindText; // 显示当前绑定按键的文本

        [SerializeField] private Button resetButton; // 重置按钮

        [SerializeField] private GameObject rebindOverlay; // 重新绑定时的遮罩层

        [SerializeField] private TextMeshProUGUI rebindOverlayTitle; // 遮罩层标题文本

        // 编辑器校验：引用存在时刷新绑定信息与 UI。
        private void OnValidate()
        {
            if (inputActionReference == null) return;

            GetBindingInfo();
            UpdateUI();
        }

        // 启用时：为重新绑定与重置按钮注册点击监听。
        private void OnEnable()
        {
            rebindButton.onClick.AddListener(PerformRebind);
            resetButton.onClick.AddListener(ResetRebind);
        }

        // 初始化：读取绑定信息、加载已保存的覆盖绑定、刷新 UI，并订阅重绑定完成/取消事件。
        private void Awake()
        {
            if (inputActionReference == null) return;

            GetBindingInfo();

            InputManager.LoadBindingOverride(actionName); // 加载该动作的绑定覆盖

            UpdateUI();

            InputManager.rebindComplete += UpdateUI; // 重绑定完成后刷新 UI
            InputManager.rebindCanceled += UpdateUI; // 重绑定取消后刷新 UI
        }

        // 禁用时：移除按钮监听。
        private void OnDisable()
        {
            rebindButton.onClick.RemoveListener(PerformRebind);
            resetButton.onClick.RemoveListener(ResetRebind);
        }

        // 销毁时：取消订阅重绑定完成/取消事件。
        private void OnDestroy()
        {
            InputManager.rebindComplete -= UpdateUI;
            InputManager.rebindCanceled -= UpdateUI;
        }
        
        // 获取绑定信息：缓存动作名称，并读取选定索引对应的绑定数据与索引。
        private void GetBindingInfo()
        {
            if (inputActionReference.action != null)
                actionName = inputActionReference.action.name; // 记录动作名称

            if (inputActionReference.action.bindings.Count > selectedBinding) // 索引有效时
            {
                inputBinding = inputActionReference.action.bindings[selectedBinding];
                bindingIndex = selectedBinding;
            }
        }

        // 刷新 UI：更新动作名称文本与当前绑定按键文本（运行中读取覆盖，编辑中读取原始绑定）。
        private void UpdateUI()
        {
            if (actionText != null)
                actionText.text = actionName;

            if (rebindText != null)
            {
                if (Application.isPlaying)
                {
                    rebindText.text = InputManager.GetBindingName(actionName, bindingIndex); // 运行时显示覆盖后的绑定名
                }
                else
                {
                    rebindText.text = inputActionReference.action.GetBindingDisplayString(bindingIndex); // 编辑模式下显示原始绑定
                }
            }
        }

        // 执行重新绑定：调用 InputManager 开始监听新按键。
        private void PerformRebind()
        {
            InputManager.StartRebind(actionName, bindingIndex, rebindText, excludeMouseFromBinding, rebindOverlay, rebindOverlayTitle);
        }

        // 重置绑定：调用 InputManager 恢复默认绑定并刷新 UI。
        private void ResetRebind()
        {
            InputManager.ResetBinding(actionName, bindingIndex);

            UpdateUI();
        }
    }
}
