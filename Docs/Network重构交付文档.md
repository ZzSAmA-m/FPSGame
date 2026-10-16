# FPSGame Network 基础架构重构交付文档

## 1. 文档信息

- 项目：FPSGame
- 技术栈：Unity 6、Mirror 96.0.1、KCP Transport
- 交付日期：2026-10-12
- 本次范围：网络基础架构、连接会话、协议握手、联网玩家预制体统一、本地玩家生命周期清理
- 验证场景：`Assets/Scenes/SampleScene.unity`

本文既是本次重构的交付说明，也是后续继续开发网络功能时的维护指南。

---

## 2. 交付结论

本次重构已经把原先的“NetworkManager + 全局事件 + ClientManager 演示 Command/Rpc”样板，改造成以下结构：

```text
FPSNetworkManager
├── 负责 Mirror 生命周期与模块装配
├── NetBridge
│   ├── 注册/注销 NetworkMessage Handler
│   ├── 发送 HelloRequest
│   └── 发送 HelloResponse
└── ServerManager
    └── SessionRegistry
        └── ClientSession
```

最终运行链路如下：

```text
Transport 连接建立
    ↓
FPSNetworkManager.OnServerConnect
    ↓
SessionRegistry 创建 ClientSession
    ↓
客户端发送 HelloRequest
    ↓
ServerManager 验证协议版本
    ↓
Connected → ProtocolVerified
    ↓
Mirror Ready
    ↓
ProtocolVerified → Ready
    ↓
生成完整 Player.prefab
    ↓
Ready → Playing
```

同时，场景中的 `NetworkManager.playerPrefab` 已从只包含 `NetworkIdentity + ClientManager` 的空壳 `Client.prefab`，切换为包含完整移动、视角、武器、模型、动画与网络组件的 `Player.prefab`。

---

## 3. 本次解决的核心问题

### 3.1 网络生命周期依赖全局事件

重构前：

```text
FPSNetworkManager
    → EventCenter.ServerStartedEvent
    → FPSNetworkManager 自己订阅事件
    → Instantiate(Server.prefab)
```

这里存在两个问题：

1. 发送者和真正处理者其实是同一个对象，却绕了一次全局事件。
2. 初始化顺序被事件订阅隐藏，很难判断 `ServerManager` 是否已经存在。

重构后，`FPSNetworkManager` 直接创建和初始化 `ServerManager`：

```text
OnStartServer
    → CreateServerRuntime
    → ServerManager.Initialize(NetBridge)
    → NetBridge.StartServer
```

停止时严格执行逆序清理：

```text
OnStopServer
    → NetBridge.StopServer
    → ServerManager.Shutdown
    → Destroy(ServerManager)
```

### 3.2 把玩家组件列表当作连接管理

重构前 `ServerManager` 保存：

```csharp
List<ClientManager> Clients
```

这会混淆三个不同概念：

- `NetworkConnectionToClient`：Mirror 的服务器侧连接。
- 客户端会话：协议状态、认证状态和玩家绑定。
- 玩家网络对象：场景中的 `NetworkIdentity`。

重构后，这三者已经分开：

```text
NetworkConnectionToClient
    ↓
ClientSession
    ├── ConnectionId
    ├── ClientVersion
    ├── SessionState
    └── PlayerIdentity
```

以后加入账号认证时，应当把 `AccountId`、登录票据结果和重连信息放进 `ClientSession`，而不是继续扩展玩家 GameObject。

### 3.3 Command 在错误生命周期执行

旧 `ClientManager.Start()` 会直接调用 `[Command] Hello2Server()`。

`Start()` 会在服务器实例和客户端实例上执行，因此 Dedicated Server 上会出现：

```text
Command called ... without an active client
```

现在连接级握手完全迁移到 `NetBridge + NetworkMessage`，不再依赖某个玩家对象，也不要求该对象具有 authority。

