// ============================================================================
// WeaponInspectState.cs —— 武器检视状态。
// 玩家检视武器（查看武器外观/实时自定义配件）时进入。进入后初始化检视动画、
// 标记交互系统处于检视中，并可选地剥夺玩家控制权；当玩家再次按检视键、开火
// 或开始奔跑时退出检视并回到默认状态。
// ============================================================================
namespace cowsins
{
    using UnityEngine;
    /// <summary>
    /// 武器检视状态：负责进入/退出检视动画、管理玩家控制权与实时配件自定义，
    /// 并根据输入或移动状态切换回默认状态。
    /// </summary>
    public class WeaponInspectState : WeaponBaseState
    {
        private PlayerDependencies playerDependencies; // 玩家依赖集合，用于统一获取各子系统引用
        private WeaponController controller; // 武器控制器引用
        private WeaponAnimator animator; // 武器动画控制器引用
        private InteractManager interact; // 交互管理器引用，用于控制检视状态
        // 玩家控制接口引用，用于剥夺/恢复玩家控制权
        private IPlayerControlProvider playerControl; // IPlayerControlProvider is implemented in PlayerControl.cs
        // 玩家移动状态接口引用，用于读取奔跑速度等移动状态
        private IPlayerMovementStateProvider player; // IPlayerMovementStateProvider is implemented in PlayerMovement.cs
        // 交互事件接口引用，用于广播开始/停止检视事件
        private IInteractEventsProvider interactEvents; // IInteractEventsProvider is implemented in InteractManager.cs
        private InputManager inputManager; // 输入管理器引用

        private float timer; // 计时器：进入检视后前 1 秒内不允许退出，防止误操作

        // 构造函数：从上下文中缓存依赖集合、控制器、动画、交互、输入等引用。
        public WeaponInspectState(WeaponStates currentContext, WeaponStateFactory playerStateFactory)
            : base(currentContext, playerStateFactory)
        {
            playerDependencies = _ctx.Dependencies;
            player = _ctx.PlayerMovement;
            controller = _ctx.WeaponController;
            animator = _ctx.WeaponAnimator;
            interact = _ctx.InteractManager;
            playerControl = _ctx.PlayerControlProvider;
            interactEvents = playerDependencies.InteractEvents;
            inputManager = playerDependencies.InputManager;
        }

        // 进入检视状态：重置计时器，初始化检视动画，标记交互系统进入检视，
        // 并在启用实时配件自定义时触发开始检视事件；同时订阅再次按检视键即退出的输入。
        public sealed override void EnterState()
        {
            timer = 0;

            animator.InitializeInspection(); // 初始化检视动画
            interact.ToggleInspectionState(true); // 将交互系统标记为正在检视

            // 若启用实时配件自定义，则广播「开始实时检视」事件
            if (interact.RealtimeAttachmentCustomization)
                interactEvents.Events.OnStartRealtimeInspection?.Invoke(interact.DisplayCurrentAttachmentsOnly);

            inputManager.OnInspect += SwitchToDefault; // 再次按检视键即切换回默认状态
        }


        // 每帧更新：实时自定义时剥夺玩家控制；累计计时；退出瞄准并检查状态切换。
        public sealed override void UpdateState()
        {
            if (interact.RealtimeAttachmentCustomization) playerControl.LoseControl(); // 实时自定义期间剥夺控制权

            if (timer <= 1) timer += Time.deltaTime; // 进入后至少停留约 1 秒

            controller.aimBehaviour?.Exit(); // 检视时退出瞄准

            CheckSwitchState();
        }

        public sealed override void FixedUpdateState() { }

        // 退出检视状态：关闭检视标记、禁用检视动画、尝试恢复玩家控制，
        // 并广播关闭配件 UI 与停止检视事件，最后取消输入订阅。
        public sealed override void ExitState()
        {
            interact.ToggleInspectionState(false);
            animator.DisableInspection();
            playerControl.CheckIfCanGrantControl(); // 视情况恢复玩家控制权

            UIEvents.onEnableAttachmentUI?.Invoke(null); // 通知 UI 关闭配件界面
            interact.Events.OnStopInspect?.Invoke();

            inputManager.OnInspect -= SwitchToDefault;
        }
        // 检查状态切换：计时未满 1 秒不切换；开火（非实时自定义）或开始奔跑时切回默认状态。
        public sealed override void CheckSwitchState()
        {
            if (timer < 1) return; // 防止刚进入检视就立刻退出
            if (inputManager.Shooting && !interact.RealtimeAttachmentCustomization || player.CurrentSpeed == player.RunSpeed) SwitchState(_factory.Default());
        }

        // 按检视键退出：计时未满 1 秒时忽略，否则切回默认状态。
        private void SwitchToDefault()
        {
            if (timer < 1) return;
            SwitchState(_factory.Default());
        }
    }
}