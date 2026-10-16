# FPSGame 开发路线图 — 类 Valorant 多人联机 FPS

> 项目：Unity + Cowsins FPS Engine + Mirror 网络框架
> 更新日期：2026-09-01

---

## 📊 现状总结

### ✅ Cowsins 已提供（单机可用）

| 系统 | 状态 | 说明 |
|------|------|------|
| FPS 移动 | ✅ 完整 | 走/跑/蹲/跳/滑铲/爬墙/抓钩（需裁剪掉 Valorant 不需要的） |
| 射击系统 | ✅ 完整 | Hitscan/弹道/近战/穿透，策略模式可扩展 |
| 武器配置 | ✅ 完整 | SO 数据驱动，含后坐力/散布/伤害衰减/配件 |
| 生命值 + 护盾 | ✅ 完整 | 和 Valorant 的血+甲模型一致 |
| 爆头倍率 | ✅ 完整 | IDamageable 接口含 isHeadshot 参数 |
| ADS 瞄准 | ✅ 完整 | 支持 FOV 缩放和瞄准减速 |
| HUD | ✅ 完整 | 准星/弹药/血条/击杀提示/伤害数字 |
| 武器拾取/切换 | ✅ 完整 | 背包 + 切枪 + 拾取 |

### ❌ 缺失的关键系统

| 系统 | 难度 | 说明 |
|------|------|------|
| **网络同步** | 🔴 极高 | Mirror 装了但完全没接入，所有 Cowsins 组件都是单机的 |
| **服务端权威命中检测** | 🔴 极高 | 竞技 FPS 必须服务端判定，当前是客户端 raycast |
| **回合制/经济系统** | 🟠 高 | 买阶段、积分、武器购买 |
| **特工/技能系统** | 🟠 高 | Valorant 的核心特色，完全从零开始 |
| **Spike（炸弹）安放/拆除** | 🟡 中 | 安包/拆包/爆炸 |
| **队伍系统** | 🟡 中 | 攻守双方分配 |
| **Valorant 风格 UI** | 🟠 高 | 买菜单、特工选择、计分板、小地图 |

---

## 🗺️ 开发路线图

### 第一阶段：让多人联机能跑起来 🏗️

> **目标：两个玩家能连上同一个服务器，看到对方移动和射击**

| 步骤 | 任务 | 关键文件/组件 |
|------|------|--------------|
| 1 | 修复 ZNetworkManager（当前 Awake() 覆盖为空，会抑制基类初始化） | `Assets/Scripts/Manager/ZNetworkManager.cs` |
| 2 | 创建网络玩家预制体（NetworkIdentity + NetworkTransform + 玩家控制器） | 新建 Player Prefab |
| 3 | 同步玩家位置/旋转 | Mirror NetworkTransform / 自定义 SyncVar |
| 4 | 同步玩家外观（动画状态、武器模型） | NetworkAnimator + 自定义同步 |
| 5 | 基础大厅/房间（Host / Join，分配玩家） | ZNetworkManager + Mirror HUD |

**阶段产出：** 两个客户端连接同一服务器，能互相看到对方移动

---

### 第二阶段：核心对战循环 ⚔️

> **目标：玩家能互相射击，有血量，能死能复活**

| 步骤 | 任务 | 说明 |
|------|------|------|
| 6 | 服务端权威射击流程 | 客户端发射击意图 → 服务端验算+raycast → 服务端扣血 → 广播命中结果 |
| 7 | 生命值/护盾网络同步 | SyncVar 同步血量，客户端只做显示 |
| 8 | 死亡/重生/复活 | 死亡动画、重生逻辑、观战模式 |
| 9 | 武器状态同步 | 当前武器、换弹、开火状态跨网络同步 |

**阶段产出：** 玩家可以互相击杀，有完整的射击→伤害→死亡→重生循环

---

### 第三阶段：Valorant 对局模式 🎯

> **目标：5v5 攻守回合制，有经济和买阶段**

| 步骤 | 任务 | 说明 |
|------|------|------|
| 10 | 队伍系统 | 攻方/守方分配，5v5 人数限制 |
| 11 | 回合系统 | 回合计时、胜负判定（全灭/炸弹爆炸/拆除/超时） |
| 12 | 经济/买阶段 | 积分系统、买区域、买菜单、回合间保留经济 |
| 13 | Spike 安放/拆除 | 指定安放点、拆除交互、爆炸倒计时、全局 UI 指示 |
| 14 | 半场换边 | 12回合后攻守互换 |

**阶段产出：** 完整的 5v5 回合制对局，含买阶段和经济系统

---

### 第四阶段：特工与技能 ✨

> **目标：不同特工有独特技能，Valorant 的灵魂**