旧 `ClientManager` 暂时保留为空兼容组件，目的是保留旧 prefab 的脚本 GUID，避免出现 Missing Script。新架构不再使用它。

### 3.4 联网玩家与玩法玩家是两套 prefab

重构前：

- `NetworkManager.playerPrefab` 指向 `Assets/Prefabs/Net/Client/Client.prefab`。
- 完整 FPS 组件位于 `Assets/Prefabs/Player/Player.prefab`。

因此玩家连接后只会生成一个网络空壳：

- 没有移动组件。
- 没有视角组件。
- 没有武器组件。
- 不会绑定 `PlayerInputDriver`。
- 不会挂载第一人称相机。

现在 `playerPrefab` 已指向完整 `Player.prefab`，玩家生成后能够执行完整本地玩家生命周期。

### 3.5 本地玩家退出时没有清理

重构前，本地玩家生成时会：

- 让 `PlayerInputDriver` 保存移动、视角和武器组件引用。
- 把场景相机挂到玩家相机点下面。

但玩家销毁时没有反向操作，可能导致：

- 输入服务继续引用已销毁组件。
- 场景相机作为玩家子物体被一并销毁。

现在生命周期是对称的：

```text
OnStartLocalPlayer
├── 绑定输入
├── 挂载相机
└── 切换 FP 模型

OnStopLocalPlayer
├── 解绑输入
├── 相机脱离玩家层级
└── Mirror 继续销毁玩家
```

---

## 4. 最终模块职责

### 4.1 FPSNetworkManager

文件：

`Assets/Scripts/Network/Common/FPSNetworkManager.cs`

职责：

- 作为 Mirror `NetworkManager` 的项目实现。
- 创建和关闭服务器运行时。
- 启动和停止 `NetBridge` 双端 Handler。
- 把连接、Ready、AddPlayer、Disconnect 回调转给 `ServerManager`。
- 维护进程角色：`None`、`Client`、`Server`、`Host`。
- 使用 Mirror 默认生成逻辑创建完整玩家 prefab。

不负责：

- 玩家列表的实际存储。
- 消息字段序列化细节。
- 移动、武器、伤害等玩法规则。
- 数据库存储。

### 4.2 NetBridge

文件：

`Assets/Scripts/Network/Common/NetBridge.cs`

职责：

- 注册服务端 `HelloRequest` Handler。
- 注册客户端 `HelloResponse` Handler。
- 提供明确的双端发送入口。
- 检查 NetworkClient / NetworkServer 是否可用。
- 把收到的消息转换为 C# 事件交给应用层。

关键原则：

> NetBridge 是协议边界，不是业务管理器。

不要把玩家扣血、生成武器、比赛计分等逻辑直接写入 NetBridge。

### 4.3 ServerManager

文件：

`Assets/Scripts/Network/Server/ServerManager.cs`

职责：

- 管理服务器应用层生命周期。
- 接收 NetBridge 的握手事件。
- 验证协议版本。
- 驱动 Session 状态转换。
- 处理玩家实体绑定和断开清理。
- 向客户端发送握手结果。

它不再依赖 `ClientManager`，也不再订阅 `EventCenter.NetworkEvent`。

### 4.4 SessionRegistry

文件：

`Assets/Scripts/Network/Server/Session/SessionRegistry.cs`

职责：

- 保证一个 `connectionId` 只对应一个会话。
- 按连接或 ID 查找会话。
- 幂等移除会话。
- 服务器关闭时统一清理。

`Sessions` 只暴露只读枚举，外部不能直接替换字典内容。

### 4.5 ClientSession

文件：

`Assets/Scripts/Network/Server/Session/ClientSession.cs`

当前记录：

- Mirror 连接对象。
- connectionId。
- 连接建立时间。
- 客户端构建版本。
- 当前 Session 状态。
- 已绑定的玩家 NetworkIdentity。

状态只能通过受控方法推进，外部不能任意设置。

### 4.6 ClientSessionState

