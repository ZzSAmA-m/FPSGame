// 文件：DeviceDetection.cs
// 作用：检测玩家当前使用的输入设备（键盘/鼠标 或 手柄），并维护当前的输入模式。
//       通过订阅 Unity Input System 的底层事件，在设备发生切换时更新模式，
//       并通过 OnInputModeChanged 事件通知其它模块（例如用于切换 UI 提示）。
using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace cowsins
{
    /// <summary>
    /// 输入设备检测器：识别当前活动输入设备属于键盘还是手柄，并广播输入模式切换事件。
    /// </summary>
    public class DeviceDetection : MonoBehaviour
    {
        /// <summary>
        /// 输入模式枚举：键盘（含鼠标） / 手柄。
        /// </summary>
        public enum InputMode
        {
            Keyboard,
            Controller
        }

        // 当前输入模式（外部只读）。
        public InputMode mode { get; private set; }

        // DeviceDetection 的单例实例。
        public static DeviceDetection Instance;

        // 最近一次产生输入事件的设备，用于判断设备是否发生切换。
        private InputDevice lastDevice;

        // 输入模式发生变化时触发的事件，参数为新的输入模式。
        public event Action<InputMode> OnInputModeChanged;

        // 初始化单例：仅在不存在实例时把自身设为 Instance。
        private void Awake()
        {
            if (Instance == null) Instance = this;
        }

        // 启用时订阅 Input System 的底层事件，以监听所有输入设备的原始事件。
        private void OnEnable() => InputSystem.onEvent += OnInputEvent;

        // 禁用时取消订阅，避免残留回调导致异常。
        private void OnDisable() => InputSystem.onEvent -= OnInputEvent;

        // 处理 Input System 的原始输入事件：当产生事件的设备与上次不同且为有效状态事件时，
        // 更新 lastDevice，判定设备类型并更新 mode，必要时触发 OnInputModeChanged。
        private void OnInputEvent(InputEventPtr eventPtr, InputDevice device)
        {
            // 仅处理来自新设备的有效状态事件（StateEvent / DeltaStateEvent），过滤掉其他无关事件。
            if (device != lastDevice && (eventPtr.IsA<StateEvent>() || eventPtr.IsA<DeltaStateEvent>()))
            {
                lastDevice = device;
                InputMode newMode;

                // 依据设备类型映射为对应的输入模式。
                if (IsKeyboard(device))
                    newMode = InputMode.Keyboard;
                else if (IsController(device))
                    newMode = InputMode.Controller;
                else
                    return;

                // 仅当新模式与当前模式不同时才更新并广播事件。
                if (newMode != mode)
                {
                    mode = newMode;
                    OnInputModeChanged?.Invoke(mode);
                }
            }
        }

        // 判断设备是否为手柄（Gamepad）。
        private bool IsController(InputDevice device)
        {
            return device is Gamepad;
        }

        // 判断设备是否为键盘或鼠标（两者统一视为键盘模式）。
        private bool IsKeyboard(InputDevice device)
        {
            return device is Keyboard || device is Mouse;
        }
    }
}