| 步骤 | 任务 | 说明 |
|------|------|------|
| 15 | 特工选择界面 | 选人阶段，每人选一个特工 |
| 16 | 技能框架 | 4 个技能槽（C/Q/E/X），冷却/充能/释放 |
| 17 | 基础技能实现 | 烟雾、闪光、墙壁、治疗等 |
| 18 | 技能网络同步 | 所有技能效果必须服务端验证 + 广播 |

**阶段产出：** 多个可选特工，每个有 4 个独特技能，网络同步

---

### 第五阶段：打磨 🎨

> **目标：可玩的完整体验**

| 步骤 | 任务 | 说明 |
|------|------|------|
| 19 | UI 全面替换 | Valorant 风格买菜单、计分板、小地图、技能 HUD |
| 20 | 固定弹道模式 | Valorant 的确定性后坐力模式（非随机 AnimationCurve） |
| 21 | 反作弊基础 | 服务端验证移动合法性、射速合法性、散布合法性 |
| 22 | 地图制作 | 炸弹点（A/B）、出生点、买区域、掩体设计 |
| 23 | 语音通话 | 团队语音通讯 |

**阶段产出：** 接近 Valorant 核心体验的多人联机 FPS

---

## 🔧 技术要点备忘

### Cowsins 需要裁剪的功能（Valorant 不需要）
- ❌ 滑铲（CrouchSlideBehaviour）
- ❌ 跑墙（WallRunBehaviour）
- ❌ 墙弹（WallBounceBehaviour）
- ❌ 抓钩（GrapplingHookBehaviour）
- ❌ 武器配件系统（Valorant 无配件）
- ❌ 拾取系统（Valorant 用买阶段，非地图拾取）

### 服务端权威原则（竞技 FPS 必须）
- ✅ 所有伤害计算在服务端执行
- ✅ 射击验证在服务端（防自瞄/防射速修改）
- ✅ 移动合法性验证（防穿墙/防加速）
- ✅ 经济/买阶段逻辑在服务端
- ✅ Spike 安放/拆除在服务端判定

### Mirror 关键组件
- `NetworkManager` — 连接管理
- `NetworkIdentity` — 网络对象标识
- `NetworkBehaviour` — 网络脚本基类（SyncVar/Command/ClientRpc）
- `NetworkTransform` — 位置同步
- `NetworkAnimator` — 动画同步
- KCP Transport — UDP 可靠传输（推荐用于 FPS）
- `LagCompensation` — 服务端延迟补偿（命中检测必须）

---

## 🎯 下一步行动

**立即开始第一阶段第 1 步：修复 ZNetworkManager + 创建网络玩家预制体 + 在场景中放置 NetworkManager**


## 代码文件架构

如果你现在是做 **Unity + Mirror + Server/Client（纯 Dedicated Server，不要 Host 模式）** 的联机 FPS，我比较建议你从一开始就把代码按 **“网络层 / 游戏逻辑 / 玩家 / 武器 / 表现层”** 分开。

你之前做的 FPS 联机项目，如果继续往下扩展，我会推荐这种结构：

```text
Assets/
└── Scripts/
    │
    ├── Core/                       # 最底层、通用代码
    │   ├── Game/
    │   │   ├── GameManager.cs
    │   │   ├── GameState.cs
    │   │   └── GameMode.cs
    │   │
    │   ├── Event/
    │   │   └── EventCenter.cs
    │   │
    │   └── Utility/
    │       ├── Timer.cs
    │       └── MathUtility.cs
    │
    ├── Network/                    # Mirror相关
    │   ├── NetworkManager/
    │   │   └── FPSNetworkManager.cs
    │   │
    │   ├── Player/
    │   │   ├── NetworkPlayer.cs
    │   │   └── NetworkPlayerState.cs
    │   │
    │   ├── Messages/
    │   │   ├── PlayerInputMessage.cs
    │   │   └── HitMessage.cs
    │   │
    │   └── Transport/
    │
    ├── Gameplay/                   # 真正的游戏规则
    │   │
    │   ├── Player/
    │   │   ├── PlayerController.cs
    │   │   ├── PlayerMovement.cs
    │   │   ├── PlayerHealth.cs
    │   │   ├── PlayerState.cs
    │   │   └── PlayerInteraction.cs
    │   │
    │   ├── Weapon/
    │   │   ├── Weapon.cs
    │   │   ├── WeaponData.cs
    │   │   ├── HitscanWeapon.cs
    │   │   ├── ProjectileWeapon.cs
    │   │   ├── WeaponManager.cs
    │   │   └── WeaponState.cs
    │   │
    │   ├── Combat/
    │   │   ├── DamageInfo.cs
    │   │   ├── DamageSystem.cs
    │   │   ├── Hitbox.cs
    │   │   └── TeamSystem.cs
    │   │
    │   └── GameMode/
    │       ├── GameModeBase.cs
    │       ├── TeamDeathMatch.cs
    │       └── RoundManager.cs
    │
    ├── Prediction/                 # 客户端预测
    │   ├── Input/
    │   │   ├── PlayerInput.cs
    │   │   └── InputState.cs
    │   │
    │   ├── Movement/
    │   │   ├── PlayerPrediction.cs
    │   │   └── MovementState.cs
    │   │
    │   └── Reconciliation/
    │       └── ClientReconciliation.cs
    │
    ├── Server/                     # Dedicated Server专属逻辑
    │   ├── ServerGameManager.cs
    │   ├── ServerPlayer.cs
    │   ├── ServerCombat.cs
    │   └── ServerRoundManager.cs
    │
    ├── Client/                     # 客户端专属逻辑
    │   ├── ClientGameManager.cs
    │   ├── ClientPlayer.cs
    │   ├── Camera/
    │   │   ├── FPSCamera.cs
    │   │   └── CameraController.cs
    │   │
    │   └── UI/
    │       ├── HUD.cs
    │       ├── Crosshair.cs
    │       └── KillFeed.cs
    │
    └── Presentation/               # 纯表现
        ├── Animation/
        │   ├── PlayerAnimator.cs
        │   └── WeaponAnimator.cs
        │
        ├── VFX/
        │   ├── HitEffect.cs
        │   ├── MuzzleFlash.cs
        │   └── BulletImpact.cs
        │
        └── Audio/
            ├── WeaponAudio.cs
            └── PlayerAudio.cs
```

