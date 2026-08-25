// ============================================================================
// WeaponShootingState.cs —— 武器射击状态。
// 当玩家正在射击时进入。该状态支持多种射击方式（Press、PressAndHold、
// HoldAndRelease、HoldUntilReady），通过 UnityEvent 动态绑定对应的射击逻辑，
// 并在射击期间处理瞄准、弹药检查与换弹切换。
// ============================================================================
using UnityEngine;
using UnityEngine.Events;

namespace cowsins
{
    /// <summary>
    /// 武器射击状态：根据武器射击方式执行开火，管理射击期间的瞄准与换弹切换，
    /// 并在满足条件时切回默认状态。
    /// </summary>
    public class WeaponShootingState : WeaponBaseState
    {
        // Weapon FSM ( Finite State Machine ) is dependant on WeaponController.
        private WeaponController controller; // 武器控制器引用
        // 玩家移动状态接口引用，用于读取冲刺状态
        private IPlayerMovementStateProvider movement; // Reference to PlayerMovement.cs ( IPlayerMovementStateProvider is implemented in PlayerMovement.cs )
        // 玩家控制接口引用，用于判断玩家是否可控
        private IPlayerControlProvider playerControl; // Reference to PlayerControl.cs ( IPlayerControlProvider is implemented in PlayerControl.cs )
        private InputManager inputManager; // 输入管理器引用

        private Weapon_SO currentWeapon; // 当前正在使用的武器数据（ScriptableObject）
        private UnityEvent shootAction; // 射击动作事件：根据射击方式动态绑定具体射击方法

        // Control properties
        private bool isPressToShoot; // 是否为「按下即射一次」的射击方式（Press）
        private bool isHoldAndRelease; // 是否为「按住蓄力、松开射击」的射击方式（HoldAndRelease）
        private bool shootsOnStopFire; // 是否在松开射击键时切换回默认状态

        private float holdProgress; // 蓄力/蓄能进度（0~100+），用于 HoldAndRelease 与 HoldUntilReady

        // 构造函数：从上下文中缓存控制器、移动、控制、输入引用，并初始化射击事件。
        public WeaponShootingState(WeaponStates currentContext, WeaponStateFactory playerStateFactory)
            : base(currentContext, playerStateFactory)
        {
            controller = _ctx.WeaponController;
            movement = _ctx.PlayerMovement;
            playerControl = _ctx.PlayerControlProvider;
            inputManager = _ctx.Dependencies.InputManager;

            shootAction = new UnityEvent();
        }

        // 进入射击状态：重置蓄力进度，记录当前武器与射击方式标志，
        // 更新射击事件监听，并订阅取消射击/收枪事件。
        public override void EnterState() 
        {
            // Reset hold progress when we start to shoot
            holdProgress = 0f; // 开始射击时重置蓄力进度
            currentWeapon = controller.Weapon;

            isPressToShoot = currentWeapon.shootMethod == ShootingMethod.Press;
            isHoldAndRelease = currentWeapon.shootMethod == ShootingMethod.HoldAndRelease;
            shootsOnStopFire = isPressToShoot || isHoldAndRelease;

            UpdateShootEventListeners(); // 根据射击方式绑定对应的射击方法

            controller.settings.userEvents.OnUnholster.AddListener(UpdateShootEventListeners); // 收枪后重新绑定监听
            if (!isPressToShoot) inputManager.OnStopShoot += SwitchToDefault; // 非 Press 方式松开射击键即退出

            controller.IsShooting = true; // 标记玩家正在射击
        }

        // 每帧更新：若能射击则触发射击事件，处理瞄准，并检查状态切换。
        public override void UpdateState()
        {
            if (CanShoot()) shootAction?.Invoke();
            HandleAiming();
            CheckSwitchState();
        }

        public override void FixedUpdateState(){}

        // 退出射击状态：移除事件监听；若为蓄力型且在松开时进度已满则补一枪；
        // 取消射击输入订阅，并清除射击标记。
        public override void ExitState()
        {
            controller.settings.userEvents.OnSelectWeapon.RemoveListener(UpdateShootEventListeners);

            // 蓄力型（HoldAndRelease）在退出时若蓄力已满，则执行射击
            if (isHoldAndRelease && holdProgress > 100f)
                controller.shootBehaviour.Shoot();

            if (!isPressToShoot)
                inputManager.OnStopShoot -= SwitchToDefault;

            controller.IsShooting = false;
        }

