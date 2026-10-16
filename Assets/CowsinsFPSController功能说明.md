# CowsinsFPSController 学习笔记

对照当前打开的 Demo 场景 **`Assets/Cowsins/Demo/BlankScene.unity`** 里的 `CowsinsFPSController` 实例写成。预制体源是 **`Assets/Cowsins/Prefabs/PlayerControllers/CowsinsFPSController.prefab`**。官方文档（你给的 mhtml 就是这份 GitBook 的离线快照）在：

- 在线：[FPS ENGINE DOCUMENTATION](https://cowsinss-organization.gitbook.io/fps-engine-documentation)
- 教程入口：https://www.cowsins.com/
- Discord：见 `Assets/Cowsins/TUTORIALS.txt`

把这个预制体拖进场景，就是一套完整的第一人称玩家：移动、相机、武器、交互、HUD、暂停与设置。根物体本身几乎只有 `Transform`，真正干活的是下面这几个子系统。

---

## 1. 怎么用这个插件（最短路径）

1. 场景里放一个 **`CowsinsFPSController`**（或 `MovementCowsinsFPSController`，后者偏移动展示）。
2. 地面要有碰撞；玩家 Layer 是 **`Player`**，Tag 是 **`Player`**。
3. 武器数据不在控制器脚本里写死，而在 **`Weapon_SO`**（`Assets/Cowsins/ScriptableObjects/Weapons/`）。本 Demo 的 `WeaponController.initialWeapons` **长度为 0**，出生空手，靠场景里的 `WeaponPickeable` 捡枪。
4. 改手感：优先改 Inspector 上的 **`PlayerMovement.playerSettings`** 和 **`WeaponController.settings`**，以及对应 `Weapon_SO`，少改状态机代码。
5. 改键位：走 **New Input System**（`InputManager` + 暂停菜单里的 `RebindUI`），不要另起一套 Input。

官方对应章节：`Add a Player Controller`、`Adding New Weapons`、`Changing Keybindings`、`Controllable & Not Controllable Player`。

---

## 2. 数据怎么流（先建立心智模型）

```
InputManager（读 PlayerActions）
    ↓ 事件 / 布尔快照
PlayerStates / WeaponStates（有限状态机，每帧驱动当前状态）
    ↓ 调用
PlayerMovement 的 Behaviours / WeaponController 的 Behaviours
    ↓ 写刚体、生成子弹、播动画
Camera / WeaponHolder / UIController（表现与 HUD）
```

**`PlayerDependencies` 是接线板**：Awake 时从同一 `Player` 物体上收集接口（移动、武器、生命、交互、可控、乘数），再把引用灌给 `InputManager`、`CameraFOVManager`、`CameraEffects`、`WeaponEffects`、`UIController`、`UIEffects`。缺任何一个接口实现都会打 `Missing required component`。

不要拆散 `Player` 上那一组脚本；官方也强调这些接口必须挂在同一 GameObject。

---

## 3. 层级总览（BlankScene 实测）

根：`CowsinsFPSController` — 仅 `Transform`。

| 一级子物体 | 职责 |
|---|---|
| **Player** | 逻辑体：刚体、胶囊、几乎全部玩家/武器脚本 |
| **Camera** | 相机位置跟随头部；近战刀模型；主相机 + 武器相机 |
| **PlayerGraphics** | 第三人称外观（若有）跟逻辑体位置/朝向 |
| **GeneralManagers** | 音效、金币、经验、Addon、对象池、环境音 |
| **PlayerUI** | 全套 HUD、暂停、设置、键位重绑 |
| **InputManager** | 输入中枢 + 设备检测 |

骨骼/IK 子节点（刀的 `FPSEngine_Rig` 下）是蒙皮骨骼，一般不用改。

---

## 4. `Player`：逻辑体上挂了什么

**Layer=`Player`，Tag=`Player`。**

Unity 组件：

- **Rigidbody**：mass=1.5，重力开，Interpolation=Interpolate。`PlayerMovement` 要求必须有刚体。
- **CapsuleCollider**：height=2，radius=0.5，center=(0, 0.5, 0)。
- **AudioSource**：武器/玩家音效出口（`WeaponController` 会拿它）。
- **LineRenderer**：钩索绳子可视化。

### 4.1 `PlayerDependencies`

集中缓存接口并初始化其它系统。Inspector 里绑到：

| 字段 | 指向 |
|---|---|
| `inputManager` | `/CowsinsFPSController/InputManager` |
| `cameraFOVManager` | `Main Camera` 上的 `CameraFOVManager` |
| `cameraEffects` / `weaponEffects` | 本物体 |
| `uiController` | `PlayerUI` |
| `crosshair` | `PlayerUI/Container/Crosshair` |
| `uIEffects` | `PlayerUI/Container/UI Effects` |

接口来源：`PlayerMovement`、`WeaponController`、`PlayerStats`、`InteractManager`、`PlayerControl`、`PlayerMultipliers`。

### 4.2 `PlayerControl` — 能不能动手

两个开关：

- **全局可控** `IsControllable`：`GrantControl` / `LoseControl` / `ToggleControl`。暂停、死亡会挡 `CheckIfCanGrantControl`。
- **仅移动** `IsMovementControllable`：可单独禁移动，不影响其它动作（仍受全局开关约束）。

UI、过场、死亡冻结都走这里，不要直接 `enabled = false` 整个 `PlayerMovement`。文档章节：`Controllable & Not Controllable Player`。

### 4.3 `PlayerStats` — 生命 / 护盾

本场景实例值：

- `maxHealth=100`，`maxShield=50`（运行时 `health/shield` 会在 Start 填满，编辑器里显示 0 正常）
- `freezePlayerOnDeath=true`（死了把刚体 kinematic）
- `takesFallDamage=false`
- 坠落阈值 5、倍率 2（未开坠落伤时无效果）
- 自动回血关

实现 `IDamageable`。受伤先扣盾再扣血。事件：`OnDeath` / `OnDamage` / `OnHeal`。

### 4.4 `PlayerMultipliers`

运行时可改的 **伤害 / 治疗 / 体重** 乘数（`ModifiableStat`），支持限时修改器。PowerUp、武器重量会往这里挂。

### 4.5 `PlayerMovement` — 移动总控

不把所有逻辑写在一个 Update 里，而是 **Behaviours + `playerSettings`**：

| Behaviour | 功能 |
|---|---|
| `GroundDetectionBehaviour` | 地面检测 |
| `BasicMovementBehaviour` | WASD 走路/跑步 |
| `CameraLookBehaviour` | 鼠标/手柄看向 |
| `JumpBehaviour` | 跳、土狼时间、多段跳 |
| `CrouchSlideBehaviour` | 蹲、滑铲 |
| `DashBehaviour` | 冲刺 |
| `WallRunBehaviour` / `WallBounceBehaviour` | 墙跑、墙跳 |
| `ClimbLadderBehaviour` | 爬梯 |
| `GrapplingHookBehaviour` | 钩索（配合 `LineRenderer`） |
| `StaminaBehaviour` | 体力 |
| `FootstepsBehaviour` | 脚步 |
| `SpeedLinesBehaviour` | 速度线粒子 |
| `VelocityHandlerBehaviour` | 速度限制与处理 |

本场景 **`playerSettings` 实测**：walk=8，run=16，crouch=3，jumpForce=20，maxSpeed=30，FOV 60/80，冲刺/蹲是**按住**不是切换，`canDash/canWallRun/allowGrapple/usesStamina` 全开。

子物体 **`Head ( Camera Placement )`**：空 Transform，作为第一人称相机应对齐的“头部点”。`MoveCamera` 每帧把 `Camera` 物体贴到这个点。

### 4.6 `PlayerStates` — 玩家 FSM

`Start` 进入 `PlayerDefaultState`，每帧 `UpdateState` / `FixedUpdateState`。

工厂缓存：Default / Jump / Crouch / Dead / Climb。**Dash 不缓存**（依赖当帧方向）。

各状态负责“现在允许做什么移动”，具体力仍交给 `PlayerMovement` 的 Behaviour。

### 4.7 `WeaponController` — 武器中枢

`settings`（`WeaponControllerSettings`）本场景：

- `inventorySize=2`
- `initialWeapons` 空 → 场景捡枪
- `weaponHolder` = `WeaponHolder`
- `mainCamera` = `Main Camera`
- `alternateAiming=false`（按住开镜）

运行时子系统：

| 系统 | 作用 |
|---|---|
| `ShootBehaviour` | 开火节奏、射速 |
| `ReloadBehaviour` | 换弹 |
| `AimBehaviour` | 瞄准 |
| `RecoilSystem` | 后坐力 |
| `SpreadSystem` | 散布 |
| `HitDetectionSystem` | Hitscan / 抛射命中 |
| `WeaponWeightSystem` | 枪重影响移动 |
| `AttachmentsSystem` | 瞄具/枪管/握把等 |
| `WeaponEffectsSystem` | 枪口火、弹壳 |
| `QuickActionBehaviour` | 近战等快速动作 |
| `WeaponInventorySystem` | 拾取/切枪/丢枪/弹药 |

武器视觉实例挂在 `WeaponHolder` 下，数据来自 **`Weapon_SO` + `WeaponIdentification`**。切枪、开火事件走 `WeaponControllerEvents` / `settings.userEvents`。

### 4.8 `WeaponStates` — 武器 FSM

状态：Default / Shooting / Reload / Melee / Inspect / Unholster / Hidden。

`holding` 用来处理按住开火。丢枪回到 Default；掏枪走 Unholster。

### 4.9 `WeaponAnimator`

根据移动把 Animator 的 `speed` 在 0（待机）/ 0.5（走）/ 1（跑）之间过渡；触发 `unholster`、`reloading`、`emptyReloading`、`inspect`。`holsterMotionObject` 是收枪动作 Animator。

### 4.10 `InteractManager` — 射线交互

主相机向前打射线（本场景距离 6），LayerMask 过滤可交互物。

本场景：`canDrop=true`，丢枪距离 2，随机偏移 0.2，交互进度门槛 0.5，间隔 0.4。丢枪/丢配件用：

- `Assets/Cowsins/Prefabs/DragAndDropExtras/WeaponPickeableGeneric.prefab`
- `Assets/Cowsins/Prefabs/DragAndDropExtras/AttachmentGenericPickeable.prefab`

`canInspect=true`。实时改配件 UI 默认关。

可交互物脚本：`Interactable` / `Pickeable` / `WeaponPickeable` / `BulletsPickeable` / `AttachmentPickeable`。

### 4.11 `CameraEffects`（挂在 Player 上）

作用在 **玩家相机 Transform**：移动倾斜、Head Bob、呼吸、落地抖动、射击抖动（trauma）。依赖 `PlayerDependencies`。

### 4.12 `WeaponEffects`（挂在 Player 上）

作用在 **武器挂点**：走路 bob、跳跃/落地曲线。瞄准时会缩小幅度。

---

## 5. `Camera` 子树

### 5.1 `Camera` + `MoveCamera`

每帧 `transform.position = cameraHead.position`，`cameraHead` 应指向 `Player/Head ( Camera Placement )`。旋转由 `CameraLookBehaviour` 写在 pivot/玩家朝向上，不要在 `MoveCamera` 里转视角。

### 5.2 `FastActions` + `MeleeKnife_FPSEngineRig`

`ParentConstraint` 把近战刀绑到相机。刀物体默认 **inactive**。近战由 `WeaponController.settings.canMelee` + `QuickActionBehaviour` + `MeleeState` 打开。Layer=`Weapons`。

### 5.3 `CameraPivot` + `CameraAnimations`

检视/丢弃时相机跟目标旋转，无目标则旋回。丢枪事件会重置 target。

### 5.4 `Main Camera`

- `Camera` + `AudioListener`，Tag=`MainCamera`
- `CameraFOVManager`：走/跑/墙跑/冲刺/开镜/射击时插值 FOV
- 当前有一个 **Missing Script**（历史残留），不影响核心，清理时注意别删错

子物体 **`WeaponCamera`**：只渲武器层，避免枪穿墙。其下：

- `WeaponsEffects` + **`ProceduralShot`**：按 `ProceduralShot_SO` 曲线做程序化后坐/位移
- `WeaponHolder` + Animator：当前枪模型的父节点

### 5.5 `SpeedLines`

粒子，冲刺/高速时由 `SpeedLinesBehaviour` 控制。Layer=`Weapons`。

---

## 6. `PlayerGraphics`

`PlayerGraphics` 每帧复制 `Player` 的位置和 `Orientation.Rotation`。第一人称通常看不到自己的身体；给第三人称模型或死亡布娃娃用。

---

## 7. `GeneralManagers`

| 组件 | 作用 |
|---|---|
| `SoundManager` | 单例，2D/3D 音效；3D 走对象池 |
| `CoinManager` | 金币 |
| `ExperienceManager` | 升级/经验条 |
| `AddonManager` | 官方 Add-On 挂钩 |
| `PoolManager` | 子弹、命中特效、拖尾、伤害数字对象池 |
| `AmbientSounds` | 环境 AudioSource |

场景里的击中火花、弹壳应走 Pool，不要 `Instantiate` 到爆。

---

## 8. `PlayerUI`

根：`UIController` + Canvas（Scale With Screen）+ `CanvasGroup`。

### 8.1 `UIController` 管什么

血条/护盾条或数字、受伤闪屏、交互提示与进度、检视配件面板、弹药/过热、当前武器图标、背包槽、Dash 槽、经验、准星引用、暂停菜单引用。

### 8.2 `Container` 下功能块

| 物体 | 脚本 / 作用 |
|---|---|
| **UI Effects** | `UIEffects`：准星扩散、热度条、弹药 TMP、武器图标、背包、击杀条、血/盾 Slider、经验、交互 UI、体力条、低弹药/换弹提示、金币、指南针 `Compass`、Dash 槽、配件检视面板 `AttachmentGroupUI` |
| **Crosshair** | `Crosshair` + `CrosshairShape` |
| **Hitmarker** | `Hitmarker` + Animator |
| **HealthStatesEffect** | 受伤/治疗全屏色 |
| **GameInformation** | `GetGameInformation` 显示 FPS |
| **DeathSimpleScreen** | 死亡 UI + `DeathRestart` |
| **PauseMenu** | 暂停；剥夺/归还 `PlayerControl` |
| **GameSettingsManager** | `GameSettingsManager` + `SettingsMenuController`：分辨率、全屏、帧率、VSync、画质、音量、鼠标/手柄灵敏度、Y 轴反转 |

暂停里 **Controls** 页用大量 **`RebindUI`**：Jump / Melee / Aim / Fire / Inspect / Drop / Interact / Grapple / Dash / Sprint / Crouch / Flashlight / Scroll，以及对应手柄绑定。

`CowsinsButton` 是引擎自己的按钮（带动画高亮）。

---

## 9. `InputManager`

基于 `PlayerActions`（Input System）。静态 `InputManager.inputActions` 全局一份。

对外：

- 事件：`OnShoot`、`OnAimPressed`、`OnJump`、`OnDash`、`OnCrouchPressed`、`OnDrop`、`OnInspect`、`OnMelee`、`OnTogglePause`、`OnStartGrapple`…
- 快照：`X/Y` 移动，`Mousex/Mousey`，`Shooting`，`Aiming`，`Reloading`…

`DeviceDetection`：键盘/手柄切换，设置页灵敏度走对应轴。

冲刺/蹲/瞄准的“按住 vs 切换”由 `PlayerMovement` / `WeaponController` 的 `alternate*` 传进 `SetPlayerInputModes`。

---

## 10. 本场景实例 vs 你以后该改哪里

| 目标 | 改哪里 |
|---|---|
| 走跑蹲跳、墙跑、钩索、体力 | `Player` → `PlayerMovement.playerSettings` |
| 灵敏度、FOV | 同上，或暂停菜单（会写 `GameSettingsManager`） |
| 血量 | `PlayerStats` |
| 出生带枪 | `WeaponController.settings.initialWeapons` |
| 某把枪伤害/射速/后坐 | 对应 `Weapon_SO`，不是 Controller |
| 准星形状 | `Crosshair` / `CrosshairShape` |
| 禁操作（过场） | `PlayerControl.LoseControl()` |
| 新可拾取物 | 仿 `Pickeable` + `Interactable` |
| 新键 | Input Actions + `InputManager` 事件 + 可选 `RebindUI` |

---

## 11. 建议阅读顺序（对着代码学）

1. `PlayerDependencies.cs` — 接线
2. `InputManager.cs` — 输入
3. `PlayerControl.cs` + `PlayerStats.cs`
4. `PlayerMovement.cs` + `PlayerMovementSettings.cs` + 一个 Behaviour（如 `JumpBehaviour`）
5. `PlayerStates.cs` / `PlayerDefaultState.cs`
6. `WeaponController.cs` + `Weapon_SO` + `ShootBehaviour.cs`
7. `WeaponStates.cs` / `WeaponDefaultState.cs`
8. `InteractManager.cs`
9. `UIController.cs`（很长，按 HUD 功能跳读）

官方 Content 目录与脚本一一对应：`03.1 CAMERA`、`03.2 MOVEMENT`、`03.3 PLAYER`、`03.4 WEAPONS`、`03.5 PICK UP`、`03.7 EFFECTS`、`03.8 UI`、`03.09 MANAGERS`。

---

## 12. 和另一套控制器的区别

`MovementCowsinsFPSController` 在 `Assets/Cowsins/Prefabs/PlayerControllers/`，偏移动 Showroom。游戏玩法 Demo 用当前这套 **`CowsinsFPSController`** 即可。文档有 `Switch FPS Engine´s Controller`。

---

*场景数据来自编辑器 Pipeline 对 BlankScene 中该实例的层级与序列化字段读取；脚本说明对照 `Assets/Cowsins/Scripts` 与官方 GitBook。*