文件：

`Assets/Scripts/Network/Server/Session/ClientSessionState.cs`

状态定义：

```text
Connected
    ↓ 协议版本通过
ProtocolVerified
    ↓ Mirror Ready
Ready
    ↓ AddPlayer 完成
Playing
    ↓ 连接退出
Disconnecting
```

非法转换会返回失败，`FPSNetworkManager` 会断开状态异常的连接。

---

## 5. 协议握手

### 5.1 协议常量

文件：

`Assets/Scripts/Network/Common/NetworkProtocol.cs`

当前版本：

```csharp
public const int Version = 1;
```

以下情况应递增协议版本：

- 删除消息字段。
- 改变字段含义。
- 改变数值单位。
- 把可靠消息改成不同处理语义。
- 新旧客户端无法安全互通。

只新增可选行为但仍兼容旧客户端时，可以根据实际兼容策略决定是否升级。

### 5.2 HelloRequest

文件：

`Assets/Scripts/Network/Common/Messages/HelloRequest.cs`

方向：

```text
Client → Server
```

字段：

- `protocolVersion`：项目协议版本。
- `clientVersion`：`Application.version`，只用于诊断。

安全说明：

- `clientVersion` 来自客户端，不能作为可信身份。
- 服务端会再次裁剪长度，不能只依赖客户端自己的裁剪。
- 普通 Handler 保持 `requireAuthentication=true`。

### 5.3 HelloResponse

文件：

`Assets/Scripts/Network/Common/Messages/HelloResponse.cs`

方向：

```text
Server → Client
```

字段：

- `protocolVersion`：服务器协议版本。
- `accepted`：是否允许继续。
- `message`：面向开发者的简短说明。

协议不一致时，服务器会发送拒绝响应并断开连接。

### 5.4 为什么不用 Command

握手是“连接级”行为，并不属于某个玩家实体。

如果使用 Command，会产生以下依赖：

- 必须先生成 NetworkIdentity。
- 必须拥有该对象 authority。
- 连接校验反而发生在 AddPlayer 之后。

使用 `NetworkMessage` 后可以在生成玩家之前完成协议检查：

```text
Connect → Hello → Ready → AddPlayer
```

---

## 6. 玩家生命周期

### 6.1 PlayerController

文件：

`Assets/Scripts/GamePlay/Player/PlayerController.cs`

旧实现依赖 `Start()` 中读取 `isLocalPlayer`。

新实现使用 Mirror 生命周期：

- `OnStartClient()`：每个客户端上的每个玩家副本都会执行。
  - 广播玩家生成。
  - 默认切换到 TP 模型。
- `OnStartLocalPlayer()`：仅本地玩家执行。
  - 广播本地玩家生成。
  - 绑定 `PlayerInputDriver`。
  - 切换到 FP 模型。
- `OnStopLocalPlayer()`：仅本地玩家退出时执行。
  - 解绑 `PlayerInputDriver`。

这样不会依赖 Unity `Start` 与 Mirror Spawn 回调之间的隐式顺序。

### 6.2 PlayerCameraController

文件：

`Assets/Scripts/GamePlay/Player/PlayerCameraController.cs`

现在只在 `OnStartLocalPlayer()` 挂载相机，在 `OnStopLocalPlayer()` 通知相机脱离。

远端玩家不会修改本机相机。

### 6.3 PlayerInputDriver

文件：

`Assets/Scripts/GamePlay/Player/PlayerInputDriver.cs`

新增行为：

- 保存当前 `_boundPlayer`。
- 绑定时检查玩家对象和必要组件。
- 玩家退出时清空移动、视角和武器引用。
- 只响应当前绑定玩家的解绑事件。

最后一条非常重要：如果旧玩家的退出事件晚于新玩家的绑定事件，它不会误清理新玩家。

### 6.4 CameraController

文件：

`Assets/Scripts/GamePlay/Camera/CameraController.cs`

新增行为：

