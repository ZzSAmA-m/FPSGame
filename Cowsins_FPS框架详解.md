# Cowsins FPS Engine 框架详解

> 本文件是 `Assets/Cowsins` 目录下 **cowsins™ FPS Engine**（第一人称射击控制器框架）的完整技术详解。
> 面向想要理解、扩展或二次开发该框架的开发者。所有类名、方法名、字段名、事件名均来自源码真实标识符。
> 项目：`FPSGame`（Unity 6000.5.0f1），框架命名空间统一为 `cowsins`（个别 Editor 类位于 `cowsins.Inspector` / `cowsins.Settings`）。

---

## 目录

1. [框架总览](#1-框架总览)
2. [核心架构与装配机制](#2-核心架构与装配机制)
3. [数据模型（ScriptableObject 体系）](#3-数据模型scriptableobject-体系)
4. [输入系统](#4-输入系统-inputmanager)
5. [移动系统](#5-移动系统)
6. [玩家系统与数值](#6-玩家系统与数值)
7. [状态机（玩家 + 武器）](#7-状态机玩家--武器)
8. [武器系统](#8-武器系统)
9. [拾取与交互系统](#9-拾取与交互系统)
10. [UI 系统](#10-ui-系统)
11. [管理器](#11-管理器-managers)
12. [敌人系统](#12-敌人系统)
13. [环境物与交互物](#13-环境物与交互物-extra)
14. [特效系统](#14-特效系统-effects)
15. [编辑器工具与自定义特性](#15-编辑器工具与自定义特性)
16. [扩展指南](#16-扩展指南)
17. [注意事项与已知问题](#17-注意事项与已知问题)

---

## 1. 框架总览

**cowsins FPS Engine** 是一个数据驱动、组件化、事件解耦的第一人称射击控制器框架，覆盖移动、武器、状态机、拾取、UI、敌人、特效、编辑器工具等完整体系。

### 1.1 目录结构（`Assets/Cowsins`）

| 目录 | 内容 |
|---|---|
| `Scripts/` | 全部 C# 代码（205 个 .cs） |
| `Scripts/Movement` + `Scripts/Behaviours/Movement Behaviours` | 移动中枢 + 14 个移动行为 |
| `Scripts/Weapons` + `Scripts/Behaviours/Weapon Behaviours` | 武器中枢 + 射击风格 + 附件 + 11 个武器行为 |
| `Scripts/Player` | 玩家核心、统计、双状态机 |
| `Scripts/Managers` | 8 个全局管理器 |
| `Scripts/PickUpSystem` | 拾取与交互 |
| `Scripts/UI` | HUD 与菜单 UI |
| `Scripts/Enemies` | 敌人与炮塔 |
| `Scripts/Extra` | 环境物、设置系统、通用工具 |
| `Scripts/Effects` / `Camera` | 相机、武器、程序化特效 |
| `Scripts/Editor` / `CustomAttributes` | 编辑器工具与自定义特性 |
| `Prefabs/`、`ScriptableObjects/`、`Animations/`、`Models/`、`SFX/`、`VFX/`、`UI/` 等 | 资产 |

### 1.2 核心架构全景

```
                         ┌──────────────────────────────────────────────┐
                         │   PlayerDependencies（服务定位器 / DI 容器）      │
                         │   Awake() 用 GetRequiredComponent<T>() 解析接口   │
                         └──────┬───────────────────────────────────────┘
        ┌──────────┬───────────┼──────────────┬───────────────┬─────────┐
        ▼          ▼           ▼              ▼               ▼         ▼
  PlayerMovement  PlayerStats  PlayerControl  WeaponController InteractManager ...
   (IPlayerMovement (IDamageable  (IPlayerControl (IWeaponReference (IInteractManager
    StateProvider)   IPlayerStats  Provider)      Provider)         Provider)
    + 14 behaviour    Provider)                   + 11 behaviour)
```

**关键设计**（全文贯穿）：
- **服务定位器 + 接口隔离**：`PlayerDependencies` 把所有组件以窄接口（Provider）暴露，其他系统面向接口编程。
- **组合优于继承**：`PlayerMovement` / `WeaponController` 是"组合根"，用 `Context Object`（`MovementContext` / `WeaponContext`）装配一批**纯 C# 类**（`*Behaviour` / `*System`），把具体逻辑委托出去。
- **状态机编排**：玩家 FSM（6 状态）与武器 FSM（7 状态）负责"在什么状态调用哪些行为"。
- **事件驱动双轨**：各系统内部 `UnityEvent` 总线（`PlayerMovementEvents` / `WeaponControllerEvents` / `PlayerStatsEvents`）+ 全局静态 `UIEvents`（`Action`）。
- **ScriptableObject 数据驱动**：武器/物品/配件/程序化射击都用 SO 承载数据。

### 1.3 设计模式一览

| 模式 | 出现位置 |
|---|---|
| 服务定位器 + 依赖注入 | `PlayerDependencies` + `Initialize(PlayerDependencies)` |
| 接口隔离（ISP）/ 依赖倒置（DIP） | 15+ 个 `I*Provider` 接口 |
| 组合（Composition） | 14 个移动 behaviour + 11 个武器 behaviour/system |
| 上下文对象（Context Object） | `MovementContext`、`WeaponContext` |
| 状态模式 + 工厂 | `PlayerStates`/`WeaponStates` + `*StateFactory` |
| 策略模式 | `IShootStyle`、`IAttachmentLayoutStrategy`、`Sway` 委托 |
| 观察者/事件总线 | `*Events`（UnityEvent）+ `UIEvents`（Action） |
| 对象池 | `PoolManager`（prefab 为 key） |
| 数据驱动 | `Weapon_SO`、`Item_SO`、`AttachmentIdentifier_SO`、`ProceduralShot_SO` |
| 惰性求值 + 脏标记 | `ModifiableStat` |

---

## 2. 核心架构与装配机制

### 2.1 `PlayerDependencies`（装配中枢）

文件：`Scripts/Player/PlayerDependencies.cs`。挂在玩家根物体上，是**整个玩家系统的服务定位器**。

- 序列化字段（Inspector 拖拽）：`inputManager`、`cameraFOVManager`、`cameraEffects`、`weaponEffects`、`uiController`、`crosshair`、`uIEffects`。
- `Awake()` 用 `GetRequiredComponent<T>()`（`GetComponent<T>()`，缺失仅打日志不抛异常）解析并缓存全部接口，随后对各管理器调用 `Initialize(this)`：`inputManager.SetPlayer(this)`、`cameraFOVManager.Initialize(this)`、`cameraEffects.Initialize(this)`、`weaponEffects.Initialize(this)`、`uiController.Initialize(this)`、`uIEffects.Initialize(this)`。

**暴露的 Provider 接口**（实现方 → 接口）：

| 属性 | 接口 | 实现方 |
|---|---|---|
| `InputManager` | `InputManager` | 序列化引用 |
| `CameraFOVManager` / `Crosshair` | 具体类 | 序列化引用 |
| `PlayerMovementEvents` | `IPlayerMovementEventsProvider` | PlayerMovement |
| `PlayerMovementState` | `IPlayerMovementStateProvider` | PlayerMovement |
| `WeaponReference` | `IWeaponReferenceProvider` | WeaponController |
| `WeaponBehaviour` | `IWeaponBehaviourProvider` | WeaponController |
| `WeaponRecoil` | `IWeaponRecoilProvider` | WeaponController |
| `WeaponEvents` | `IWeaponEventsProvider` | WeaponController |
| `Damageable` | `IDamageable` | PlayerStats |
| `PlayerStats` | `IPlayerStatsProvider` | PlayerStats |
| `PlayerStatsEvents` | `IPlayerStatsEventsProvider` | PlayerStats |
| `FallHeight` | `IFallHeightProvider` | PlayerStats |
| `InteractManager` | `IInteractManagerProvider` | InteractManager |
| `InteractEvents` | `IInteractEventsProvider` | InteractManager |
| `PlayerControl` | `IPlayerControlProvider` | PlayerControl |
| `PlayerMultipliers` | `IPlayerMultipliers` | PlayerMultipliers |

### 2.2 Provider 接口清单（依赖倒置的关键契约）

- **移动**：`IPlayerMovementStateProvider`（`Orientation`/`IsIdle`/`CurrentSpeed`/`RunSpeed`/`WalkSpeed`/`CrouchSpeed`/`Grounded`/`IsCrouching`/`IsSliding`/`IsClimbing`/`IsWallRunning`/`IsDashing`/`CanShootWhileDashing`/`DamageProtectionWhileDashing`/`NormalFOV`/`WallRunningFOV`/`RunningFOV`/`FadeFOVAmount`）+ `IPlayerMovementEventsProvider`。
- **武器**：`IWeaponReferenceProvider`（`MainCamera`/`Weapon`/`Id`/`CurrentWeaponIndex`/`Inventory`）、`IWeaponBehaviourProvider`（`IsAiming`/`IsReloading`/`IsShooting`/`AlternateAiming`/`IsMeleeAvailable`/`AimingCamShakeMultiplier`/`CrouchingCamShakeMultiplier`/`AddDuplicateWeaponAmmo`/`TryToAddWeapons`/`SwapWeapons`）、`IWeaponRecoilProvider`（`RecoilPitchOffset`/`RecoilYawOffset`）、`IWeaponEventsProvider`。
- **玩家**：`IPlayerStatsProvider`、`IPlayerStatsEventsProvider`、`IFallHeightProvider`、`IPlayerControlProvider`、`IPlayerMultipliers`。
- **交互**：`IInteractManagerProvider`、`IInteractEventsProvider`。

### 2.3 事件总线（双轨）

1. **系统内部 `UnityEvent`**：`PlayerMovementEvents`、`WeaponControllerEvents`、`PlayerStatsEvents`、`WeaponControllerSettings.Events`（用户侧 Inspector 可绑）、`PlayerMovementSettings.events`（用户侧）。所有订阅在 `EnterState`/`Initialize`/`OnEnable` 建立、`ExitState`/`OnDestroy`/`OnDisable` 成对移除。
2. **全局静态 `UIEvents`**（`Scripts/UI/UIEvents.cs`，全 `Action`）：`onCoinsChange(int,bool)`、`onExperienceCollected(bool)`、`onEnemyKilled(string)`、`onEnemyHit(bool,bool,Vector3,float)`、`onEnableAttachmentUI(GameObject)`、`onAttachmentUIElementClicked(Attachment,bool)`、`onAttachmentUIElementClickedNewAttachment(Attachment)`。

---

## 3. 数据模型（ScriptableObject 体系）

### 3.1 继承链

```
ScriptableObject
├── Item_SO            （物品基类：名称/图标/掉落视觉/重量/背包格子）
│   ├── Weapon_SO      （武器完整数据）
│   └── BulletTypeIdentifier_SO（弹药类型标识）
├── AttachmentIdentifier_SO （配件标识：类型 + 百分比修正字段）
└── ProceduralShot_SO  （程序化射击曲线）
```

### 3.2 `Item_SO`（`Scripts/PickUpSystem/Item_SO.cs`）

- 字段：`_name`、`description`、`icon`、`pickUpGraphics`（掉落视觉）、`maxStack`[1,99]、`itemSize`（`Vector2Int` 背包格子）、`irregularItemIcon`、`applyWeight`、`weightMultiplier`[0.2,1]。
- `#if INVENTORY_PRO_ADD_ON` 下提供 `Use(InventoryProManager, InventorySlot)` 供 Inventory Pro 附加包调用。

### 3.3 `Weapon_SO`（`Scripts/Weapons/Weapon_SO.cs`，约 1210 行，核心数据类）

- `[CreateAssetMenu(fileName = "NewWeapon", menuName = "COWSINS/New Weapon")]`，继承 `Item_SO`。
- 三大枚举：
  - `ShootStyle { Hitscan, Projectile, Melee, Custom }`
  - `ShootingMethod { Press, PressAndHold, HoldAndRelease, HoldUntilReady }`
  - `ReloadingStyle { defaultReload, Overheat }`

**字段分组**（真实字段名）：

| 分组 | 关键字段 |
|---|---|
| 基本信息 | `weaponObject`（`WeaponIdentification`，第一人称武器预制体）、`shootStyle`、`shootMethod`、`reloadStyle`、`bulletTypeIdentifier`、继承自 Item_SO 的 `_name/icon/pickUpGraphics/itemSize/weightMultiplier/applyWeight` |
| 射击 | `holdProgressSpeed`、`continuousFire`、`fireRate`、`bulletsPerFire`[1,8]、`timeBetweenShots`、`ammoCostPerFire`、`bulletRange`、`allowAim`、`allowCancelReload` |
| 散布/后坐力 | `applyBulletSpread`、`spreadAmount`、`aimSpreadAmount`、`applyRecoil`、`recoilX/recoilY`（AnimationCurve）、`xRecoilAmount/yRecoilAmount`、`recoilRelaxSpeed`、`applyDifferentRecoilOnAiming`、`xRecoilAmountOnAiming/yRecoilAmountOnAiming` |
| 弹药/换弹 | `infiniteBullets`、`magazineSize`、`limitedMagazines`、`totalMagazines`、`reloadTime`、`emptyReloadTime`、`overrideUnholsterTime`、`unholsterTime`、`coolSpeed`、`cooledPercentageAfterOverheat`、`dontShowMagazine` |
| 伤害/穿透 | `damagePerBullet`、`criticalDamageMultiplier`[1,3]、`applyDamageReductionBasedOnDistance`、`minimumDistanceToApplyDamageReduction`、`damageReductionMultiplier`、`penetrationAmount`[0,10]、`penetrationDamageReduction` |
| 瞄准 | `ignoreAimPoint`、`aimDistance`、`aimingRotation`、`aimingSpeed`、`aimingFOV`、`allowAimingIfReloading`、`movementSpeedWhileAiming`、`setMovementSpeedWhileAiming` |
| 音效 | `audioSFX`（`AudioClips`：`shooting[]`/`holster`/`unholster`/`reload`/`emptyMagReload`/`emptyMagShoot`）、`pitchVariationFiringSFX` |
| 视觉/粒子 | `bulletHoleImpact`（`BulletHoleImpact`）、`muzzleVFX`、`bulletGraphics`、`showBulletShells`、`useProceduralShot`、`proceduralShotPattern`、`grapplesFromTip`、`bulletTrail`、`explosionVFX` |
| 相机反馈 | `camShakeAmount`、`camShakeAimMultiplier`、`camShakeCrouchMultiplier`、`applyFOVEffectOnShooting`、`FOVValueToSubtract`、`AimingFOVValueToSubtract`、`amountOfShootAnimations` |
| 投射物专属 | `projectile`、`projectileUsesGravity`、`shootDelay`、`bulletDuration`、`speed`、`explosionOnHit`、`hurtsPlayer`、`explosionRadius`、`explosionForce` |
| 近战专属 | `damagePerHit`、`attackRate`、`hitDelay`、`hitDelays[]`、`attackRange` |
| 其他 | `crosshairResize`、`crosshairParts`（`CrosshairParts`）、`locomotionTransitions`（`LocomotionTransitionOverrides`） |

### 3.4 `WeaponIdentification`（运行时武器实例）

挂在武器预制体上，是 `Weapon_SO` 数据的**运行时可变副本**：
- **无显式 string ID/GUID**，唯一性由「`weapon`（SO 引用）+ 库存槽位索引 + 组件实例」共同确立。
- 关键字段：`weapon`、`FirePoint[]`（枪口）、`shellEjectPoint`、`aimPoint`、`headBone`、`defaultAttachments`、`compatibleAttachments`、`attachmentStateManager`、运行时弹药（`bulletsLeftInMagazine`/`magazineSize`/`totalBullets`/`heatRatio`）。
- `Awake()` 从 SO **拷贝**运行时副本：`damage`/`fireRate`/`baseSpread`/`baseAimSpread`/`aimSpeed`/`reloadTime`/`emptyReloadTime`/`weightMultiplier`/`camShakeAmount`/`penetrationAmount` 等（附件可再修改这些副本）。
- 关键方法：`SetShootStyle(IShootStyle)`、`Shoot(spread, damageMultiplier, shakeMultiplier)`、`ReduceAmmo()`（按 reloadStyle 绑定 `ReduceDefaultAmmo` / `ReduceOverheatAmmo`）、`PickNextShot()`、`GetCurrentShotAnimation()`、`GetFireSFX()`。

### 3.5 数值修改器系统（`Player/Stats/`）

三个文件构成通用"可修改数值"：

- `StatModifierType { Additive, Multiplicative }`
- `StatModifier { readonly float Value; readonly StatModifierType Type; readonly object Source; }`（不可变，`Source` 用于精确移除）
- `ModifiableStat`：`BaseValue`、`Value`（惰性求值 + 脏标记 `_isDirty` 缓存）、`AddModifier`、`RemoveModifierFromSource(source)`。

**运算规则（重要）**：`最终值 = (BaseValue + ΣAdditive) × (1 + ΣMultiplicative)`。乘算修改器彼此**先加算再统一乘**，其 `Value` 语义是"百分比增量"（0.2 = +20%）。例：Base=1，两个 Additive(0.5) + 两个 Multiplicative(0.2) → `(1+1.0)×(1+0.4)=2.8`。

`PlayerMultipliers` 持有三个 `ModifiableStat(1)`：`DamageMultiplier` / `HealMultiplier` / `WeightMultiplier`，并提供 `AddTemporaryModifier(stat, modifier, duration)`（同 Source 仅刷新到期时间）与 `RemoveModifierFromSource(source)` 的限时 buff 生命周期管理。

---

## 4. 输入系统（InputManager）

文件：`Scripts/Managers/InputManager.cs`（约 489 行）。**玩家输入的唯一封装点与广播中心**。

- **输入方案**：Unity **新输入系统**（`UnityEngine.InputSystem`），非旧 Input。资产包装类 `PlayerActions`（`Assets/Cowsins/Inputs/PlayerActions.cs`，生成器 1.19.0）。
- **非传统单例**：挂在玩家对象上，由 `PlayerDependencies` 持有；但共享 `public static PlayerActions inputActions` 与 `private static int enabledInstances` 引用计数（最后一个实例禁用时才 `inputActions.Disable()`）。
- 两个 Action Map：
  - `GameControls`：`Jumping/Dashing/Reloading/Melee/Crouching/Sprinting/Firing/Grapple/Scrolling/Aiming/Interacting/Inspect/Drop/ChangeWeapons/Pause/Movement/ToggleFlashLight/InventoryOpen/InventoryFavOpen/ToggleTipsCanvas`。
  - `UI`：`Navigate/Submit/Cancel/Point/Click/ScrollWheel/MiddleClick/RightClick/Select/NorthButton/WestButton/Back` 等。
- **对外暴露**：23 个 `[HideInInspector] public bool` 状态字段（`Jumping/Sprinting/Crouching/Dashing/Shooting/Reloading/Aiming/Melee/...`）+ 8 个 float（`X/Y/Scrolling/Mousex/Mousey/Controllerx/Controllery`）+ 约 20 个 `public Action` 事件（`OnJump/OnDash/OnShoot/OnStopShoot/OnAimPressed/OnAimReleased/OnMoveInputChanged/...`）。
- **切换/按住语义**：`HandleActionInput(ref current, ref last, pressed, useToggleMode, onPressed, onReleased)` 统一封装；`alternateSprint/alternateCrouch/alternateAiming` 分别由 `PlayerMovementSettings`/`WeaponControllerSettings` 注入。
- **按键重绑定**：`StartRebind(...)` → `PerformInteractiveRebinding`，PlayerPrefs key = `action.actionMap + action.name + bindingIndex`（存 `overridePath`），支持复合绑定逐子绑定递归、Esc 取消（`WithCancelingThrough("<Keyboard>/escape")`）、`excludeMouse`；配套 `SaveBindingOverride/LoadBindingOverride/ResetBinding/ResetAllBindings/GetBindingName`。

---

## 5. 移动系统

### 5.1 `PlayerMovement`（移动中枢）

文件：`Scripts/Movement/PlayerMovement.cs`。`[RequireComponent(typeof(Rigidbody))]`，实现 `IPlayerMovementStateProvider` + `IPlayerMovementEventsProvider`。

- 持有 `Events`（`PlayerMovementEvents`）、`playerSettings`（`PlayerMovementSettings`）、`movementContext`、14 个 `xxxBehaviour` 属性。
- `Start()`：`GetDependencies → ConfigureRigidbody → playerSettings.events.OnSpawn.Invoke → SetPlayerInputModes → InitializeBehaviours`。
- `Update()`：常驻调用 `groundDetectionBehaviour.Tick()` + `jumpBehaviour.Tick()`。
- `FixedUpdate()`：死亡提前返回；非攀爬非斜坡时 `rb.AddForce(Vector3.down * 30.19f)`；钳制 `maxSpeedAllowed`；`staminaBehaviour.Tick()`。
- `ConfigureRigidbody()`：`freezeRotation=true`、`ContinuousDynamic`、`Interpolate`、`linearDamping=0`、`angularDamping=0.05f`。
- `InitializeBehaviours()`：构造 `MovementContext` 后依次 `new` 出 14 个行为（顺序：groundDetection → basicMovement → velocityHandler → cameraLook → jump → crouchSlide → dash → wallBounce → climbLadder → wallRun → grapplingHook → stamina → footsteps → speedLines）。
- `TeleportPlayer(position, rotation, resetStamina, resetDashes)`：传送 + 复位体力/冲刺 + 切回 Default 状态。

### 5.2 `PlayerMovementSettings`（配置）

`[System.Serializable]`，分组：**Assignables / Movement / Crouch / Sliding / Jump / Camera / Aim Assist / Stamina / Air Camera Influence / Wall Run / Wall Bounce / Dash / Grapple / Climb / Footsteps / Other**。嵌套：`Events`（22 个用户侧 UnityEvent）、`Sounds`、`FootStepsSounds`；枚举 `DirectionalJumpMethod{None,InputBased,ForwardMovement}`、`CancelWallRunMethod{None,Timer}`、`DashMethod{ForwardAlways,InputBased,Free}`、`GrapplingHookMethod{Linear,Swing,Combined}`、`LadderMovementMode{WSOnly,LookBased,Combined}`。

### 5.3 `MovementContext`（共享上下文）

纯数据类：Core Refs（`Transform/Rigidbody/Capsule/Camera/WhatIsGround`）+ Dependencies（`InputManager/Settings/Dependencies`）+ Global State（`IsPlayerOnSlope/SlopeHit/HasJumped/CoyoteTimer/CoyoteJumpTime/WallLeft/EnoughStaminaToRun/EnoughStaminaToJump`）。

### 5.4 14 个移动 Behaviour（均为纯 C# 类，构造签名 `XxxBehaviour(MovementContext)`）

| Behaviour | 职责 | 关键方法 |
|---|---|---|
| `BasicMovementBehaviour` | 核心移动（摩擦/加速/钳速/斜坡/空中镜头影响/事件派发） | `Movement/CalculateMoveDirection/ApplyAirCameraInfluence/AirAccelerate/CallEvents/LimitDiagonalVelocity/FindVelRelativeToLook/GetSlopeDirection/FrictionForce` |
| `CameraLookBehaviour` | 视角 yaw/pitch/roll、灵敏度、后坐力偏移、墙跑/滑铲倾斜、辅助瞄准 | `Tick/VerticalLook/GatherSensitivityValues/CalculateCameraRoll/ApplyCameraRotation/HandleAimAssist/AimAssistHit` |
| `ClimbLadderBehaviour` | 梯子攀爬（Ladder 层） | `Enter/Tick/Exit/CanExecute/DetectTopLadder/DetectBottomLadder/ApplyForcesOnTopReached` |
| `CrouchSlideBehaviour` | 下蹲 + 滑铲（缩放、爆发、屋顶检测） | `Enter/Tick/FixedTick/Exit/CanExecute/HandleCrouch/CheckUnCrouch/AllowSliding/EndSlide`；`crouchScale=(1,0.5,1)` |
| `DashBehaviour` | 冲刺 + 次数/冷却 | `Enter(Vector2)/Tick/Exit/CanExecute/GetProperDirection/CameraBasedInput/RegainDashAfterDelay/ResetDashes` |
| `FootstepsBehaviour` | 脚步声（按 Layer），独立 AudioSource | `FootSteps/PlayFootstepSound/ResetFootsteps/CacheLayerIndices` |
| `GrapplingHookBehaviour` | 钩爪三模式 + 绳索 LineRenderer | `Enter/Tick/Exit/UpdateGrappleRenderer/ApplyGrappleForces/EnableGrapple/GrappleCooldownRoutine` |
| `GroundDetectionBehaviour` | 地面检测（CapsuleCast）、斜坡、着陆/离地、土狼 | `Tick/UpdateGroundedState/HandleLanding/HandleLeavingGround/PerformGroundCheck` |
| `JumpBehaviour` | 跳跃/多段跳/土狼/方向跳/墙跳重置 | `Enter/Tick/CanExecute/CanExecuteDoubleJump/Jump/ResetJumpCount/ResetJumpsOnWallJump` |
| `SpeedLinesBehaviour` | 高速速度线粒子 | `Tick/CanExecute/StopSpeedlines` |
| `StaminaBehaviour` | 体力消耗/再生、耗尽事件、Slider | `Tick/ConsumeStamina/ResetStamina/ConsumeSlideStamina/ConsumeDashStamina/ConsumeJumpStamina` |
| `VelocityHandlerBehaviour` | **唯一**改 `CurrentSpeed` 的行为 + 冲刺 FOV | `HandleAimStart/HandleAimStop/HandleIdle/HandleMoving/HandleSprintStart/CanSprint/SetFOVToNormal/SetFOVToRun` |
| `WallBounceBehaviour` | 蹬墙反弹 | `Enter/Tick/CanExecute` |
| `WallRunBehaviour` | 墙跑（贴墙/重力抵消/墙跳/高度限制） | `Enter/FixedTick/Exit/CanExecute/WallRun/WallRunJump/CheckWalls/CheckHeight` |

### 5.5 相机模块（`Scripts/Camera/`）

- `CameraFOVManager`（MonoBehaviour）：统一管理 FOV 目标值与平滑插值。`Initialize(PlayerDependencies)`、`Update()`（Lerp 到 `targetFOV`）、`AllowChangeFOV()`（瞄准时锁定）、`SetFOV/SetFOVToNormal/SetFOVToWallrun/SetFOVToRun/ForceAddFOV/OnAimStart/OnAimStop/SetFOVOnUnholster`。冲刺 → `VelocityHandler.SetFOVToRun`；Dash → `PlayerDashState` 调 `ForceAddFOV(-fovToAddOnDash)`；瞄准 → `weapon.aimingFOV`。
- `CameraAnimations`（MonoBehaviour）：让相机跟随目标 Transform 旋转 delta（拾取物品时视角跟随），无目标时平滑复位。`SetTarget/ResetTarget(Pickeable)`。
- `MoveCamera`（MonoBehaviour，Windows-1252 编码）：每帧 `transform.position = cameraHead.position`。

---

## 6. 玩家系统与数值

### 6.1 `PlayerStats`（生命/护盾/坠落伤害/自动治疗）

`PlayerStats : MonoBehaviour, IDamageable, IPlayerStatsProvider, IFallHeightProvider, IPlayerStatsEventsProvider`。

- 字段：`health/shield`（`[ReadOnly]`）、`maxHealth/maxShield`、`freezePlayerOnDeath`、`takesFallDamage`、`minimumHeightDifferenceToApplyDamage`、`fallDamageMultiplier`、`enableAutoHeal`、`healRate`、`healAmount`、`restartAutoHealAfterBeingDamaged`、`restartAutoHealTime`。
- 事件：`userEvents`（`OnDeath/OnDamage/OnHeal`）、`Events`（`OnInitializeHealth`/`OnHealthChanged(health,shield,isDamage)`）、`Action OnDie`（状态机订阅）。
- `Damage(float, bool isHeadshot)`：冲刺免伤检查 → `OnDamage` → **先扣护盾再扣生命** → `OnHealthChanged` → 死亡判定 `Die()` → 可选重启自动治疗。
- `Heal(float)`：受 `HealMultiplier` 影响，先补生命再补护盾；`HealFull()`、`IsFullyHealed()`、`Respawn(pos)`、`OverrideHealth(...)`（供存档）。
- 坠落伤害：`ManageFallDamage()` 记录离地最高点，落地高度差 > 阈值则按 `heightDifference * fallDamageMultiplier` 伤害。

### 6.2 `PlayerControl`（可控性开关）

`PlayerControl : MonoBehaviour, IPlayerControlProvider`。两级开关 `controllable` / `movementControllable`；`GrantControl/LoseControl/ToggleControl/GrantMovementControl/CheckIfCanGrantControl`（`CheckIfCanGrantControl` 在死亡或暂停时拒绝恢复）。死亡时 `PlayerDeadState` 调 `LoseControl()`。

### 6.3 `PlayerMultipliers` / `PlayerOrientation` / `PlayerGraphics` / `PlayerDebugger`

- `PlayerMultipliers`：三个 `ModifiableStat(1)` + 临时修改器生命周期（见 §3.5）。
- `PlayerOrientation`：**纯数据类（非 MonoBehaviour）**，`Forward/Right/Up/Yaw`；`Position` setter 自动 `+ Vector3.up * 2`（脚底 → 视角）；`UpdateOrientation(pos, yaw)`。
- `PlayerGraphics`：把视觉子物体同步到 `Orientation.Rotation`。
- `PlayerDebugger`：`OnGUI`（`#if UNITY_EDITOR || DEVELOPMENT_BUILD`）运行时调试面板。

---

## 7. 状态机（玩家 + 武器）

两套同构 FSM：Context（`PlayerStates`/`WeaponStates`）→ BaseState（abstract）→ Factory → 具体状态。基类统一生命周期 `EnterState/UpdateState/FixedUpdateState/ExitState/CheckSwitchState`，`SwitchState(newState)` 统一执行 `Exit → newState.Enter → CurrentState = newState`。

### 7.1 玩家状态机（6 状态）

| 状态 | 编排的行为（要点） |
|---|---|
| `PlayerDefaultState` | `cameraLook.Tick`、`footsteps.FootSteps`、`grapplingHook.Tick`、`crouchSlide.CheckUnCrouch`；Fixed：`basicMovement.Movement`、`wallRun.FixedTick`；切换：爬梯/蹲伏 |
| `PlayerJumpState` | `jump.Enter`；`cameraLook.Tick`、`wallBounce.Tick`；Fixed：`basicMovement.Movement`、`wallRun.FixedTick`；切换：墙弹/爬梯/二段跳/落地/冲刺/蹲伏/钩爪 |
| `PlayerCrouchState` | `crouchSlide.Enter`；`crouchSlide.HandleCrouch`、`cameraLook.Tick`；Fixed：`basicMovement.Movement`、`crouchSlide.FixedTick`、`footsteps.FootSteps`、`speedLines.Tick` |
| `PlayerDashState` | `dash.Enter(input)` + `cameraFOVManager.ForceAddFOV(-fovToAddOnDash)`；`dash.Tick`；计时到/`!IsDashing` → Default |
| `PlayerClimbState` | `climbLadder.Enter`；`climbLadder.Tick`、`cameraLook.VerticalLook`；跳跃/顶部/底部退出 |
| `PlayerDeadState` | `playerControl.LoseControl()`；`freezePlayerOnDeath` 时 `rb.isKinematic=true` |

- 工厂 `PlayerStateFactory`：缓存 `_default/_jump/_crouch/_dead/_climb`；`Dash()` 动态 `new PlayerDashState(...)`（依赖运行时输入方向）。
- `PlayerBaseState` 提供 `StartGrapple/StopGrapple/SwitchState/SwitchToDie`（`SwitchToDie` 作为死亡监听回调注册到 `statsProvider.AddOnDieListener`）。

### 7.2 武器状态机（7 状态）

| 状态 | 职责 |
|---|---|
| `WeaponDefaultState` | 待机；处理背包/射击/换弹/瞄准切换 |
| `WeaponShootingState` | 射击；按 `shootMethod` 分派 `PressShoot/PressHoldShoot/HoldAndReleaseShoot/HoldUntilReadyShoot` |
| `WeaponReloadState` | 换弹；`reloadBehaviour.StartReload()` |
| `WeaponInspectState` | 检视武器 + 实时配件定制 |
| `MeleeState` | 快速近战（`quickActionBehaviour.SecondaryMeleeAttack`） |
| `WeaponHiddenState` | 收枪/隐藏（无独立 HolsterState 类） |
| `WeaponUnholsterState` | 拔枪（按 `overrideUnholsterTime ? unholsterTime : .5f`） |

---

## 8. 武器系统

### 8.1 `WeaponController`（组合根 + 门面）

`WeaponController : MonoBehaviour, IWeaponReferenceProvider, IWeaponBehaviourProvider, IWeaponRecoilProvider, IWeaponEventsProvider`。

- 持有 `Weapon`（`Weapon_SO`）、`Id`（`WeaponIdentification`）、`Inventory[]`、`CurrentWeaponIndex`。
- `Start()`：构造 `WeaponContext` → `InitializeBehaviours()` → `OnInitializeWeaponSystem(inventorySize)` → `weaponInventoryBehaviour.GetInitialWeapons()`。
- `InitializeBehaviours()` 装配：`hitDetectionSystem/spreadSystem/attachmentsSystem/weaponEffectsSystem/recoilSystem/weaponWeightSystem/weaponInventoryBehaviour/aimBehaviour/reloadBehaviour/shootBehaviour/quickActionBehaviour`。
- `Update()`：`HandleCrosshairEnemySpot()`（射线检测 Enemy/Critical 标签 → `OnEnemySpotted(bool)`）、`recoilSystem.Tick()`、`reloadBehaviour.HandleHeatRatio()`。
- 对外 API：`InstantiateWeapon`（3 重载）、`ReleaseWeapon`、`TryToAddWeapons`、`SwapWeapons`、`AddDuplicateWeaponAmmo`。

### 8.2 `WeaponContext`

纯数据容器：`Transform/WeaponHolder/InputManager/Dependencies/firePoint[]/CoroutineRunner/AudioSource/Settings/CanShoot`。

### 8.3 四种射击风格（`IShootStyle` 策略）

```csharp
public interface IShootStyle {
    void Shoot(float spread, float damageMultiplier, float shakeMultiplier);
    event Action onShoot;
}
```

| 风格 | 命中判定与伤害 | 特点 |
|---|---|---|
| `HitscanShootStyle` | `GetSpreadDirection` 生成散布 → `Physics.Raycast`；`dmg = id.damage * DamageMultiplier`；二次射线做 `penetrationAmount` 穿透 + `penetrationDamageReduction`；拖尾走 PoolManager | 即时命中 |
| `ProjectileShootStyle` | `Instantiate(projectile)` 并对 `IBullet` 赋值（Speed/Damage/Destination/爆炸参数），`rb.isKinematic = !projectileUsesGravity`；命中由 `Bullet` 处理 | 发射投射物 |
| `MeleeShootStyle` | `Physics.OverlapSphere` 按 `attackRange`；`dmg = damagePerHit * damageMultiplier`；`hitDelay/hitDelays[]` 延迟、`attackRate` 节奏 | 近战 |
| `CustomShootStyle` | 从 `settings.customPrimaryShot[]` 匹配 weapon 后 Invoke 其 `UnityEvent`；`continuousFire` 控制是否每帧调用 | 自定义 |

### 8.4 `Bullet` / `IBullet`（投射物）

- `IBullet` 12 属性：`Speed/Damage/Destination/Gravity/Player/HurtsPlayer/ExplosionOnHit/ExplosionVFX/ExplosionRadius/ExplosionForce/CriticalMultiplier/Duration`。
- `Bullet : MonoBehaviour, IBullet`：`Start`（LookAt(Destination) + `Invoke(DestroyProjectile, Duration)`）、`Update`（`Translate` 前进）、`OnTriggerEnter`（Critical/BodyShot/IDamageable/障碍层分支）、`DestroyProjectile`（爆炸 OverlapSphere 距离衰减 + `AddExplosionForce` + `CameraEffects.ExplosionShake`）。
- **注意**：投射物本身 `Instantiate/Destroy` **不做对象池**；`PoolManager` 仅用于弹孔与拖尾。

### 8.5 11 个武器 Behaviour / System

| 类 | 职责 | 关键点 |
|---|---|---|
| `ShootBehaviour` | 开火，委托 `WeaponIdentification.Shoot()` → `IShootStyle` | `SetShootingStyle` 按 `shootStyle` 在 `OnEquipWeapon` 时重建策略 |
| `ReloadBehaviour` | 换弹/过热 | 委托 `Reload` 在 `DefaultReload/OverheatReload` 间切换；`limitedMagazines` 三分支结算 |
| `AimBehaviour` | 瞄准 | 插值 `id.aimPoint`、处理 `Scope.aimingOffset` |
| `RecoilSystem` | 后坐力 | `recoilX/recoilY` 曲线 + `evaluationProgress(1/magazineSize)` 求值 → `recoilPitchOffset/recoilYawOffset` |
| `SpreadSystem` | 散布 | 维护 `baseSpread/baseAimSpread`，经 `OnGetSpread` 供 `RequestSpread()` |
| `WeaponInventorySystem` | 武器库（16.8KB） | `InstantiateWeapon/SelectWeapon/UnHolster/HolsterCoroutine/HandleInventory/MouseWheelWeaponSwitch/NumKeyWeaponSwitch/SwapWeapons/TryToAddWeapons/AddDuplicateWeaponAmmo` |
| `HitDetectionSystem` | 命中判定/伤害分发 | — |
| `WeaponEffectsSystem` | 枪口焰/弹壳/拖尾 | — |
| `WeaponWeightSystem` | 重量对移速影响 | — |
| `AttachmentsSystem` | 配件装备 | 接收 `OnAttachmentPickedUp` |
| `QuickActionBehaviour` | 快捷近战 | `SecondaryMeleeAttack/FinishMelee` |

### 8.6 附件系统（`Scripts/Weapons/Attachments/`）

- `Attachment` 抽象基类：`ModifyStats(Id, ±1)` 对 `WeaponIdentification` 运行时副本做**百分比修正**。
- `AttachmentIdentifier_SO`：`attachmentType` + 百分比字段。
- `AttachmentStateManager`：维护 `currentAttachments` 字典；`GetCurrent/SetCurrent/IsCurrentDefault/GetState → AttachmentState{None,Default,Custom}`；状态源为 `WeaponIdentification.AttachmentState`。
- `CompatibleAttachments` / `DefaultAttachment`：用序列化 List 桥接运行时 Dictionary。
- 子类：`Barrel`（fireSFXs/muzzleVFX）、`Magazine`（magazineSize）、`Scope`（aimingOffset/aimingRotation）、`Stock`/`Grip`/`Laser`（扩展点）、`Flashlight`（`AttachmentAction` 切灯）。`AttachmentType{Barrel,Scope,Stock,Grip,Magazine,Flashlight,Laser}`。

---

## 9. 拾取与交互系统

文件：`Scripts/PickUpSystem/`。

### 9.1 类层次

```
Identifiable (MonoBehaviour)   ← 可存档唯一标识
  └─ Interactable              ← 可交互基类
       └─ Pickeable (abstract) ← 可拾取基类
            ├─ WeaponPickeable
            ├─ BulletsPickeable
            └─ AttachmentPickeable
```

### 9.2 `InteractManager`（交互中枢）

- **检测**：`DetectInteractable()` 用中心射线 `Physics.Raycast(mainCamera.position, forward, detectInteractionDistance, mask)`，命中后 `TryGetComponent<Interactable>` 并走 `IsForbiddenInteraction` 分流。
- **HUD**：`EnableInteractionUI` → `Events.OnAllowedInteraction(interactText)` 广播文字；进度经 `OnInteractionProgressChanged(float)`。
- **触发**：`DetectInput` 累计 `progressElapsed`，支持长按（`progressRequiredToInteract`）与瞬时（`InstantInteraction`）；`PerformInteraction` 调 `Interact(this.transform)` 并以 `interactInterval` 防连点。
- 实现 `IInteractManagerProvider` + `IInteractEventsProvider`。

### 9.3 拾取联动

- **武器**：经 `IWeaponBehaviourProvider`（`WeaponController` 委托 `WeaponInventorySystem` 的 `TryToAddWeapons/SwapWeapons/AddDuplicateWeaponAmmo`）。
- **配件**：经 `Events.OnAttachmentPickedUp(atc)` 交给 `AttachmentsSystem` 装配。
- **弹药**：`BulletsPickeable` 按 `BulletTypeIdentifier_SO` 匹配武器弹药类型（`MatchesBulletType`）。

---

## 10. UI 系统

文件：`Scripts/UI/`（15 个 .cs）。

### 10.1 `UIController`（HUD 中枢，36KB，核心）

- 单例 `Instance`；`Initialize(PlayerDependencies)` 注入 6 个 Provider，订阅 24+ 个 `.Events.OnXxx` 事件**被动刷新** HUD。
- 血条/护盾：`healthDisplayMethod`（`Action<float,float>`）**多播委托**，条状 `BarHealthDisplayMethod` + 数字 `NumericHealthDisplayMethod` 可叠加；受伤/回血全屏着色 `healthStatesEffect`。
- 金币/经验：`UpdateCoins` / `UpdateXP`（`UIEvents.onCoinsChange` / `onExperienceCollected`）。
- 交互提示：`AllowedInteraction(string)` 用 `Regex.Replace(text, "<.*?>", "")` 剥富文本，动态调宽；`ForbiddenInteraction/DisableInteractionUI/InteractionProgressUpdate/FinishInteraction`。
- 击杀/命中/飘字：`AddKillfeed(name)`、`Hitmarker(bool,bool,Vector3,float)`、`AddDamagePopUp(position,damage)`（均走 `PoolManager`）。
- 武器 HUD：`OnUnholster`、`UpdateWeaponReloadInfo`（defaultReload 显示弹量，Overheat 走 `UpdateHeatRatio(id.heatRatio)`）、`ConfigureReloadUIVisibility`、`DisableWeaponUI`。
- 背包 UI（程序化）：`CreateInventoryUI(int)` → `Instantiate(inventoryUISlot)`；`SelectInventoryUISlot` / `SetInventoryUISlotWeapon`。
- 检视/配件 UI：`GenerateInspectionUI` / `GenerateAttachmentGroup`（7 个配件分组）/ `CleanAttachmentGroup`；`StartRealtimeInspection` / `StopInspection`。
- 冲刺 UI：`DrawDashUI(int)` / `GainDash` / `DashUsed`。
- 鼠标锁定：`LockMouse/UnlockMouse/SetMouseLockState`（`PauseMenu.isPaused` 时不锁）。

### 10.2 `Crosshair`（准星，OnGUI 直绘）

- `OnGUI()` + 1×1 `Texture2D` 直绘（非 uGUI），按 `Screen.width/2, height/2` 为圆心画四向线段 + 中心块 + 角括号。
- 散布：`spread` 按移动状态（run/walk/crouch/jump）`Resize(...)`，射击时 `Resize()`；`SpotEnemy/ShowEnemySpotted/HideEnemySpotted` 命中变色。
- `CrosshairParts`：九段布尔（topPart/downPart/leftPart/rightPart/center/topLeftBracket/...）；`CrosshairShape.SetCrosshair(weapon.crosshairParts)` 实现"每把武器不同准星"。

### 10.3 其他 UI

- `Hitmarker`：`Play(bool headshot)` 音效 + `animator.SetTrigger("hit")` + 红/白变色。
- `UIEffects`：UI sway/tilt/jump，`ApplyCombinedTransform` 四元数叠加。
- `UIEvents`：7 个静态 Action（见 §2.3）。
- 附件 UI：`IAttachmentLayoutStrategy`（`WorldSpaceLayoutStrategy`/`VerticalLayoutStrategy`）+ `AttachmentGroupUI`（互斥展开）+ `AttachmentUIElement`（点击装配/卸载）+ `WeaponsInventoryUISlot`（选中缩放）。
- `RebindUI`：桥接 `InputManager.StartRebind/ResetBinding/LoadBindingOverride`。
- `CowsinsButton : Button`：`GameObjectTransition`（淡入切面板）/`SceneTransition`（`SceneManager.LoadScene`）/音效。

---

## 11. 管理器（Managers）

文件：`Scripts/Managers/`（8 个）。

| 类 | 单例访问器 | 职责 |
|---|---|---|
| `InputManager` | 无单例（静态 `PlayerActions` + 计数） | 输入封装 + 广播（见 §4） |
| `PoolManager` | `Instance`（字段 + 销毁重复） | prefab 为 key 的对象池 |
| `SoundManager` | `Instance`（字段 + 销毁重复，成单例时 `SetParent(null)`） | 2D/3D 音效，3D 音源复用对象池 |
| `CoinManager` | `Instance`（字段 + 销毁重复） | 金币计数，`UIEvents.onCoinsChange` 广播 |
| `ExperienceManager` | `Instance`（字段，不销毁） | 经验/等级 |
| `GameSettingsManager` | `Instance`（**属性式**） | 设置门面（Graphics/Audio/Controls） |
| `DeviceDetection` | `Instance`（字段，不销毁） | 键鼠/手柄检测 |
| `AddonManager` | `instance`（**小写**，无重复检查） | 编译符号探测 DLC |

> ⚠️ **单例实现不一致**：集成时不能假设命名/销毁语义一致。`CoinManager/PoolManager/SoundManager` 是"字段 + 销毁重复"；`DeviceDetection/ExperienceManager` 只赋值不销毁；`GameSettingsManager` 用属性；`AddonManager` 用小写且无重复检查。

### 11.1 `PoolManager`（重点）

- key = **prefab 的 `GameObject` 引用**；`Dictionary<GameObject, Queue<GameObject>> poolDictionary` + `Dictionary<GameObject, List<GameObject>> activeObjects`。
- API：`RegisterPool(prefab,size)` 预热、`GetFromPool(prefab,pos,rot[,returnToPool])`（池空自动 `ExpandPool`）、`ReturnToPool(obj,prefab)`、`RemovePool(prefab)`。
- 自动归还：含 `ParticleSystem` 按 `ps.main.duration` 归还，否则按延时。`SoundManager` 注册 3D 音源池（size 5）。

### 11.2 `GameSettingsManager`（设置门面）

- `Dictionary<Type, SettingsCategory>` 注册三类：`GraphicsSettings`（Fullscreen/Resolution/FrameRate/VSync/Quality）、`AudioSettings`（MasterVolume → `AudioMixer`）、`ControlSettings`（鼠标/手柄灵敏度、Y 反转）。
- 事件 `OnSettingsLoaded/OnSettingsReset/OnSettingsApplied`；`SaveAllSettings/LoadAllSettings/ResetAllSettings/ApplyAllSettings`；`OnApplicationQuit` 兜底保存；PlayerPrefs 持久化。

---

## 12. 敌人系统

文件：`Scripts/Enemies/`。

- `IDamageable`：`void Damage(float damage, bool isHeadshot)` —— **统一伤害契约**，`PlayerStats`/`EnemyHealth`/`Destructible` 均实现。
- `EnemyHealth`：护盾优先吸收、`LerpBar` 血条插值、`OnSpawn/OnDamaged/OnDeath` 事件、killfeed/伤害飘字。
- `TrainingTarget`（静态靶，`Invoke Revive` 复活 + 移除 CompassElement）与 `CircularTargetEnemy`（`MoveTowards` 往返移动）都继承 `EnemyHealth`。
- `Turret`：Sphere/Cube/Cone 三种范围 + 视线检测 + 对象池抛射；`TurretProjectile` 用 `UnityEvent destroyEvent` 回收。

---

## 13. 环境物与交互物（Extra）

文件：`Scripts/Extra/`。逐个简要：

| 类 | 用途 |
|---|---|
| `DoorInteractable` | 可交互门 |
| `JumpPad` | 跳板（3 种 `ForceModeType`） |
| `ExplosiveBarrel` | 爆炸桶（OverlapSphere 半径伤害 + 爆炸力 + 相机震动） |
| `Destructible` | 可破坏物（`virtual Die` 模板方法） |
| `Healthpack` / `PowerUp` / `DamageMultiplier` / `HealMultiplierPowerUp` | 血包/道具（`StatModifier Additive` 临时 buff） |
| `Lootbox` | 战利品箱（金币购买 + 延迟掉落 + 唯一 ID） |
| `CheckPointView` | 检查点（多单位距离） |
| `Compass` / `CompassElement` | 罗盘（`SignedAngle` 定位） |
| `PointCapture` | 占点（progress + UI Slider） |
| `HurtTrigger` | 伤害区域（cooldown） |
| `Coin` / `Crate` / `Experience` | 金币/箱子/经验 |
| `DeathRestart` | 死亡重生 |
| `GetGameInformation` | FPS 计数器/游戏信息 |
| `GravitonWeapon` | 重力枪 |
| `MainMenuManager` / `PauseMenu` | 主菜单/暂停菜单 |
| `Trigger` | 触发器基类 |

### 13.1 设置系统（`Scripts/Extra/SettingsSystem/`）

- `ISetting` / `ISetting<T>` → `Setting<T>` 抽象基类：`Value` setter 内聚「变更 → 通知 → Apply → Save」。
- 具体：`IntSetting/FloatSetting/BoolSetting/StringSetting`（基于 PlayerPrefs）；`ResolutionSetting/FrameRateSetting/VSyncSetting/GraphicsQualitySetting/AudioVolumeSetting` 特化 `Apply`。
- `SettingsCategory` 分组（Graphics/Audio/Controls）；`ToggleSettingUI/SliderSettingUI/DropdownSettingUI` 双向绑定；`SettingsMenuController` 装配。

### 13.2 `CowsinsUtilities`（`Scripts/Extra/Utilities/CowsinsUtilities.cs`）

通用工具：`GetSpreadDirection`、动画快捷方法（`PlayAnim/ForcePlayAnim`）、`GatherDamageableParent`、`InvokeFunc`、`IsFloor`、`CompatibleAttachment`、图层查找扩展（脚步音/弹孔/命中特效）、`MatchesBulletType`、统一日志（`LogWarning/LogError`）。

---

## 14. 特效系统（Effects）

文件：`Scripts/Effects/`。

### 14.1 `CameraEffects`（相机特效中枢）

- `Initialize(PlayerDependencies)` 注入 `IPlayerMovementStateProvider/IPlayerControlProvider/IWeaponEventsProvider/Rigidbody/InputManager`，订阅 `OnLand→LandingShake`、`OnShootShake→ShootShake`。
- `Update()`：`UpdateTilt`（移动输入驱动，非鼠标）、`UpdateHeadBob`（sin 位移）、`UpdateBreathing`（sin 位移 + 可选 cos 旋转）、`HandleCamShake`。
- 震动模型：`PerlinNoise` + `Trauma`（`Clamp01`）衰减；`ShootShake` 硬编码 `power=20/movementAmount=.8f/rotationAmount=17f`；`ExplosionShake(distance)` = `Trauma += 10/distance`；`LandingShakeRoutine` 用 sin 回弹 + 指数衰减在世界空间做垂直位移。

### 14.2 武器特效

- `WeaponEffects`：武器挂点 Bobbing（`rb.linearVelocity.magnitude` 驱动 `bobSpeed`，瞄准 `aimingMultiplier` 缩放）+ Jump Motion（`jumpMotion/groundedMotion` 两条曲线）；`ApplyCombinedEffects` 用 `finalRot = baseRotation * currentBobRot * currentJumpRot` 四元数左乘叠加。
- `WeaponSpecificEffects`：Sway 用**策略模式**——`SwayMethod{Simple,PivotBased}` + `Sway` 委托。Simple 用 `inputManager.Mousex/Mousey`（瞄准 `playerMultiplier=5` 减小摆动）；PivotBased 围绕 `pivot` `RotateAround`。内嵌 CrouchTilt。

### 14.3 程序化射击

- `ProceduralShot_SO`：`playSpeed` + 平移/旋转各 3 条 `AnimationCurve` + `translationDistance/rotationDistance` + 瞄准乘数。
- `ProceduralShot`（单例）：`Shoot(ProceduralShot_SO)` 采样六条曲线，按 `playSpeed` 计时、`translationDistance/rotationDistance` 缩放、瞄准乘数降幅。**确定性曲线驱动**（非运行时随机）。

### 14.4 独立小特效

- `CrouchTilt`（独立蹲伏倾斜）、`JumpMotion`（独立跳跃曲线）、`UISway`（`Mouse.current.position` 驱动的 RectTransform 视差漂移）。

---

## 15. 编辑器工具与自定义特性

### 15.1 自定义特性（`Scripts/CustomAttributes/`）

- `ReadOnlyAttribute` + `ReadOnlyDrawer`：`GUI.enabled=false` 灰显只读。
- `SaveFieldAttribute`：纯标记，供存档反射识别持久化字段。
- `TitleAttribute` + `TitleDrawer`：粗体标题 + 可选灰线分隔（`title/divider/upMargin`）。

### 15.2 编辑器工具（`Scripts/Editor/`）

- `TagLayerInitializationManager`（`cowsins.Inspector`，`[InitializeOnLoad]`）：每会话一次自动初始化 **6 个 Tag**（`Enemy/FirePoint/Critical/Weapons/Ramp/BodyShot`）与 **14 个 Layer**（`Ground/Weapons/Enemy/Object/Interactable/Grass/Metal/Mud/Wood/UITop/PostProcessing/Effects/Player/Ladder`），Layer 空槽从索引 8 开始找。
- `Unity6EditorWindow`（类名 `Unity6EditorWindow`，文件名 `Unity6WindowEditor.cs`）：Unity 6 设置/教程窗口（URP/BuiltIn/HDRP）。
- `WeaponCreatorAssistant`：从 `CowsinsBlankWeaponTemplate.prefab` + `BlankWeaponAnimatorTemplate.controller` 复制生成 `Weapon_SO` 与 weapon prefab，自动挂 Animator、设 `Weapons` Layer、回填 `WeaponIdentification.weapon` 与 `newWeapon.weaponObject`。
- `CowsinsManager`（`CustomTabEditorWindow` + `ITab` 反射式标签页）：Home/Weapons 标签，`DraggableButtonInSceneView`（场景视图可拖拽按钮，`OnAddExternalButtons` 扩展点）、`AddonSymbolTracker`（`AssetPostprocessor` 跟踪附加包移动/删除）。
- 两个 PropertyDrawer：`FootstepSoundsDrawer`（`PlayerMovementSettings.FootStepsSounds` 按层列表）、`ImpactEffectsDrawer`（`WeaponControllerSettings.ImpactEffects` 按层命中特效列表）。

---

## 16. 扩展指南

### 16.1 新增一把武器
1. 创建 `Weapon_SO` 资产（`COWSINS/New Weapon`），或用 `WeaponCreatorAssistant` 从模板生成 prefab + SO。
2. 配置 `weaponObject`（`WeaponIdentification` prefab）、`shootStyle`、`shootMethod`、弹药/伤害/后坐力等字段。
3. 加入 `WeaponControllerSettings.initialWeapons` 或放在场景中作为 `WeaponPickeable`。

### 16.2 新增一种射击方式
实现 `IShootStyle`，在换枪装配处（`WeaponIdentification.SetShootStyle` 的调用点）按 `ShootStyle` 注册新分支。

### 16.3 新增一种移动能力
仿照现有 behaviour：写 `public class MyBehaviour { public MyBehaviour(MovementContext context){...} public void Enter()/Tick()/FixedTick()/Exit()/CanExecute(){...} }`，在 `PlayerMovement` 加属性，在 `InitializeBehaviours()` 里 `new`，在合适的 `Player*State` 调钩子、必要时在 `CheckSwitchState` 加切换条件。

### 16.4 新增一个配件
继承 `Attachment` 实现 `ModifyStats(WeaponIdentification, int)`，创建 `AttachmentIdentifier_SO`，加入 `CompatibleAttachments` 列表，如需 UI 分组则对应 `AttachmentType` 与 `UIController.GenerateAttachmentGroup`。

### 16.5 新增一个可拾取物
继承 `Pickeable`（或 `Interactable`），实现交互后逻辑；如需掉落视觉在 `Item_SO.pickUpGraphics` 配置；武器/子弹/配件分别参考 `WeaponPickeable/BulletsPickeable/AttachmentPickeable`。

### 16.6 新增一个玩家状态
继承 `PlayerBaseState`，在 `PlayerStateFactory` 添加工厂方法，在相关状态的 `CheckSwitchState` 加切换条件。

### 16.7 数值 buff/debuff
```csharp
ModifiableStat stat = new ModifiableStat(100);
stat.AddModifier(new StatModifier(10, StatModifierType.Additive, source: this));       // +10
stat.AddModifier(new StatModifier(0.2f, StatModifierType.Multiplicative, source: this));// +20%
float v = stat.Value;                                  // (100+10)*(1+0.2)=132
stat.RemoveModifierFromSource(this);
// 限时 buff：PlayerMultipliers.AddTemporaryModifier(stat, modifier, duration)
```

### 16.8 常用扩展触点速查
- 读移动状态：`playerDependencies.PlayerMovementState.Grounded / IsDashing / CurrentSpeed ...`
- 读武器状态：`playerDependencies.WeaponReference.Weapon / Id`、`WeaponBehaviour.IsAiming / IsShooting`
- 订阅移动事件：`playerDependencies.PlayerMovementEvents.Events.OnXxx`
- 订阅武器事件：`playerDependencies.WeaponEvents.Events.OnXxx`
- 改 FOV：`cameraFOVManager.SetFOV / ForceAddFOV`
- 播音效：`SoundManager.Instance.PlaySound(...)`
- 对象池：`PoolManager.Instance.GetFromPool / ReturnToPool`

---

## 17. 注意事项与已知问题

### 17.1 文件编码
多个脚本含 `™`/`´` 等非 UTF-8 字节（Windows-1252 / CP1252），`read` 工具按 UTF-8 会报 `invalid UTF-8`。已知涉及：`PlayerStats.cs`、`PlayerControl.cs`、`PlayerStatsEditor.cs`、`MoveCamera.cs`、`IShootStyle.cs`、`Bullet.cs`、`Pickeable.cs`、`Interactable.cs`、`InteractManagerEditor.cs`、`IDamageable.cs` 及 `Extra/` 下多个文件（`CheckPointView/Crate/Destructible/DoorInteractable/ExplosiveBarrel/GetGameInformation/Healthpack/PointCapture/PowerUp/DestroyMe/LookAt` 等）。精读时需按 CP1252 解码。

### 17.2 单例不一致（集成风险）
见 §11 开头表格：访问器名（`Instance` vs `instance`）、销毁语义（销毁重复 vs 不销毁 vs 无检查）不统一。

### 17.3 已知实现细节
- `ProceduralShot_SOEditor.OnInspectorGUI` 中 `Weapon_SO myScript = target as Weapon_SO;` 类型强转错误（target 实为 `ProceduralShot_SO`），`myScript` 未使用——遗留代码。
- `WeaponSpecificEffects.HandleSwayRotation()` 末尾连续两次 `Quaternion.Lerp`，第二次向 `Euler(Vector3.zero)` 逼近，覆盖第一次倾斜结果，PivotBased 倾斜在运行时基本被抵消。
- `Unity6WindowEditor.cs` 类名实为 `Unity6EditorWindow`（文件名与类名不一致）。
- 投射物 `Bullet` 不做对象池（`Instantiate/Destroy`）；对象池仅用于弹孔、拖尾、伤害飘字、击杀条、3D 音源。

### 17.4 依赖前提
- 项目需 Unity 6+（框架内部 `CowsinsUtilities.IsUsingUnity6()` 判断）。
- 需 `com.unity.inputsystem`（`PlayerActions` 生成类）。
- 可选附加包通过编译符号 `INVENTORY_PRO_ADD_ON` / `SAVE_LOAD_ADD_ON` 隔离（`AddonManager` 探测）。

---

*文档基于对 `Assets/Cowsins/Scripts/` 下 205 个脚本的全量精读（含并行分模块分析）与核心文件逐一核对生成。*
