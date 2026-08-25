// ============================================================================
// WeaponReloadState.cs —— 武器换弹状态。
// 玩家触发换弹后进入。进入时启动换弹行为，在换弹期间可选择是否允许取消换弹
// 来切换武器，并处理瞄准的退出；当换弹完成（IsReloading 变为 false）后自动
// 切回默认状态。
// ============================================================================
namespace cowsins
{
    /// <summary>
    /// 武器换弹状态：启动换弹流程，管理换弹期间的背包切换与瞄准，换弹结束后切回默认状态。
    /// </summary>
    public class WeaponReloadState : WeaponBaseState
    {
        private WeaponController controller; // 武器控制器引用
        // 玩家控制接口引用
        private IPlayerControlProvider playerControl; // IPlayerControlProvider is implemented in PlayerControl.cs
        private InputManager inputManager; // 输入管理器引用

        // 构造函数：从上下文中缓存武器控制器、玩家控制与输入引用。
        public WeaponReloadState(WeaponStates currentContext, WeaponStateFactory playerStateFactory)
            : base(currentContext, playerStateFactory)
        {
            controller = _ctx.WeaponController;
            playerControl = _ctx.PlayerControlProvider;
            inputManager = _ctx.Dependencies.InputManager;
        }

        // 进入换弹状态：启动换弹行为。
        public sealed override void EnterState()
        {
            controller.reloadBehaviour.StartReload();
        }

        // 每帧更新：若配置允许取消换弹则处理背包切换；随后检查状态切换与瞄准。
        public sealed override void UpdateState()
        {
            // Allow inventory switching if configured
            // 若武器存在且允许取消换弹，则允许在换弹期间切换武器
            if (controller.Weapon != null && controller.Weapon.allowCancelReload)
                controller.weaponInventoryBehaviour.HandleInventory();

            CheckSwitchState();
            if (!playerControl.IsControllable) return; // 玩家不可控时跳过瞄准处理
            CheckStopAim();
        }

        // 物理更新：换弹状态无需物理逻辑，留空。
        public sealed override void FixedUpdateState()
        {
        }

        // 退出换弹状态：无需额外清理，留空。
        public sealed override void ExitState() { }

        // 检查状态切换：换弹完成后切回默认状态。
        public sealed override void CheckSwitchState()
        {
            if (!controller.IsReloading) SwitchState(_factory.Default());
        }
        // 检查是否停止瞄准：未按瞄准键或换弹期间不允许瞄准时，退出瞄准。
        private void CheckStopAim()
        {
            if (!inputManager.Aiming || !controller.Weapon.allowAimingIfReloading) controller.aimBehaviour?.Exit();
        }
    }
}