- `Awake` 保存相机原父节点。
- 挂载时重置局部位置与局部旋转。
- 本地玩家退出时使用 `SetParent` 将相机移回场景层级。

相机是场景服务，不是玩家 prefab 的所有物。

---

## 7. EventCenter 调整

文件：

`Assets/Scripts/Core/Event/EventCenter.cs`

已删除：

- `NetworkEvent.ServerStartedEvent`
- `NetworkEvent.OnServerAddPlayerEvent`
- 对 `Mirror` 和 `ClientManager` 的反向引用

新增：

- `GamePlayEvent.CameraDetachEvent`
- `InputEvent.UnbindPlayerFromInputDriverEvent`

网络核心生命周期现在使用直接方法调用；玩法表现仍可使用事件解耦。

建议延续以下规则：

- 关键控制流使用明确接口或方法调用。
- 一对多表现通知可以使用事件。
- 事件订阅必须有对称取消订阅。
- 不要使用 EventCenter 隐藏认证、状态迁移和资源修改。

---

## 8. 场景与 prefab 变更

### 8.1 SampleScene

文件：

`Assets/Scenes/SampleScene.unity`

NetworkManager 对象新增：

```text
Network.Common.NetBridge
```

`playerPrefab` 从：

```text
Assets/Prefabs/Net/Client/Client.prefab
GUID: ebcf3587fbc6d5442bb31a3f0ef2da25
```

切换为：

```text
Assets/Prefabs/Player/Player.prefab
GUID: 6ba07c36370c4394bb887d4cba5fa9ad
```

保持不变：

- KCP Transport。
- 端口 7777。
- NetworkManagerHUD。
- 两个 NetworkStartPosition。
- Server prefab 引用。
- `autoCreatePlayer=true`。
- RoundRobin 出生方式。

### 8.2 Player.prefab

本次没有重写完整 Player prefab 的内部序列化内容，只把它设置为唯一联网玩家入口。

它当前已经包含：

- `NetworkIdentity`
- `NetworkTransformReliable`
- `PlayerController`
- `PlayerMovementController`
- `PlayerSightController`
- `PlayerWeaponController`
- `PlayerCameraController`
- FP / TP 模型及动画相关组件

### 8.3 旧 Client.prefab

`Assets/Prefabs/Net/Client/Client.prefab` 暂未删除。

原因：

- 用户工作区当前有大量未提交资源变更。
- 直接删除旧 prefab 与脚本可能影响尚未发现的临时场景或个人测试。
- 保留 `ClientManager` 的脚本 GUID 可以避免 Missing Script。

确认所有场景都不再引用后，可以在单独清理提交中删除：

- `Assets/Prefabs/Net/Client/Client.prefab`
- `Assets/Scripts/Network/Client/ClientManager.cs`

---

## 9. 逐文件变更清单

### 新增

- `Assets/Scripts/Network/Common/NetworkProtocol.cs`
  - 项目协议版本和字符串长度限制。
- `Assets/Scripts/Network/Common/Messages/HelloResponse.cs`
  - 服务器握手响应。
- `Assets/Scripts/Network/Server/Session/ClientSessionState.cs`
  - 会话状态机枚举。
- `Assets/Scripts/Network/Server/Session/ClientSession.cs`
  - 单连接会话模型。
- `Assets/Scripts/Network/Server/Session/SessionRegistry.cs`
  - 会话唯一注册表。
- `Docs/Network重构交付文档.md`
  - 本文档。

### 重写或显著修改

- `Assets/Scripts/Network/Common/NetBridge.cs`
  - 从 Awake 单次注册空 Handler 改为对称的双端消息网关。
- `Assets/Scripts/Network/Common/FPSNetworkManager.cs`
  - 改为网络组合根和生命周期适配器。
- `Assets/Scripts/Network/Server/ServerManager.cs`
  - 从 ClientManager 列表改为 Session 应用服务。
