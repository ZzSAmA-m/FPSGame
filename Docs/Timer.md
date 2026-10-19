# Core.Timer API

命名空间：`Core.Timer`

源码：

- `Assets/Scripts/Core/Timer/Timer.cs`
- `Assets/Scripts/Core/Timer/TimerRegistry.cs`
- `Assets/Scripts/Core/Timer/TimerDriver.cs`（内部驱动，不作为调用入口）

计时器按固定秒数重复调用一个方法。多个计时器可以用字符串名字登记，再从别处取出来启动或停止。

## 用法

```csharp
using Core.Timer;

Timer tm = TimerRegistry.Register("PosePublish");
tm.Time = 1d / 60d;
tm.EventMethod = Publish;
tm.StartInvoke();
```

`StartInvoke` 之后，`Publish` 按每秒 60 次触发。停止：

```csharp
TimerRegistry.Get("PosePublish").StopInvoke();
```

间隔必须写成浮点除法。`1 / 60` 是整数除法，结果是 `0`，`StartInvoke` 会抛异常。使用 `1d / 60d` 或 `1f / 60f`。

第一次触发发生在启动后的一个间隔，不会在 `StartInvoke` 当帧立刻调用。

## Timer

一个计时器实例。

| 成员 | 说明 |
|---|---|
| `double Time` | 两次触发之间的秒数。运行中修改后，从下一次间隔开始使用新值。小于或等于 `0` 时不能启动。 |
| `Action EventMethod` | 到点调用的方法。可以是方法组，也可以是 lambda。为 `null` 时到点不调用，节拍仍然往前走。 |
| `int MaxCatchUp` | 一帧内最多补调用多少次。默认 `8`。设成小于 `1` 的值会被抬到 `1`。 |
| `bool IsRunning` | 是否正在触发。只读。 |
| `void StartInvoke()` | 开始按 `Time` 重复触发。 |
| `void StopInvoke()` | 停止后续触发。 |

`StartInvoke` 在 `Time <= 0` 时抛出 `ArgumentOutOfRangeException`。计时器已经在运行时再调用 `StartInvoke`，不会重新计时；要重设起点，先 `StopInvoke` 再 `StartInvoke`。

`EventMethod` 里抛出的异常会被接住并记到 Console，计时器继续运行。回调里可以调用 `StopInvoke`，当前这次调用会执行完，同帧内不会再补下一次。

不登记名字也可以直接使用：

```csharp
Timer tm = new Timer();
tm.Time = 0.5d;
tm.EventMethod = Publish;
tm.StartInvoke();
```

## TimerRegistry

按字符串保存计时器。名字在一次运行期内唯一。

| 成员 | 说明 |
|---|---|
| `Timer Register(string name)` | 创建计时器并登记，返回该实例。 |
| `void Register(string name, Timer timer)` | 登记一个已经创建的计时器。 |
| `Timer Get(string name)` | 按名字取得计时器。 |
| `bool TryGet(string name, out Timer timer)` | 按名字查找。找不到时返回 `false`，`timer` 为 `null`。 |
| `bool Unregister(string name)` | 停止并移除这个名字。名字不存在时返回 `false`。 |

`Register` 在名字为空或只含空白时抛出 `ArgumentException`。`timer` 为 `null` 时抛出 `ArgumentNullException`。名字已经存在时抛出 `InvalidOperationException`。

`Get` 在名字不存在时抛出 `InvalidOperationException`。不确定名字是否存在时用 `TryGet`。

`Unregister` 只从登记表移除并停止触发，不要求调用方再调用 `StopInvoke`。移除后同一个名字可以重新 `Register`。

```csharp
Timer created = new Timer();
created.Time = 1d / 60d;
created.EventMethod = Publish;
TimerRegistry.Register("PosePublish", created);

if (TimerRegistry.TryGet("PosePublish", out Timer tm))
{
    tm.StartInvoke();
}

TimerRegistry.Unregister("PosePublish");
```

## 时间精度

时钟是 `Time.unscaledTimeAsDouble`。`Time.timeScale` 设为 `0` 时，计时器仍然按真实经过的时间触发。

每一拍排在上一拍的绝对时间之后，长期触发次数与设定间隔一致，不会因为帧间隔波动慢慢变慢。某一帧来晚了，会在这一帧把欠下的调用补上，所以帧率高于或低于目标频率时，长期次数仍然对准。

补调用不超过 `MaxCatchUp`。一次卡顿超过这个上限时，中间欠下的拍被跳过，下一拍对齐到后面的节拍，避免之后每一帧都在补旧账。默认 `8` 表示：间隔为 `1/60` 秒时，单帧最多补约 `133` 毫秒的欠账。

触发对齐到帧。`unscaledTimeAsDouble` 在同一帧内不变，回调执行得更早或更晚不会改变这一帧读到的时间。实际调用时刻的误差不超过一帧。

第一次 `StartInvoke` 会创建名为 `TimerDriver` 的 `DontDestroyOnLoad` 对象，由它在 `Update` 里统一驱动所有正在运行的计时器。不需要把这个对象放进场景。
