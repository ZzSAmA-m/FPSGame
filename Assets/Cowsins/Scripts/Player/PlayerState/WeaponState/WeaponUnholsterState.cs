// ============================================================================
// WeaponUnholsterState.cs —— 掏出（收枪后再拿出）武器状态。
// 当武器从收起状态重新拿出时进入。进入后记录掏出动画时长并开始计时，动画
// 结束后切回默认状态；同时支持在掏出过程中（若配置允许）直接切换到换弹状态。
// ============================================================================
using UnityEngine;

namespace cowsins
{
    // Called when a weapon is unholstered.
    /// <summary>
    /// 掏出武器状态：播放/等待掏出武器的时长，结束后切回默认状态，期间可选地切换换弹。
    /// </summary>
    public class WeaponUnholsterState : WeaponBaseState
    {
        private float timer; // 计时器：累计掏出过程经过的时间
        private float duration; // 掏出动画总时长
        private bool isDefaultReload; // 当前武器是否为普通换弹模式（defaultReload）

        // 玩家控制接口引用
        private IPlayerControlProvider playerControl; // Reference to PlayerControl.cs ( IPlayerControlProvider is implemented in PlayerControl.cs )
        // Weapon FSM ( Finite State Machine ) is dependant on WeaponController.
        private WeaponController controller; // 武器控制器引用
        private InputManager inputManager; // 输入管理器引用

        // 构造函数：从上下文中缓存玩家控制、武器控制器与输入引用。
        public WeaponUnholsterState(WeaponStates currentContext, WeaponStateFactory playerStateFactory)
            : base(currentContext, playerStateFactory) 
        {
            playerControl = _ctx.PlayerControlProvider;
            controller = _ctx.WeaponController;
            inputManager = _ctx.Dependencies.InputManager;
        }

        // 进入掏出状态：重置计时，记录换弹模式与掏出时长
        // （若武器覆盖了掏出时间则用武器值，否则用默认 0.5 秒）。
        public sealed override void EnterState()
        {
            // Reset timer to ensure the timing is tracked accordingly.
            timer = 0; // 重置计时，确保时长被正确跟踪
            isDefaultReload = controller.Weapon.reloadStyle == ReloadingStyle.defaultReload;
            duration = controller.Weapon.overrideUnholsterTime ? controller.Weapon.unholsterTime : .5f;
        }

        // 每帧更新：累计计时并检查状态切换；玩家可控时处理背包切换，不可控时退出瞄准。
        public sealed override void UpdateState()
        {
            if (timer < duration) timer += Time.deltaTime;
            CheckSwitchState();

            if (!playerControl.IsControllable)
            {
                controller.aimBehaviour?.Exit();
                return;
            }
            controller.weaponInventoryBehaviour.HandleInventory();
        }

        public sealed override void FixedUpdateState() { }

        public sealed override void ExitState() {}

        // 检查状态切换：掏出动画结束则切回默认状态；
        // 若允许在掏出时换弹且满足换弹条件，则切换到换弹状态。
        public sealed override void CheckSwitchState()
        {
            if (timer >= duration) SwitchState(_factory.Default());

            if (controller.settings.allowReloadWhileUnholstering && isDefaultReload && CanSwitchToReload(controller))
                SwitchState(_factory.Reload());
        }

        // 判断能否换弹：手动换弹或自动换弹（弹匣打空），且非近战武器且有备弹。
        private bool CanSwitchToReload(WeaponController controller)
        {
            return inputManager.Reloading && (int)controller.Weapon.shootStyle != 2 && controller.Id.bulletsLeftInMagazine < controller.Id.magazineSize && controller.Id.totalBullets > 0
                        || controller.Id.bulletsLeftInMagazine <= 0 && controller.settings.autoReload && (int)controller.Weapon.shootStyle != 2 && controller.Id.bulletsLeftInMagazine < controller.Id.magazineSize && controller.Id.totalBullets > 0;
        }
    }
}