- `Assets/Scripts/Network/Common/Messages/HelloRequest.cs`
  - 增加命名空间、协议版本和客户端构建版本。
- `Assets/Scripts/Network/Client/ClientManager.cs`
  - 移除错误 Command/Rpc 演示，保留兼容空组件。
- `Assets/Scripts/Core/Event/EventCenter.cs`
  - 移除网络反向依赖，补充解绑事件。
- `Assets/Scripts/Core/Utility/Enums.cs`
  - `NetState` 新增 Host 并补充语义注释。
- `Assets/Scripts/GamePlay/Player/PlayerController.cs`
  - 使用 Mirror 玩家生命周期。
- `Assets/Scripts/GamePlay/Player/PlayerCameraController.cs`
  - 使用本地玩家生命周期挂载/释放相机。
- `Assets/Scripts/GamePlay/Player/PlayerInputDriver.cs`
  - 增加安全绑定与解绑。
- `Assets/Scripts/GamePlay/Camera/CameraController.cs`
  - 增加相机脱离逻辑。
- `Assets/Scenes/SampleScene.unity`
  - 添加 NetBridge，切换完整 Player prefab。

---

## 10. 代码注释策略

本次新增与修改代码采用以下注释原则：

1. 公共类型和公共方法使用中文 XML 注释。
2. 生命周期方法说明“何时由谁调用”。
3. 非显然判断解释“为什么”，例如：
   - 为什么握手必须早于 Ready。
   - 为什么 Stop 回调不能直接相信 active 标记。
   - 为什么解绑必须检查当前绑定玩家。
4. 不给普通赋值、花括号和显然代码增加逐行噪声注释。
5. 对过渡设计明确写出未来删除条件。

维护者应继续遵循这一风格：让注释补充代码无法自然表达的上下文，而不是把代码重新翻译成中文。

---

## 11. 验证记录

### 11.1 编译验证

使用 Unity Pipeline 强制重新编译：

```text
recompile_status:
status = completed
failed = false
errors = []
```

结果：

- 编译成功。
- 最终错误日志数量为 0。

### 11.2 Host 启动验证

验证结果：

```text
NetState = Host
ServerManager.ClientCount = 1
NetworkClient.localPlayer.name = Player [connId=0]
```

会话日志顺序：

```text
服务器应用层已初始化
建立 session(state=Connected)
协议握手通过(state=ProtocolVerified)
客户端 Ready(state=Ready)
玩家实体绑定(state=Playing)
客户端收到 HelloResponse
```

### 11.3 玩家预制体验证

运行时查找到：

```text
/Player [connId=0]
Component: GamePlay.Player.PlayerController
```

证明生成的是完整 `Player.prefab`，而不是旧 `Client.prefab`。

### 11.4 输入绑定验证

Host 运行期间，`PlayerInputDriver` 的三个引用均指向：

```text
/Player [connId=0]
```

包括：

- PlayerMovementController
- PlayerSightController
- PlayerWeaponController

### 11.5 相机挂载验证

Host 运行期间相机层级：

```text
/Player [connId=0]/FPModelParent///Camera_Point///Camera
```

说明只有本地玩家完成相机挂载。

### 11.6 Host 停止验证

停止结果：

```text
NetState = None
```

输入引用：

```text
_playerMovement = null
_playerSight = null
_playerWeapon = null
```

相机仍然存在，并已回到场景层级：

```text
///Camera
```

最终 Unity Error 日志：

```text
total = 0
```

### 11.7 尚未覆盖的验证

本次自动验证只覆盖单 Editor Host 生命周期，没有覆盖：

- 独立 Dedicated Server 构建。
- 第二个远程客户端。
- KCP 真实网络丢包与断线。
- 场景切换。
- 协议版本不一致客户端。
- Android / WebGL 平台编译。

这些项目应在进入服务端权威移动阶段前补充。

---

## 12. 当前安全边界与已知限制