        // 检查状态切换：Press 或 HoldAndRelease 松开即切回默认；玩家不可控时切回默认；随后检查换弹。
        public override void CheckSwitchState()
        {
            if (shootsOnStopFire && !isHoldAndRelease || !playerControl.IsControllable)
                SwitchToDefault();

            CheckReload();
        }

        private void SwitchToDefault() => SwitchState(_factory.Default());

        // 判断能否射击：上下文允许射击，且有弹药（近战武器无弹药要求），
        // 且移动状态允许射击（冲刺中只有允许边冲边射时才可射击）。
        private bool CanShoot()
        {
            return controller.weaponContext.CanShoot &&
                    (controller.Id.bulletsLeftInMagazine > 0 || controller.Weapon.shootStyle == ShootStyle.Melee) // Melee weapons dont use bullets 
                    && (movement.CanShootWhileDashing && movement.IsDashing || !movement.IsDashing);
        }

        // 检查是否需要换弹：普通换弹模式下按条件切换；热量型模式下热量满时切换。
        private void CheckReload()
        {
            if (controller.Weapon != null && controller.Weapon.reloadStyle == ReloadingStyle.defaultReload)
            {
                if (CanReload())
                    SwitchState(_factory.Reload());
            }
            else if (controller.Id != null && controller.Id.heatRatio >= 1)
            {
                SwitchState(_factory.Reload());
            }
        }
        // 判断能否换弹：手动换弹或自动换弹（弹匣打空），且非近战武器且有备弹。
        private bool CanReload()
        {
            bool outOfAmmo = controller.Id.bulletsLeftInMagazine <= 0; // 弹匣是否打空
            bool needsReload = controller.Id.bulletsLeftInMagazine < controller.Id.magazineSize; // 弹匣是否未满
            bool hasAmmo = controller.Id.totalBullets > 0; // 是否有备用弹药
            bool isNotMelee = (int)controller.Weapon.shootStyle != 2; // 是否非近战武器

            return (inputManager.Reloading && isNotMelee && needsReload && hasAmmo) ||
                   (outOfAmmo && controller.settings.autoReload && isNotMelee && hasAmmo);
        }

        // 处理瞄准：按住瞄准键且武器允许瞄准时执行瞄准 Tick，否则退出瞄准。
        private void HandleAiming()
        {
            if (inputManager.Aiming && controller.Weapon.allowAim) controller.aimBehaviour?.Tick();
            CheckStopAim();
        }

        // 松开瞄准键时退出瞄准。
        private void CheckStopAim() { if (!inputManager.Aiming) controller.aimBehaviour?.Exit(); }

        // Subscribe to different ways of handling shpooting based on the shooting method
        // 根据射击方式订阅不同的射击处理方法。
        private void UpdateShootEventListeners()
        {
            // Ensure shootAction is clean
            shootAction.RemoveAllListeners(); // 清空已有监听，避免重复绑定

            switch (controller.Weapon?.shootMethod)
            {
                case ShootingMethod.Press: shootAction.AddListener(PressShoot); break; // 按下即射
                case ShootingMethod.PressAndHold: shootAction.AddListener(PressHoldShoot); break; // 按住连射
                case ShootingMethod.HoldAndRelease: shootAction.AddListener(HoldAndReleaseShoot); break; // 按住蓄力松开射击
                case ShootingMethod.HoldUntilReady: shootAction.AddListener(HoldUntilReadyShoot); break; // 按住蓄力至就绪自动射击
            }
        }

        // 按下即射（Press）：仅当未处于 holding 状态时射击一次，避免重复触发。
        private void PressShoot()
        {
            if (!_ctx.holding)
            {
                _ctx.holding = true;
                controller.shootBehaviour.Shoot();
            }
        }
        // 按住连射（PressAndHold）：每帧直接射击。
        private void PressHoldShoot()
        {
            controller.shootBehaviour.Shoot();
        }
        // 按住蓄力（HoldAndRelease）：蓄力进度未满时按速度累积。
        private void HoldAndReleaseShoot()
        {
            if(holdProgress < 100 ) holdProgress += Time.deltaTime * controller.Weapon.holdProgressSpeed;
        }
        // 蓄力至就绪自动射击（HoldUntilReady）：蓄力满后射击一次并重置进度。
        private void HoldUntilReadyShoot()
        {
            holdProgress += Time.deltaTime * controller.Weapon.holdProgressSpeed;

            if (holdProgress > 100)
            {
                controller.shootBehaviour.Shoot();
                holdProgress = 0;
            }
        }
    }
}