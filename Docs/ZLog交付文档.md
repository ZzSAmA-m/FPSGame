# ZLog Channel / Frequency 交付文档

## 1. 文档信息

- 项目：FPSGame
- 技术栈：Unity 6（6000.5.0f1）、脚本后端 Mono（Windows 玩家构建）
- 交付日期：2026-10-25
- 本次范围：为 `ZLog` 的 `Log`、`LogWarning`、`LogError` 增加 Channel 与 Frequency，并在编辑器里按这两项过滤 Unity Console

本文是这次扩展的交付说明，也是之后加日志分类时的维护说明。

---

## 2. 交付结论

`Core.ZLog.ZLog` 的六个入口都多了两个可选参数：

- `LogChannel channel`：这条日志属于哪一类。默认 `LogChannel.Default`。
- `LogFrequency frequency`：这条日志的噪声等级。默认 `LogFrequency.Default`。

旧调用只传消息字符串，编译结果仍然是「默认分类 + 默认频率」，调用处不用改。

Unity Console 顶栏有一个 **ZLog** 按钮，点开就是过滤器。有分类或频率被关掉时，按钮文字变成 **ZLog***。取消勾选某个 Channel 或 Frequency 后，带有这一项的日志直接不打印，不会调用 `Debug.Log`。已经打进 Console 的旧日志留在原地，重新勾上也不会把之前没打印的日志补出来。玩家构建不读取这些勾选，默认全部输出。

Frequency 是打在单条消息上的等级标签，用来显隐，不会按帧数抽掉日志。需要少看到高频日志时，在调用处把那条日志标成 `High` 或 `Extreme`，再在窗口里关掉对应等级。

---

## 3. 接口

分类：

| 枚举 | 窗口上的名字 | 含义 |
| --- | --- | --- |
| `LogChannel.Default` | 默认 | 未归类 |
| `LogChannel.Input` | Input | 输入 |
| `LogChannel.Network` | Network | 网络 |
| `LogChannel.GamePlay` | GamePlay | 玩法 |

频率：

| 枚举 | 窗口上的名字 | 含义 |
| --- | --- | --- |
| `LogFrequency.Default` | 默认 | 未标注等级 |
| `LogFrequency.Low` | 低 | 低 |
| `LogFrequency.Medium` | 中 | 中 |
| `LogFrequency.High` | 高 | 高 |
| `LogFrequency.Extreme` | 极高 | 极高 |

普通入口与泛型入口参数相同。泛型入口仍会把类型名写进消息。

```csharp
ZLog.Log("客户端应用层已初始化");

ZLog.Log<ClientManager>(
    "收到服务器分发的ClientId: " + id,
    LogChannel.Network,
    LogFrequency.Medium);

ZLog.LogWarning("按键重复绑定", LogChannel.Input, LogFrequency.Low);
ZLog.LogError<ServerManager>("玩家对象缺少命令目标", LogChannel.Network);
```

`ZLog.Enable == false` 时整库直接返回，不再看分类和频率。

显隐状态也可以由代码改，编辑器窗口用的就是这组方法：

- `IsChannelVisible` / `SetChannelVisible` / `SetAllChannelsVisible`
- `IsFrequencyVisible` / `SetFrequencyVisible` / `SetAllFrequenciesVisible`

---

## 4. 消息格式

进入 Console 的文本固定带上分类和频率。

普通调用：

```text
[Network][High] 服务器应用层已初始化
```

泛型调用：

```text
[Network][Medium][ClientManager] 收到服务器分发的ClientId: 1
```

因此即使过滤窗口全部勾选，也能从正文看出这条日志被标成了什么。

---

## 5. 编辑器用法

打开 **Window > General > Console**。顶栏和 Clear、Collapse 同一排，搜索框左边有 **ZLog** 按钮。点它会在按钮下方打开过滤面板。

Console 宽度大约小于 420 时，Unity 会把 Error Pause、搜索框和这个按钮一起收起来。把 Console 拉宽后按钮会重新出现。

面板分 Channel、Frequency 两组。每组有「全选」和「全不选」。勾选表示显示，取消勾选表示不显示。鼠标停在某一项上时，提示里是枚举名。点到面板外面即关闭。

勾选写入 `EditorPrefs`，键名是 `ZLog.Channel.<枚举名>` 和 `ZLog.Frequency.<枚举名>`。脚本域重载后，`ZLogConsoleToolbar` 会把保存的勾选重新套到 `ZLog` 上，不打开面板也会生效，Play Mode 同样生效。

处理方式是不打印，不是先打印再隐藏。`ZLog` 在调用 `Debug.Log`、`Debug.LogWarning`、`Debug.LogError` 之前检查 Channel 和 Frequency，任意一项被关掉就直接返回。因此每帧打的位置日志，只要标成 `High` 或 `Extreme` 并在面板里关掉这一级，就不会进入 Console。

已经打进 Console 的旧日志不会被删掉，也不会在重新勾选后补打。直接调用 `Debug.Log` 的消息没有这两项，不受这个面板控制。

调用处如果先写成 `$"..."` 再传给 `ZLog`，字符串本身仍会在进入 `ZLog` 之前拼出来。不打印的是 Console 输出。

---

## 6. 玩家构建

Windows 玩家构建使用 Mono 时，过滤窗口和 `EditorPrefs` 都不会进包。`ZLog` 里的显隐掩码初始为全部可见。打包后的游戏会输出所有通过 `ZLog.Enable` 的日志，消息里仍然带 `[Channel][Frequency]` 前缀。

---

## 7. 现有调用

这次没有改现有调用点。它们现在都落在 `Default` / `Default` 上，例如：

- `ClientManager`、`ClientCommandPublisher`、`FPSNetworkManager`、`ServerManager` 的网络日志
- `ClientSessions` 的错误日志
- `ServerManagerDebugger` 的调试日志

之后如果希望在过滤窗口里单独关掉网络日志，需要在对应调用上传入 `LogChannel.Network`，并按噪声程度补上 `LogFrequency`。只改窗口、不改调用时，这些旧日志始终属于「默认 / 默认」。

---

## 8. 如何新增一类

1. 在 `LogChannel` 或 `LogFrequency` 末尾追加枚举值。不要改已有值的名字，`EditorPrefs` 按名字保存。
2. 在 `ZLogConsoleToolbar` 的 `ChannelLabel` 或 `FrequencyLabel` 里补上面板显示名。
3. 重新编译后，新项默认勾选（`EditorPrefs` 缺键时按显示处理）。

枚举值需要从 0 连续递增。`ZLog` 用枚举的整数值做显隐数组下标。

---

## 9. 涉及文件

- `Assets/Scripts/Core/ZLog/ZLog.cs`：六个日志入口、显隐掩码、消息格式
- `Assets/Scripts/Core/ZLog/LogChannel.cs`：分类枚举
- `Assets/Scripts/Core/ZLog/LogFrequency.cs`：频率枚举
- `Assets/Scripts/Core/ZLog/ZLog.asmdef`：运行时程序集，玩家构建会带上
- `Assets/Scripts/Core/ZLog/Editor/ZLog.Editor.asmdef`：只在编辑器编译，引用 `ZLog`
- `Assets/Scripts/Core/ZLog/Editor/ZLogConsoleFilterWindow.cs`：Console 顶栏按钮、过滤面板与 EditorPrefs
- `Docs/ZLog交付文档.md`：本文