### 但是有一个你现在特别需要搞清楚的概念

**“代码按照功能分文件夹”和“代码按照 Server / Client 分离”是两回事。**

比如：

```text
PlayerMovement.cs
```

这个东西既涉及：

```text
Client：
读取 WASD
↓
预测移动
↓
立即移动角色

Server：
接收输入
↓
验证输入
↓
计算真正的位置
↓
同步结果
```

所以你不能简单地认为：

```text
Client/PlayerMovement.cs
Server/PlayerMovement.cs
```

这样就一定是最好的。

对于 FPS，实际上更推荐：

```text
Gameplay/
    Player/
        PlayerMovement.cs
```

然后在代码内部区分：

```csharp
[Client]
void HandleInput()
{
    // 客户端读取输入
}

[Server]
void ServerMove()
{
    // 服务端权威移动
}
```

---

# FPS最核心的架构

你现在做的是联机 FPS，我反而建议你重点记住下面这张关系：

```text
                    ┌──────────────┐
                    │    Client    │
                    │              │
                    │ Input        │
                    │ Camera       │
                    │ Prediction   │
                    │ VFX          │
                    └──────┬───────┘
                           │
                     Input / Command
                           │
                           ▼
                    ┌──────────────┐
                    │    Server    │
                    │              │
                    │ Validate     │
                    │ Movement     │
                    │ Combat       │
                    │ Damage       │
                    │ Game Rules   │
                    └──────┬───────┘
                           │
                       State
                           │
                           ▼
                    ┌──────────────┐
                    │    Clients   │
                    │              │
                    │ Interpolation│
                    │ Animation    │
                    │ VFX          │
                    └──────────────┘
```

核心原则就是：

> **Client负责“想做什么”，Server负责“到底有没有发生”。**

例如玩家按鼠标左键：

```text
Client
    ↓
我想开枪
    ↓
发送 FireInput
    ↓
Server
    ↓
检查：
    ├── 玩家是否活着？
    ├── 有没有子弹？
    ├── 是否处于换弹？
    ├── 射速是否允许？
    ├── 是否有资格开枪？
    ↓
Server执行射击
    ↓
计算命中
    ↓
扣血
    ↓
广播结果
```

而不是：

```text
Client
    ↓
Physics.Raycast()
    ↓
Enemy.TakeDamage()
```

这种写法在单机没问题，**联机 FPS 就非常容易被作弊。**

---

# 玩家代码我建议这样拆

不要搞一个：

```text
Player.cs
```

里面 3000 行。

建议：

```text
Player/
├── PlayerController.cs
├── PlayerMovement.cs
├── PlayerHealth.cs
├── PlayerState.cs
├── PlayerInput.cs
├── PlayerWeapon.cs
├── PlayerInventory.cs
└── PlayerAnimator.cs
```

例如：

```csharp
public class PlayerController : NetworkBehaviour
{
    public PlayerMovement Movement { get; private set; }
    public PlayerHealth Health { get; private set; }
    public PlayerWeapon Weapon { get; private set; }

    private void Awake()
    {
        Movement = GetComponent<PlayerMovement>();
        Health = GetComponent<PlayerHealth>();
        Weapon = GetComponent<PlayerWeapon>();
    }
}
```

