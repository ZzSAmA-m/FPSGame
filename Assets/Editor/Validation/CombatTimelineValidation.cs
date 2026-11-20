using System;
using System.Reflection;
using System.Text;
using Core.Input;
using GamePlay.Player;
using GamePlay.Player.Combat;
using GamePlay.Player.Presentation;
using GamePlay.Player.Simulation;
using Mirror;
using Network.Client;
using Network.Common;
using Network.Common.Messages;
using Unity.Pipeline.Commands;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FPSGame.Editor.Validation
{
    /// <summary>隔离验证公共显示时间、实际输入视图基准和命中确认路由。</summary>
    public static class CombatTimelineValidation
    {
        private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

        [CliCommand("combat_timeline_validate", "Validate shared combat presentation time, input capture and hit confirmation routing", Tags = new[] { "fpsgame/validation" })]
        public static string Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return "REFUSED：请先退出 Play Mode，再验证战斗公共时间轴。";

            var report = new StringBuilder();
            int passed = 0;
            RunCase("缺少时钟基准时保持未初始化，旧锚点与非法值被拒绝", TestAnchors, report, ref passed);
            RunCase("统一扣除 30 ms，显示时间与快照两端共同提交", TestPresentationTime, report, ref passed);
            RunCase("短点击保留采样时刻，连续补算沿用同屏时间与快照对", TestInputCapture, report, ref passed);
            RunCase("预测重同步保留世界时间，缺少世界时钟的输入明确标记 NaN", TestPredictionReset, report, ref passed);
            RunCase("远端共享呈现 Tick，快照对分歧时保持上一已呈现基准", TestCommonBracket, report, ref passed);
            RunCase("远端重生与传送切换连续段，迟到旧段不能重新参与插值", TestContinuity, report, ref passed);
            RunCase("命中广播按射击 ID 去重，仅向本地射手触发专属确认", TestHitConfirmation, report, ref passed);
            RunCase("输入视图、公共骨骼与连续段、时钟锚点及命中结果经过 Mirror 完整编解码", TestWireRoundTrip, report, ref passed);
            return "PASS " + passed + " checks\n" + report;
        }

        private static void TestAnchors()
        {
            var timeline = new ClientWorldTimeline();
            Require(!timeline.TryGetRenderTick(10d, out _) && double.IsNaN(timeline.LastPresentedServerTick),
                "收到服务器锚点前不能构造有效视图时间。");
            timeline.MarkPresented(100d, 100, 100);
            Require(double.IsNaN(timeline.LastPresentedServerTick), "无锚点时不能提交呈现记录。");
            Require(!timeline.ObserveAnchor(100, double.NaN) && !timeline.ObserveAnchor(100, -1d),
                "锚点必须具有有效的服务器时间。");
            Require(timeline.ObserveAnchor(100, 10d), "首个有效锚点应被接受。");
            Require(!timeline.ObserveAnchor(99, 11d) && !timeline.ObserveAnchor(100, 11d),
                "乱序和重复锚点不能重写当前时钟映射。");
            Require(!timeline.TryGetRenderTick(double.PositiveInfinity, out _), "非法当前时间不能生成 Tick。");
        }

        private static void TestPresentationTime()
        {
            var timeline = new ClientWorldTimeline();
            timeline.ObserveAnchor(100, 10d);
            Require(timeline.TryGetRenderTick(10.03d, out double tick) && Near(tick, 100d),
                "同一服务器时钟域应仅扣除项目的 30 ms 显示缓冲。");
            timeline.MarkPresented(101d, 100, 102);
            Require(timeline.LastPresentedServerTick == 101d && timeline.LastPresentedFromTick == 100 &&
                    timeline.LastPresentedToTick == 102, "显示 Tick 与两端快照必须一起提交。");
            timeline.MarkPresented(100d, 100, 100);
            timeline.MarkPresented(103d, 100, 102);
            timeline.MarkPresented(102d, 103, 101);
            Require(timeline.LastPresentedServerTick == 101d && timeline.LastPresentedToTick == 102,
                "倒退、区间外或倒置的快照对不能覆盖已呈现状态。");
            Require(timeline.TryGetRenderTick(9d, out tick) && tick == 101d,
                "同步时钟短暂倒退不能令公共呈现时间倒退。");
            timeline.Reset();
            Require(!timeline.HasAnchor && double.IsNaN(timeline.LastPresentedServerTick) &&
                    timeline.LastPresentedFromTick == 0 && timeline.LastPresentedToTick == 0,
                "连接关闭应完整清理显示基准。");
        }

        private static void TestInputCapture()
        {
            using var scope = new Scope();
            PlayerInputDriver driver = scope.CreateInput();
            scope.Client.WorldTimeline.ObserveAnchor(110, 10d);
            scope.Client.WorldTimeline.MarkPresented(100d, 100, 100);
            driver.Consume(0);
            scope.Client.WorldTimeline.MarkPresented(101d, 100, 102);
            Invoke(driver, "Accumulate", new PlayerInput { lMouse = true });
            scope.Client.WorldTimeline.MarkPresented(102d, 102, 102);
            Invoke(driver, "Accumulate", new PlayerInput());
            PlayerInputFrame click = driver.Consume(1);
            Require(click.fire && click.viewServerTick == 101d && click.viewFromTick == 100 && click.viewToTick == 102,
                "跨渲染帧保留的短点击必须携带最初点击时的完整视图基准。");
            PlayerInputFrame released = driver.Consume(2);
            Require(!released.fire && released.viewServerTick == 102d && released.viewFromTick == 102 && released.viewToTick == 102,
                "松开后的下一拍应恢复使用最近采样的视图基准。");
            scope.Client.WorldTimeline.MarkPresented(103d, 102, 104);
            Invoke(driver, "Accumulate", new PlayerInput { lMouse = true });
            PlayerInputFrame heldFirst = driver.Consume(3);
            PlayerInputFrame heldSecond = driver.Consume(4);
            Require(heldFirst.fire && heldSecond.fire && heldFirst.viewServerTick == 103d &&
                    heldSecond.viewServerTick == 103d && heldSecond.viewFromTick == 102 && heldSecond.viewToTick == 104,
                "同一渲染帧补算多拍不能为持续开火伪造不同的呈现时间。");
        }

        private static void TestPredictionReset()
        {
            using var scope = new Scope();
            scope.Client.WorldTimeline.ObserveAnchor(110, 10d);
            scope.Client.WorldTimeline.MarkPresented(105d, 104, 106);
            var prediction = scope.CreateRoot("Prediction").AddComponent<PlayerPrediction>();
            prediction.ResetPrediction();
            scope.Client.ResetSimulationClock();
            Require(scope.Client.LastPresentedServerTick == 105d && scope.Client.LastPresentedFromTick == 104 &&
                    scope.Client.LastPresentedToTick == 106,
                "预测输入轮次重置不应清除服务器公共显示时间。");
            PlayerInputDriver driver = scope.CreateInput();
            scope.Client.WorldTimeline.Reset();
            PlayerInputFrame input = driver.Consume(1);
            Require(double.IsNaN(input.viewServerTick) && input.viewFromTick == 0 && input.viewToTick == 0,
                "尚未取得有效公共时钟时应发送明确的不可用视图基准。");
        }

        private static void TestCommonBracket()
        {
            using var scope = new Scope();
            RemotePoseInterpolator first = scope.CreateRemote(1);
            RemotePoseInterpolator second = scope.CreateRemote(2);
            first.Push(100, new PlayerSimState { position = Vector3.zero }, 1);
            first.Push(102, new PlayerSimState { position = new Vector3(2f, 0f, 0f) }, 1);
            second.Push(100, new PlayerSimState { position = new Vector3(10f, 0f, 0f) }, 1);
            second.Push(101, new PlayerSimState { position = new Vector3(11f, 0f, 0f) }, 1);
            second.Push(102, new PlayerSimState { position = new Vector3(12f, 0f, 0f) }, 1);
            double serverTime = NetworkTime.time + (NetworkServer.active ? 0d : NetworkClient.bufferTime);
            scope.Client.WorldTimeline.ObserveAnchor(102, serverTime);
            scope.Client.WorldTimeline.MarkPresented(100d, 100, 100);
            Invoke(scope.Client, "Update");
            Invoke(scope.Client, "LateUpdate");
            Require(scope.Client.LastPresentedServerTick == 100d && double.IsNaN(first.PresentedTick),
                "快照对分歧必须在修改任一远端角色之前停止该帧的呈现推进。");

            Invoke(second, "OnDisable");
            second.Push(100, new PlayerSimState { position = new Vector3(10f, 0f, 0f) }, 1);
            second.Push(102, new PlayerSimState { position = new Vector3(12f, 0f, 0f) }, 1);
            Invoke(scope.Client, "Update");
            Invoke(scope.Client, "LateUpdate");
            Require(first.PresentedTick == second.PresentedTick && first.PresentedTick == scope.Client.LastPresentedServerTick &&
                    scope.Client.LastPresentedFromTick == 100 && scope.Client.LastPresentedToTick == 102 &&
                    Near(second.transform.position.x - first.transform.position.x, 10d),
                "快照对一致后应统一呈现并提交相同的时间与快照两端。");
            Require(first.Present(102d, out uint from, out uint to) && from == 102 && to == 102,
                "恰好位于快照时刻时应使用同一帧构成的快照对。");
        }

        private static void TestHitConfirmation()
        {
            using var scope = new Scope();
            NetworkIdentity localPlayer = scope.CreateRoot("CombatTimelineLocal").AddComponent<NetworkIdentity>();
            typeof(NetworkIdentity).GetProperty(nameof(NetworkIdentity.netId)).SetValue(localPlayer, 3u);
            PropertyInfo localPlayerProperty = typeof(NetworkClient).GetProperty(nameof(NetworkClient.localPlayer));
            NetworkIdentity previousLocalPlayer = NetworkClient.localPlayer;
            int received = 0;
            int confirmed = 0;
            scope.Client.HitReceived += _ => received++;
            scope.Client.HitConfirmed += _ => confirmed++;
            var hit = new HitConfirmMessage
            {
                shotId = 10, serverTick = 100, shooterNetId = 1, targetNetId = 2,
                region = HitRegion.Head, point = new Vector3(1f, 2f, 3f), killed = true
            };
            try
            {
                localPlayerProperty.SetValue(null, localPlayer);
                Invoke(scope.Bridge, "HandleHitConfirm", hit);
                Invoke(scope.Bridge, "HandleHitConfirm", hit);
                Require(received == 1 && confirmed == 0, "观察者应仅收到一次公共命中。");

                hit.shooterNetId = localPlayer.netId;
                hit.shotId = 9;
                Invoke(scope.Bridge, "HandleHitConfirm", hit);
                hit.shotId = 11;
                hit.region = HitRegion.None;
                Invoke(scope.Bridge, "HandleHitConfirm", hit);
                hit.region = HitRegion.Body;
                hit.shooterNetId = 0;
                Invoke(scope.Bridge, "HandleHitConfirm", hit);
                hit.shooterNetId = hit.targetNetId;
                Invoke(scope.Bridge, "HandleHitConfirm", hit);
                hit.shooterNetId = localPlayer.netId;
                hit.point = new Vector3(float.NaN, 0f, 0f);
                Invoke(scope.Bridge, "HandleHitConfirm", hit);
                Require(received == 1 && confirmed == 0, "旧消息和无效命中不得进入任一订阅入口。");

                hit.point = Vector3.one;
                Invoke(scope.Bridge, "HandleHitConfirm", hit);
                Invoke(scope.Bridge, "HandleHitConfirm", hit);
                Require(received == 2 && confirmed == 1, "本地射手应各收到一次公共命中和专属确认。");

                scope.Client.ShutDown();
                hit.shotId = 12;
                Invoke(scope.Bridge, "HandleHitConfirm", hit);
                Require(received == 2 && confirmed == 1, "客户端关闭后必须解除命中消息路由。");
            }
            finally
            {
                localPlayerProperty.SetValue(null, previousLocalPlayer);
            }
        }

        private static void TestWireRoundTrip()
        {
            Require(NetworkProtocol.Version >= 9, "包含射手身份的公共命中消息需要协议版本至少为 9。");
            using NetworkWriterPooled writer = NetworkWriterPool.Get();
            var input = new PlayerInputMessage
            {
                predictionEpoch = 3,
                frames = new[] { new PlayerInputFrame { tick = 8, fire = true, viewServerTick = 100.25d, viewFromTick = 100, viewToTick = 102 } }
            };
            NetworkMessages.Pack(input, writer);
            using NetworkReaderPooled inputReader = NetworkReaderPool.Get(writer.ToArraySegment());
            Require(NetworkMessages.UnpackId(inputReader, out ushort id) && id == NetworkMessageId<PlayerInputMessage>.Id,
                "输入消息必须保留自己的 Mirror 消息编号。");
            PlayerInputMessage decoded = inputReader.Read<PlayerInputMessage>();
            Require(decoded.predictionEpoch == 3 && decoded.frames.Length == 1 && decoded.frames[0].fire &&
                    decoded.frames[0].viewServerTick == 100.25d && decoded.frames[0].viewFromTick == 100 &&
                    decoded.frames[0].viewToTick == 102 && inputReader.Remaining == 0,
                "视图 Tick 和快照对必须完整经过实际网络编码。");
            writer.Reset();
            var hit = new HitConfirmMessage
            {
                shotId = 0x0102030405060708UL, serverTick = 200, shooterNetId = 3, targetNetId = 7,
                region = HitRegion.Body, point = new Vector3(1f, 2f, 3f), killed = true
            };
            NetworkMessages.Pack(hit, writer);
            using NetworkReaderPooled hitReader = NetworkReaderPool.Get(writer.ToArraySegment());
            Require(NetworkMessages.UnpackId(hitReader, out id) && id == NetworkMessageId<HitConfirmMessage>.Id,
                "命中确认必须具有独立的 Mirror 消息编号。");
            HitConfirmMessage decodedHit = hitReader.Read<HitConfirmMessage>();
            Require(decodedHit.shotId == hit.shotId && decodedHit.serverTick == hit.serverTick &&
                    decodedHit.shooterNetId == hit.shooterNetId && decodedHit.targetNetId == hit.targetNetId &&
                    decodedHit.region == hit.region &&
                    decodedHit.point == hit.point && decodedHit.killed && hitReader.Remaining == 0,
                "权威命中结果的全部字段必须完整往返。");
            writer.Reset();
            var pose = new PlayerCombatPose
            {
                valid = true, headPosition = new Vector3(0.1f, 1.7f, 0.2f),
                pelvisPosition = Vector3.up, headRotation = Quaternion.Euler(30, 90, 0),
                pelvisRotation = Quaternion.identity
            };
            var snapshot = new PlayerSnapshotMessage
            {
                serverTick = 300, serverTime = 10.25d,
                players = new[] { new PlayerSnapshotPose { netId = 7, predictionEpoch = 3,
                    combatPose = pose, combatContinuity = 5 } }
            };
            NetworkMessages.Pack(snapshot, writer);
            using NetworkReaderPooled snapshotReader = NetworkReaderPool.Get(writer.ToArraySegment());
            Require(NetworkMessages.UnpackId(snapshotReader, out id) && id == NetworkMessageId<PlayerSnapshotMessage>.Id,
                "公共快照应保留消息编号。");
            var decodedSnapshot = snapshotReader.Read<PlayerSnapshotMessage>();
            Require(decodedSnapshot.serverTick == 300 && decodedSnapshot.serverTime == 10.25d &&
                    decodedSnapshot.players.Length == 1 && decodedSnapshot.players[0].combatContinuity == 5 &&
                    decodedSnapshot.players[0].combatPose.Equals(pose) && snapshotReader.Remaining == 0,
                "公共姿态、连续段与时钟锚点必须完整往返。");
        }

        private static void TestContinuity()
        {
            using var scope = new Scope();
            var remote = scope.CreateRemote(10);
            var before = new PlayerSimState { position = Vector3.zero };
            var after = new PlayerSimState { position = Vector3.right * 10f };
            remote.Push(100, before, 1, default, 1);
            remote.Push(102, after, 1, default, 2);
            Require(!remote.Present(101), "不能显示传送或重生两侧之间不存在的中间位置。");
            Require(remote.TryGetRange(out double oldest, out double latest) && oldest == 102 && latest == 102,
                "切换连续段应丢弃旧段样本。");
            remote.Push(101, before, 1, default, 1);
            Require(!remote.Present(101) && remote.Present(102), "迟到的旧连续段不能恢复旧位置。");
        }

        private static void RunCase(string name, Action test, StringBuilder report, ref int passed)
        {
            test();
            passed++;
            report.AppendLine("PASS " + name);
        }

        private static bool Near(double value, double expected) => Math.Abs(value - expected) < 0.00001d;

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }

        private static void Invoke(object target, string name, params object[] arguments) =>
            target.GetType().GetMethod(name, PrivateInstance).Invoke(target, arguments);

        private static void Set(object target, string name, object value) =>
            target.GetType().GetField(name, PrivateInstance).SetValue(target, value);

        private sealed class Scope : IDisposable
        {
            private readonly Scene _scene = EditorSceneManager.NewPreviewScene();
            private readonly ClientManager _previousClient = ClientManager.Instance;
            public ClientManager Client { get; }
            public NetBridge Bridge { get; }

            public Scope()
            {
                GameObject root = CreateRoot("CombatTimelineClient");
                Bridge = root.AddComponent<NetBridge>();
                Client = root.AddComponent<ClientManager>();
                Client.Initialize(Bridge);
            }

            public GameObject CreateRoot(string name)
            {
                GameObject root = EditorUtility.CreateGameObjectWithHideFlags(name, HideFlags.HideAndDontSave);
                SceneManager.MoveGameObjectToScene(root, _scene);
                return root;
            }

            public PlayerInputDriver CreateInput() => CreateRoot("CombatTimelineInput").AddComponent<PlayerInputDriver>();

            public RemotePoseInterpolator CreateRemote(uint netId)
            {
                GameObject root = CreateRoot("Remote" + netId);
                var brain = root.AddComponent<PlayerBrain>();
                var sight = root.AddComponent<PlayerSightController>();
                var interpolator = root.AddComponent<RemotePoseInterpolator>();
                Set(brain, "<Role>k__BackingField", PawnRole.Interpolated);
                Set(brain, "<Sight>k__BackingField", sight);
                Set(brain, "<Interpolator>k__BackingField", interpolator);
                Invoke(interpolator, "Awake");
                Client.Players.Add(netId, root);
                return interpolator;
            }

            public void Dispose()
            {
                Client.ShutDown();
                typeof(ClientManager).GetField("<Instance>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic)
                    .SetValue(null, _previousClient);
                if (_scene.IsValid()) EditorSceneManager.ClosePreviewScene(_scene);
            }
        }
    }
}