### 12.1 当前仍是客户端到服务端位置同步

`Player.prefab` 上的 `NetworkTransformReliable` 当前配置为：

```text
syncDirection = ClientToServer
```

这只是让多人位置同步先跑通的过渡方案，不是竞技 FPS 的最终方案。

风险：

- 客户端可以修改 Transform。
- 服务端没有验证速度、加速度和碰撞。
- 无客户端预测/服务端校正。
- Reliable 位置流量可能产生队头阻塞。

下一阶段必须替换为：

```text
客户端发送 InputCommand
    ↓
服务端固定 Tick 模拟
    ↓
服务端发送权威 Snapshot
    ↓
客户端 Prediction + Reconciliation
```

### 12.2 当前握手不是账号认证

协议握手只能确认：

- 客户端消息结构版本兼容。
- 基础连接顺序合法。

它不能确认：

- 玩家账号身份。
- 登录票据真伪。
- 是否属于目标比赛。
- 是否被封禁。

真正的登录认证应接入 Mirror `NetworkAuthenticator` 或独立认证流程，并把认证结果写入 `ClientSession`。

### 12.3 当前没有重连

连接断开后会立即移除 Session 并销毁 Mirror 玩家对象。

未来重连需要：

- AccountId / PlayerId。
- 有时限的 Reconnecting 状态。
- 重连票据。
- 原玩家实体重新绑定。
- 超时清理策略。

### 12.4 当前没有自动化测试程序集

项目当前业务脚本都位于预定义 `Assembly-CSharp`，没有项目级 asmdef。

如果直接创建 asmdef 测试程序集，会涉及程序集依赖重组，不适合混入本次网络基础重构。

本次使用 Unity Pipeline 完成编译和 Host 集成验证。后续建议单独建立：

- `FPSGame.Core.asmdef`
- `FPSGame.Network.asmdef`
- `FPSGame.Network.Tests.asmdef`

再为 Session 状态机和协议验证增加 EditMode 测试。

---

## 13. 如何增加新的网络消息

以“客户端请求服务器时间”为例。

### 第一步：定义消息

放入：

```text
Assets/Scripts/Network/Common/Messages/
```

```csharp
public struct ServerTimeRequest : NetworkMessage
{
    public uint requestId;
}

public struct ServerTimeResponse : NetworkMessage
{
    public uint requestId;
    public double serverTime;
}
```

消息应满足：

- 使用 struct。
- 实现 `NetworkMessage`。
- 字段数量有限。
- 字符串和数组有明确长度限制。
- 不包含 MonoBehaviour、GameObject 或数据库对象。

### 第二步：在 NetBridge 注册

在 `StartServer()` 注册：

```csharp
NetworkServer.RegisterHandler<ServerTimeRequest>(HandleServerTimeRequest);
```

在 `StopServer()` 对称注销：

```csharp
NetworkServer.UnregisterHandler<ServerTimeRequest>();
```

客户端响应同理。

### 第三步：NetBridge 只转发

不要在 Handler 中实现业务：

```csharp
private void HandleServerTimeRequest(
    NetworkConnectionToClient connection,
    ServerTimeRequest request)
{
    ServerTimeRequested?.Invoke(connection, request);
}
```

### 第四步：ServerManager 或专用服务处理

业务层负责：

- 检查 Session 是否存在。
- 检查 Session 状态是否允许。
- 限制调用频率。
- 生成响应。
- 通过 NetBridge 发送。

### 第五步：确定可靠性

低频请求响应可以使用可靠通道。

未来玩家输入、位置快照等高频且可被新数据覆盖的消息，应优先考虑不可靠通道。

---

## 14. 何时使用 NetworkMessage、Command、Rpc、SyncVar

### 使用 NetworkMessage

适合：

- 登录和协议握手。
- 大厅、匹配、聊天。
- 输入流和快照。
- 不属于某个 NetworkIdentity 的请求。

### 使用 Command

适合：