然后：

```text
PlayerController
      │
      ├── PlayerMovement
      │
      ├── PlayerHealth
      │
      └── PlayerWeapon
```

这样以后加：

```text
Stamina
Armor
Inventory
Ability
Interaction
```

都不会把 `PlayerController` 搞成超级类。

---

# 武器建议单独设计

FPS里面武器会非常多：

```text
Weapon
 ├── AK47
 ├── M4
 ├── AWP
 ├── Pistol
 ├── Shotgun
 └── Grenade
```

所以不要：

```csharp
if (weaponType == AK47)
{
}

if (weaponType == AWP)
{
}

if (weaponType == Shotgun)
{
}
```

可以：

```text
Weapon/
├── Weapon.cs
├── WeaponData.cs
├── HitscanWeapon.cs
├── ProjectileWeapon.cs
├── ShotgunWeapon.cs
└── WeaponManager.cs
```

例如：

```csharp
public abstract class Weapon : NetworkBehaviour
{
    public abstract void Fire();
}
```

然后：

```csharp
public class HitscanWeapon : Weapon
{
    public override void Fire()
    {
        // AK / M4 / AWP
    }
}
```

```csharp
public class ProjectileWeapon : Weapon
{
    public override void Fire()
    {
        // RPG / 火箭弹
    }
}
```

---

# 数据和代码最好分开

这个对 FPS **非常重要**。

例如：

```text
WeaponData
```

不要把：

```csharp
damage = 30;
fireRate = 10;
magazineSize = 30;
reloadTime = 2.1f;
```

全部写死在代码里。

Unity 可以使用：

```text
ScriptableObject
```

例如：

```csharp
[CreateAssetMenu]
public class WeaponData : ScriptableObject
{
    public float damage;
    public float fireRate;
    public int magazineSize;
    public float reloadTime;
}
```

于是：

```text
Weapons/
├── AK47.asset
├── M4.asset
├── AWP.asset
├── Glock.asset
└── Shotgun.asset
```

代码：

```text
Weapon.cs
     │
     └── WeaponData
             │
             ├── Damage
             ├── FireRate
             ├── MagazineSize
             └── ReloadTime
```

这样你以后调枪械参数会舒服很多。

---

# 网络数据也最好独立

比如：

```text
Network/
└── Messages/
    ├── PlayerInputMessage.cs
    ├── FireMessage.cs
    ├── ReloadMessage.cs
    └── HitMessage.cs
```

不过这里有一个很关键的东西：

**不要把“网络同步数据”和“游戏逻辑数据”混在一起。**

比如：

```csharp
public struct PlayerInput
{
    public Vector2 move;
    public Vector2 look;
    public bool jump;
    public bool fire;
}
```

这是：

> **输入数据**

而：

```csharp
public struct PlayerState
{
    public Vector3 position;
    public Quaternion rotation;
    public int health;
}
```

这是：

> **状态数据**

两者不是一回事。

---

# 如果是你现在这个项目

结合你之前一直在研究的：

```text
Mirror
Dedicated Server
客户端预测
Server Authority
FPS
第三人称玩家
```

我建议你**不要一开始就把项目搞成上面那种几十个文件夹**。

第一版实际上这样就够了：

```text
Scripts/
│
├── Core/
│   ├── GameManager.cs
│   └── EventCenter.cs
│
├── Network/
│   ├── FPSNetworkManager.cs
│   ├── NetworkPlayer.cs
│   └── NetworkMessages/
│
├── Player/
│   ├── PlayerController.cs
│   ├── PlayerMovement.cs
│   ├── PlayerHealth.cs
│   └── PlayerInput.cs
│
├── Weapon/
│   ├── Weapon.cs
│   ├── WeaponData.cs
│   ├── WeaponManager.cs
│   └── HitscanWeapon.cs
│
├── Combat/
│   ├── DamageInfo.cs
│   ├── DamageSystem.cs
│   └── Hitbox.cs
│
├── Prediction/
│   ├── ClientPrediction.cs
│   └── Reconciliation.cs
│
└── UI/
    ├── HUD.cs
    ├── Crosshair.cs
    └── KillFeed.cs
```

等项目真的变大了，再拆：

```text
Client/
Server/
Presentation/
Gameplay/
```

**不要为了“架构漂亮”一开始就拆几十层。**

你现在最应该先把这条链跑通：

```text
输入
 ↓
Client Prediction
 ↓
发送 Input
 ↓
Dedicated Server
 ↓
Server Authority
 ↓
Movement / Shoot / Damage
 ↓
同步 State
 ↓
Client Reconciliation / Interpolation
```

这条链跑通以后，你这个 FPS 的网络架构基本就立住了。
