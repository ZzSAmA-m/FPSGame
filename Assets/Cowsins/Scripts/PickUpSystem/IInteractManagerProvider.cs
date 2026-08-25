// ----------------------------------------------------------------------
// IInteractManagerProvider.cs
// 作用：定义交互管理相关的接口与事件集合类型。
//       IInteractManagerProvider / IInteractEventsProvider 由 InteractManager 实现，
//       供 PlayerDependencies 等模块以解耦方式访问交互状态与交互事件；
//       InteractManagerEvents 则集中声明交互过程中的各类 UnityEvent。
// ----------------------------------------------------------------------
using UnityEngine.Events;

namespace cowsins
{
    /// <summary>
    /// 交互管理器访问接口。由 InteractManager 实现，PlayerDependencies 依赖它以解耦方式
    /// 获取交互进度、检视状态、当前高亮交互物以及重复武器转子弹等配置信息。
    /// </summary>
    // Implemented by InteractManager and required by PlayerDependencies
    public interface IInteractManagerProvider
    {
        // 当前交互的进度值（通常为 0~1）。
        float ProgressElapsed { get; }
        // 玩家是否正处于实时检视（inspect）状态。
        bool Inspecting { get; }
        // 当前被高亮显示的可交互对象。
        Interactable HighlightedInteractable { get; }
        // 拾取已拥有的重复武器时，是否将其转换为子弹。
        bool DuplicateWeaponAddsBullets { get; }
    }

    /// <summary>
    /// 交互事件访问接口。由持有 InteractManagerEvents 的对象实现，用于获取交互事件集合。
    /// </summary>
    public interface IInteractEventsProvider
    {
        // 交互事件集合属性，返回一组可供订阅的交互事件。
        InteractManagerEvents Events { get; }
    }
    /// <summary>
    /// 交互事件集合类。集中声明交互系统对外暴露的各类事件（允许交互、进度变化、
    /// 执行/结束交互、禁用/禁止交互、拾取子弹/配件、丢弃、实时检视等），供外部模块订阅。
    /// </summary>
    public class InteractManagerEvents
    {
        // 允许交互时触发，参数为与本次交互相关的字符串（如交互提示文本）。
        public UnityEvent<string> OnAllowedInteraction = new UnityEvent<string>();
        // 交互进度变化时触发，参数为当前进度值。
        public UnityEvent<float> OnInteractionProgressChanged = new UnityEvent<float>();
        // 开始执行交互时触发。
        public UnityEvent OnPerformInteraction = new UnityEvent();
        // 完成交互时触发。
        public UnityEvent OnFinishInteraction = new UnityEvent();
        // 交互被禁用时触发。
        public UnityEvent OnDisableInteraction = new UnityEvent();
        // 尝试交互但条件不满足（被禁止）时触发。
        public UnityEvent OnForbiddenInteraction = new UnityEvent();

        // 拾取子弹时触发。
        public UnityEvent OnBulletsPickedUp = new UnityEvent();
        // 拾取配件时触发，参数为拾取到的配件。
        public UnityEvent<Attachment> OnAttachmentPickedUp = new UnityEvent<Attachment>();
        // 丢弃物品时触发。
        public UnityEvent OnDrop = new UnityEvent();

        // 开始/结束实时检视时触发，参数表示检视的开启状态。
        public UnityEvent<bool> OnStartRealtimeInspection = new UnityEvent<bool>();
        // 停止检视时触发。
        public UnityEvent OnStopInspect = new UnityEvent();
        // 请求刷新检视 UI 时触发，参数为刷新请求相关的布尔标志。
        public UnityEvent<bool> OnInspectionUIRefreshRequested = new UnityEvent<bool>();
    }
}