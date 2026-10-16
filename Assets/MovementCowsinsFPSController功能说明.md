# MovementCowsinsFPSController 学习笔记（对照 CowsinsFPSController）

记得你之前那份：**`Assets/CowsinsFPSController功能说明.md`**（BlankScene 里的 `CowsinsFPSController`）。本文件写的是当前编辑器场景里的 **`MovementCowsinsFPSController`**，并和那份对照。

| | CowsinsFPSController | 本文件（Movement 版） |
|---|---|---|
| 当前场景 | `Assets/Cowsins/Demo/BlankScene.unity` | **`Assets/Scenes/SampleScene.unity`** |
| 预制体 | `Assets/Cowsins/Prefabs/PlayerControllers/CowsinsFPSController.prefab` | `Assets/Cowsins/Prefabs/PlayerControllers/MovementCowsinsFPSController.prefab` |
| 官方配套 Demo | BlankScene / 综合玩法 | 官方还有 `Assets/Cowsins/Demo/MovementShowroom.unity`（移动展厅） |

官方文档（mhtml 是 GitBook 欢迎页快照）：[FPS ENGINE DOCUMENTATION](https://cowsinss-organization.gitbook.io/fps-engine-documentation) → `Switch FPS Engine´s Controller`、`03.2 MOVEMENT` / `PlayerMovement`。教程：https://www.cowsins.com/

---

## 先记住结论

两套控制器 **不是两套代码**。`Player` 上挂的脚本类型完全一样，数据流也一样（`InputManager` → 两套 FSM → Movement/Weapon Behaviours → 相机/HUD）。差别几乎全在 **预制体 Inspector 默认值**（为了练移动：二段跳、墙跑 FOV、关体力、关墙跳反弹），以及 Main Camera 上 Movement 版 **没有 Missing Script**。

做完整 FPS（捡枪、配件、击杀条）两套都能用；专门调走跑蹲跳墙跑钩索时，用 **Movement** 这套更贴官方 Showroom。

---

## 1. 和 CowsinsFPSController 的实测对比

当前 Pipeline 读到的 Movement 实例 vs 上一份 BlankScene 笔记：

| 项 | CowsinsFPSController（前一份 md） | MovementCowsinsFPSController（本场景） |
|---|---|---|
| 根物体脚本 | 仅 Transform | 仅 Transform（相同） |
| `Player` 组件列表 | Movement / Weapon / Stats / Interact / Control / 特效 / 两套 States / Animator / Multipliers / Dependencies + RB / Capsule / Audio / LineRenderer | **完全相同** |
| walk / run / crouch | 8 / 16 / 3 | 8 / 16 / 3 |
| jumpForce / maxSpeed | 20 / 30 | 20 / 30 |
| 冲刺、蹲切换 | 按住（alternate=false） | 相同 |
| 冲刺 | 开 | 开；有限 3 段，非无限 |
| 墙跑 | 开 | 开；**wallrunningFOV=100** |
| 墙跳反弹 `canWallBounce` | （前一份未单独记） | **关** |
| 钩索 / 爬梯 | 开 | 开 |
| **体力 `usesStamina`** | **开** | **关**（maxStamina 仍是 10，不扣） |
| 速度线 | （默认开） | **开** |
| 最大连跳 | （未单独记） | **2（二段跳）** |
| 生命/盾 | 100 / 50，死冻结，无坠落伤 | 相同 |
| 背包 | 2 格，`initialWeapons` 空 | 相同；`canMelee=true` |
| 交互 | 距离 6，可丢枪，可检视 | 相同，同一套 Generic Pickeable |
| Main Camera | 曾有 **Missing Script** | **干净**：Camera + FOVManager + AudioListener |
| PlayerUI / 暂停 / 重绑 | 全套 HUD | **结构同套**（血盾、准星、暂停、设置、RebindUI） |

脚本职责请直接看前一份 md 的第 4～9 节；下面只写层级和「学移动时该拧哪些旋钮」。

---

## 2. 数据流（与前一份相同）

```
InputManager（PlayerActions）
    → PlayerStates / WeaponStates
    → PlayerMovement 的 Behaviours / WeaponController 的 Behaviours
    → Rigidbody、相机、WeaponHolder、PlayerUI
```

接线板仍是 **`PlayerDependencies`**（本实例绑到 `/MovementCowsinsFPSController/...`）：

| 字段 | 指向 |
|---|---|
| `inputManager` | `.../InputManager` |
| `cameraFOVManager` | `.../Main Camera` |
| `cameraEffects` / `weaponEffects` | `Player` 自身 |
| `uiController` | `.../PlayerUI` |
| `crosshair` | `.../PlayerUI/Container/Crosshair` |
| `uIEffects` | `.../PlayerUI/Container/UI Effects` |

---

## 3. 层级（SampleScene 实测）

根：`MovementCowsinsFPSController` — 仅 `Transform`。

```
MovementCowsinsFPSController
├── Player                          逻辑体（脚本全集 + 刚体胶囊）
│   └── Head ( Camera Placement )   第一人称相机对齐点
├── Camera                          MoveCamera 贴头
│   ├── FastActions                 近战刀约束（刀默认 inactive）
│   ├── CameraPivot                 CameraAnimations
│   │   └── CameraContainer
│   │       └── Main Camera         FOV + WeaponCamera + WeaponHolder
│   └── SpeedLines                  冲刺粒子
├── PlayerGraphics                  外观跟随逻辑体
├── GeneralManagers                 音效/金币/经验/Addon/对象池
├── PlayerUI                        HUD + 暂停 + 设置
└── InputManager                    输入 + DeviceDetection
```

骨骼（`FPSEngine_Rig`）可忽略。Layer：玩家 `Player`，刀/速度线 `Weapons`，UI `UI`。

---

## 4. `Player` 上每个脚本干什么（精简索引）

和前一份 **类型一一对应**，不换类名：

| 脚本 | 功能 |
|---|---|
| `PlayerDependencies` | 收集接口并初始化输入/FOV/特效/UI |
| `PlayerControl` | 全局可控 / 仅移动可控 |
| `PlayerStats` | 血/盾/坠落伤/自动回血；本场景 100/50、无坠落伤 |
| `PlayerMultipliers` | 伤害/治疗/体重乘数 |
| `PlayerMovement` | 所有移动 Behaviour + `playerSettings`（本预制体的核心差异在这里） |
| `PlayerStates` | Default / Jump / Crouch / Dead / Climb；Dash 按方向动态建 |
| `WeaponController` | 射击/换弹/瞄准/后坐/散布/命中/配件/库存；本场景空手出生 |
| `WeaponStates` | Default / Shooting / Reload / Melee / Inspect / Unholster / Hidden |
| `WeaponAnimator` | 待机/走/跑混合 + 掏枪换弹检视 |
| `InteractManager` | 射线交互、丢枪、检视 |
| `CameraEffects` | 倾斜、Head Bob、呼吸、落地/射击抖动 |
| `WeaponEffects` | 枪械 bob、跳/落地曲线 |
| `Rigidbody` | mass=1.5，重力开，Interpolate |
| `CapsuleCollider` | h=2，r=0.5，center=(0,0.5,0) |
| `AudioSource` | 武器/玩家音 |
| `LineRenderer` | 钩索绳子 |

移动 Behaviour 仍是：地面、走路、视角、跳、蹲滑铲、冲刺、墙跑、墙跳、爬梯、钩索、体力、脚步、速度线、速度限制。本预制体 **体力系统关着**，滑铲/冲刺不会因 stamina 卡住，方便练移动。

---

## 5. 相机 / 图形 / 管理器 / UI / 输入

与前一份 md 第 5～9 节相同，只换根路径前缀 `MovementCowsinsFPSController`：

- **`MoveCamera`**：位置跟 `Head ( Camera Placement )`。
- **近战刀** `MeleeKnife_FPSEngineRig`：默认关；`canMelee=true` 时近战会开。
- **`WeaponCamera`**：只渲武器，防穿墙；下挂 `ProceduralShot` + `WeaponHolder`。
- **`GeneralManagers`**：`SoundManager`、`CoinManager`、`ExperienceManager`、`AddonManager`、`PoolManager`、环境音。
- **`PlayerUI`**：血/盾、弹药、背包、击杀条、经验、交互、体力条（体力关了条可能一直满）、换弹提示、金币、指南针、Dash 槽、配件检视、准星、Hitmarker、FPS、死亡、暂停、视频/音频/键位（含 Grapple/Dash/Sprint 等 Rebind）。
- **`InputManager` + `DeviceDetection`**：同一套 `PlayerActions`。

---

## 6. 学移动时优先改这些（本预制体）

| 手感 | `Player` → `PlayerMovement.playerSettings` |
|---|---|
| 走跑蹲速、加速度 | `walkSpeed` / `runSpeed` / `crouchSpeed` / `acceleration` |
| 跳、二段跳 | `jumpForce`、`maxJumps=2` |
| 冲刺段数 | `canDash`、`amountOfDashes=3` |
| 墙跑视野 | `canWallRun`、`wallrunningFOV=100` |
| 钩索 | `allowGrapple` + `LineRenderer` |
| 爬梯 | `canClimb` |
| 体力（本套默认关） | `usesStamina` 打开才会扣 |
| 墙跳弹开 | 打开 `canWallBounce` |
| 灵敏度 / FOV | `sensitivityX/Y`、`normalFOV=60`、`runningFOV=80`，或暂停菜单 |

枪仍走 `Weapon_SO`；本场景出生空手，要枪就填 `WeaponController.settings.initialWeapons` 或放 `WeaponPickeable`。

---

## 7. 你该用哪一套

| 目标 | 用哪个 |
|---|---|
| 综合玩法、捡枪、配件、关卡 Demo | **CowsinsFPSController**（前一份 md） |
| 只调移动、对照 MovementShowroom | **MovementCowsinsFPSController**（本文件） |
| 自己的正式玩家 | 复制其中一套预制体，不要两个都放进同一场景（会抢 `InputManager.inputActions`、音效单例等） |

官方：`Add a Player Controller`、`Switch FPS Engine´s Controller`。

---

## 8. 建议阅读顺序

若已读过 `CowsinsFPSController功能说明.md`：只精读 **`PlayerMovementSettings.cs`** + `DashBehaviour` / `WallRunBehaviour` / `GrapplingHookBehaviour` / `JumpBehaviour`，对照本文件第 1 节表格拧 Inspector。

若没读过：按前一份第 11 节顺序读脚本，再回到本文件看「体力关、二段跳、墙跑 FOV」这些默认差异。

---

*场景数据来自 Pipeline：`SampleScene` 中 `MovementCowsinsFPSController` 实例；对照文档 `Assets/CowsinsFPSController功能说明.md`。*
