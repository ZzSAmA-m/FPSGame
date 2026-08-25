// ============================================================================
// WeaponDefaultState.cs —— 武器的默认（待机）状态。
// 当玩家手持武器但未进行射击、换弹、近战、检视等动作时，武器处于该状态。
// 该状态负责监听输入，在满足条件时切换到射击、换弹、检视或近战状态，
// 同时处理瞄准、背包切换、空弹匣音效等默认行为。
// ============================================================================
using UnityEngine;

namespace cowsins
{
    /// <summary>
    /// 武器默认状态：处理待机时的输入监听、瞄准、背包切换，
    /// 并根据输入与弹药情况决定切换到射击/换弹/检视/近战状态。
    /// </summary>
    public class WeaponDefaultState : WeaponBaseState
    {
        private WeaponController weaponController; // 武器控制器引用
        // 玩家控制接口引用，用于判断玩家是否可控
        private IPlayerControlProvider playerControl; // IPlayerControlProvider is implemented in PlayerControl.cs
        // 玩家移动状态接口引用，用于读取攀爬/冲刺等移动状态
        private IPlayerMovementStateProvider movement; // IPlayerMovementStateProvider is implemented in PlayerMovement.cs
        private InteractManager interact; // 交互管理器引用，用于判断能否检视武器
        private InputManager inputManager; // 输入管理器引用，用于读取玩家输入

        private bool holdingEmpty = false; // 标记是否正在「按住扳机但弹匣为空」，用于只播放一次空仓音效

        // 构造函数：从上下文中缓存武器控制器、玩家控制、交互、移动与输入等引用。
        public WeaponDefaultState(WeaponStates currentContext, WeaponStateFactory playerStateFactory)
            : base(currentContext, playerStateFactory) 
        {
            weaponController = _ctx.WeaponController;
            playerControl = _ctx.PlayerControlProvider;
            interact = _ctx.InteractManager;
            movement = _ctx.PlayerMovement;
            inputManager = _ctx.Dependencies.InputManager;
        }

        // 进入默认状态：重置空仓标记，订阅检视、近战、手电筒等输入事件。
        public override void EnterState()
        {
            holdingEmpty = false;
            inputManager.OnInspect += SwitchToInspect;
            inputManager.OnMelee += SwitchToMelee;

            // 若存在配件系统，则把「切换手电筒」输入绑定到配件系统的手电筒开关上
            if(weaponController.attachmentsSystem != null)
                inputManager.OnToggleFlashlight += weaponController.attachmentsSystem.ToggleFlashLight;
        }

        // 每帧更新：玩家不可控时退出瞄准并返回；否则处理背包切换，
        // 并在持有武器且未攀爬时检查状态切换与瞄准。
        public override void UpdateState()
        {
            // 玩家不存在或不可控时，退出瞄准并直接返回
            if (playerControl == null || !playerControl.IsControllable)
            {
                weaponController.aimBehaviour?.Exit();
                return;
            }

            weaponController.weaponInventoryBehaviour.HandleInventory(); // 处理武器栏/背包切换逻辑

            if (weaponController.Weapon == null || movement.IsClimbing) return; // 无武器或正在攀爬时跳过后续逻辑

            CheckSwitchState();
            CheckAim();
        }

        public override void FixedUpdateState() {}

        // 退出默认状态：取消订阅检视、近战、手电筒等输入事件。
        public override void ExitState()
        {
            inputManager.OnInspect -= SwitchToInspect;
            inputManager.OnMelee -= SwitchToMelee;
            inputManager.OnToggleFlashlight -= weaponController.attachmentsSystem.ToggleFlashLight;
        }
        // 检查状态切换：处理射击与换弹的切换条件。
        public override void CheckSwitchState()
        {
            // 玩家按住射击键时
            if (inputManager.Shooting)
            {
                // 弹匣为空且未在持续空仓状态时，播放一次空仓音效
                if (weaponController.Weapon.audioSFX.emptyMagShoot != null && weaponController.Id.bulletsLeftInMagazine <= 0 && !holdingEmpty)
                {
                    SoundManager.Instance.PlaySound(weaponController.Weapon.audioSFX.emptyMagShoot, 0, .15f, true);
                    holdingEmpty = true;
                }
                // 否则若能射击，则切换到射击状态
                else if (CanSwitchToShoot(weaponController)) 
                    SwitchState(_factory.Shoot());
            }
            else holdingEmpty = false; // 松开射击键时清除空仓标记

            if (weaponController.Weapon.infiniteBullets) return; // 无限弹药武器无需换弹

            // 普通换弹模式：满足条件则切换换弹状态
            if (weaponController.Weapon.reloadStyle == ReloadingStyle.defaultReload)
            {
                if (CanSwitchToReload(weaponController))
                    SwitchState(_factory.Reload());
            }
            else
            {
                // 热量型换弹模式：当热量比例达到 1 时自动切换换弹状态
                if (weaponController.Id.heatRatio >= 1) SwitchState(_factory.Reload());
            }

        }

        // 检查瞄准：按住瞄准键且武器允许瞄准时持续执行瞄准 Tick，否则退出瞄准。
        private void CheckAim()
        {
            if (inputManager.Aiming && weaponController.Weapon.allowAim) weaponController.aimBehaviour?.Tick();
            CheckStopAim();
        }

        // 松开瞄准键时退出瞄准状态。
        private void CheckStopAim() { if (!inputManager.Aiming) weaponController.aimBehaviour?.Exit(); }

        // 判断能否切换到射击状态：非「按住即射」且未处于 holding 状态，同时弹匣内还有子弹。
        private bool CanSwitchToShoot(WeaponController controller)
        {
            return !(_ctx.holding && controller.Weapon.shootMethod == ShootingMethod.Press) && controller.Id.bulletsLeftInMagazine > 0;
        }

        // 判断能否切换到换弹状态：手动按换弹键且弹匣未满且有备弹；
        // 或者弹匣打空且开启自动换弹（且均不是近战武器 shootStyle != 2）。
        private bool CanSwitchToReload(WeaponController controller)
        {
            return inputManager.Reloading && (int)controller.Weapon.shootStyle != 2 && controller.Id.bulletsLeftInMagazine < controller.Id.magazineSize && controller.Id.totalBullets > 0
                        || controller.Id.bulletsLeftInMagazine <= 0 && controller.settings.autoReload && (int)controller.Weapon.shootStyle != 2 && controller.Id.bulletsLeftInMagazine < controller.Id.magazineSize && controller.Id.totalBullets > 0;
        }

        // 切换到检视状态：仅在玩家可控、持有武器且允许检视时执行。
        private void SwitchToInspect()
        {
            if (playerControl.IsControllable && weaponController.Weapon && interact.CanInspect) SwitchState(_factory.Inspect());
        }

        // 切换到近战状态：仅当近战行为可执行时执行。
        private void SwitchToMelee()
        {
            if (weaponController.quickActionBehaviour.CanExecute()) SwitchState(_factory.Melee());
        }
    }
}