- 客户端对自己拥有 authority 的网络对象发送低频意图。
- 行为天然属于某个 NetworkIdentity。

不要在普通 `Start()` 中调用 Command。优先使用：

- `OnStartLocalPlayer`
- `OnStartAuthority`
- 明确的用户操作

### 使用 TargetRpc

适合：

- 服务器通知某个具体客户端。
- 通知与某个 NetworkBehaviour 语义相关。

### 使用 ClientRpc

适合：

- 服务器通过某个网络实体广播低频表现事件。

不要用 ClientRpc 持续发送每帧 Transform。

### 使用 SyncVar

适合：

- 血量。
- 队伍。
- 当前武器。
- 回合状态。
- 低频且需要晚加入客户端获得当前值的状态。

---

## 15. 故障排查

### 客户端连接后立即断开

检查日志：

```text
[ServerManager] 拒绝连接
```

重点检查：

- `NetworkProtocol.Version` 是否一致。
- HelloRequest 是否早于 Ready。
- Session 是否成功在 `OnServerConnect` 创建。

### 报 Ready 但没有完成协议握手

确认 `FPSNetworkManager.OnClientConnect()` 中：

```text
SendHelloToServer
```

位于：

```text
base.OnClientConnect
```

之前。

这依赖 Mirror 可靠消息的发送顺序。

### 玩家生成但不能控制

检查：

- `SampleScene` 的 playerPrefab 是否仍是完整 Player.prefab。
- `PlayerController.OnStartLocalPlayer()` 是否执行。
- `PlayerInputDriver` 三个引用是否非空。
- Player prefab 是否包含 Movement / Sight / Weapon。

### 本地玩家退出后相机消失

检查：

- `PlayerCameraController.OnStopLocalPlayer()` 是否执行。
- `CameraDetachEvent` 是否有订阅者。
- `CameraController` 是否在 `OnEnable` 订阅。
- 相机对象是否在玩家销毁前脱离层级。

### 重复启动出现 Handler replacing 警告

检查：

- 是否绕过 `FPSNetworkManager` 直接调用 `NetBridge.StartServer()`。
- 上一次 StopServer 是否正常执行。
- 是否有第二个 NetBridge 注册同一种消息。

---

## 16. 推荐的下一阶段

下一阶段应聚焦“服务端权威移动”，而不是继续增加更多 Rpc。

建议顺序：

1. 定义 `PlayerInputCommand`：
   - sequence
   - clientTick
   - move
   - look
   - jump / fire 等按钮位
2. 客户端只为本地玩家采样输入。
3. NetBridge 或专用 Input Gateway 使用不可靠通道发送。
4. 服务端按 Session 找到 PlayerEntity。
5. 建立固定服务器 Tick。
6. 服务端验证并模拟移动。
7. 定义 `PlayerSnapshot`。
8. 客户端实现本地预测和 reconciliation。
9. 远端玩家使用插值。
10. 删除 `NetworkTransformReliable` 的客户端权威位置同步。

完成权威移动后，再进入：

- 服务端武器状态机。
- 服务端命中判定。
- 血量 SyncVar。
- 死亡和重生。
- 延迟补偿。

---

## 17. 交付边界总结

本次已经完成的是：

- 可维护的网络模块生命周期。
- 集中的 Mirror 消息边界。
- 明确的客户端会话状态机。
- 协议版本握手。
- 合法顺序：Connect → Hello → Ready → AddPlayer。
- 完整 FPS Player 作为联网玩家。
- 本地/远端模型生命周期。
- 输入与相机的对称绑定/解绑。
- Host 启停集成验证。
- 详细代码注释与交付文档。

本次没有把“能同步移动”包装成“服务端权威”。

当前 `NetworkTransformReliable ClientToServer` 仍然是明确记录的过渡技术债。后续所有竞技性玩法都应建立在服务端权威输入模拟上，而不是继续相信客户端 Transform、命中或伤害结果。
