using System;
using System.IO;
using System.Reflection;
using System.Text;
using GamePlay.HoldSystem;
using GamePlay.Player;
using GamePlay.Player.Simulation;
using GamePlay.Player.Simulation.Reconciliation;
using Mirror;
using Network.Client;
using Network.Common;
using Network.Common.Messages;
using Network.Server;
using Unity.Pipeline.Commands;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FPSGame.Editor.Validation
{
    /// <summary>隔离验证瞬时事件的真实 Mirror 编解码、去重和服务器成功开火边沿。</summary>
    public static class NetworkEventValidation
    {
        private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
        private static bool _running;

        [CliCommand("network_event_validate", "Validate event wire format, client delivery and authoritative fire", Tags = new[] { "fpsgame/validation" })]
        public static string Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return "REFUSED：请先退出 Play Mode，再运行隔离网络事件验证。";
            if (_running) return "REFUSED：网络事件验证已经在执行。";
            _running = true;
            var report = new StringBuilder();
            int passed = 0;
            try
            {
                RunCase("多态开火负载经 Mirror 消息打包解包后完整保留", TestWireRoundTrip, report, ref passed);
                RunCase("空负载、未知子类和未知线路类型均明确拒绝", TestInvalidPayloads, report, ref passed);
                RunCase("客户端独立于玩家生成投递事件，并拒绝重复和旧事件", TestClientDelivery, report, ref passed);
                RunCase("客户端关闭后重连清空事件去重状态", TestClientRestart, report, ref passed);
                RunCase("收到自己的权威开火不会再次扣弹或播放本地射击", TestOwnerConfirmation, report, ref passed);
                RunCase("服务器准备与射速限制按成功发射返回，空弹和松手不发射", TestAuthoritativeCadence, report, ref passed);
                RunCase("服务器射击入口拒绝本地、远端和未绑定身份", TestAuthoritativeRoles, report, ref passed);
                RunCase("冗余输入不改写已选 Fire，缺失输入不会补射", TestInputRedundancy, report, ref passed);
                RunCase("含 Fire 的移动输入真实回滚不会重扣弹或重播枪械", TestMovementReplay, report, ref passed);
                return "PASS " + passed + " checks\n" + report;
            }
            finally { _running = false; }
        }

        private static FireEventMessage Event(ulong id = 1, uint netId = 73) => new()
        {
            eventId = id,
            serverTick = 1234,
            netId = netId,
            predictionEpoch = 9,
            inputTick = 513,
            position = new Vector3(1.25f, -2f, 30f),
            origin = new Vector3(1.25f, -0.25f, 30.5f),
            direction = new Vector3(0.6f, 0f, 0.8f)
        };

        private static void TestWireRoundTrip()
        {
            FireEventMessage original = Event(0x0102030405060708UL);
            using NetworkWriterPooled writer = NetworkWriterPool.Get();
            NetworkMessages.Pack(new NetworkEventEnvelope { payload = original }, writer);
            using NetworkReaderPooled reader = NetworkReaderPool.Get(writer.ToArraySegment());
            Require(NetworkMessages.UnpackId(reader, out ushort id) && id == NetworkMessageId<NetworkEventEnvelope>.Id,
                "实际线路消息必须使用统一事件封装的 Mirror 消息 ID。");
            NetworkEventEnvelope envelope = reader.Read<NetworkEventEnvelope>();
            Require(envelope.payload is FireEventMessage, "派生类型必须在抽象基类字段中往返保留。");
            var decoded = (FireEventMessage)envelope.payload;
            Require(decoded.eventId == original.eventId && decoded.serverTick == original.serverTick &&
                    decoded.netId == original.netId && decoded.predictionEpoch == original.predictionEpoch &&
                    decoded.inputTick == original.inputTick && decoded.position == original.position &&
                    decoded.origin == original.origin && decoded.direction == original.direction && reader.Remaining == 0,
                "基类和开火子类的全部字段必须完整往返，不能留下未读取的字节。");
        }

        private static void TestInvalidPayloads()
        {
            using (NetworkWriterPooled writer = NetworkWriterPool.Get())
                Expect<ArgumentNullException>(() => writer.WriteNetworkEventMessage(null), "空事件不能成为有效线路消息。");
            using (NetworkWriterPooled writer = NetworkWriterPool.Get())
                Expect<ArgumentException>(() => writer.WriteNetworkEventMessage(new UnknownEvent()), "未注册子类不能被静默序列化为基类。");
            using (NetworkReaderPooled reader = NetworkReaderPool.Get(new byte[] { byte.MaxValue }))
                Expect<InvalidDataException>(() => reader.ReadNetworkEventMessage(), "未知事件种类必须拒绝解码。");
        }

        private static void TestClientDelivery()
        {
            using var scope = new PreviewScope();
            ClientManager client = scope.CreateClient();
            int received = 0;
            NetworkEventMessage last = null;
            client.EventReceived += value => { received++; last = value; };
            FireEventMessage first = Event(10, uint.MaxValue);
            Invoke(client, "HandleNetworkEvent", first);
            Require(received == 1 && ReferenceEquals(last, first) && client.Players.Players.Count == 0,
                "事件的世界坐标数据应在玩家对象尚未生成时仍可消费。");
            Invoke(client, "HandleNetworkEvent", first);
            Invoke(client, "HandleNetworkEvent", Event(9));
            Invoke(client, "HandleNetworkEvent", Event(0));
            Invoke(client, "HandleNetworkEvent", new object[] { null });
            Require(received == 1, "重复、过期、零序号和空事件不能再次触发表现消费。");
            Invoke(client, "HandleNetworkEvent", Event(11));
            Require(received == 2, "去重不能屏蔽后续新事件。");
        }

        private static void TestClientRestart()
        {
            using var scope = new PreviewScope();
            ClientManager client = scope.CreateClient();
            int received = 0;
            client.EventReceived += _ => received++;
            Invoke(client, "HandleNetworkEvent", Event(1000));
            client.ShutDown();
            client.Initialize(scope.Bridge);
            Invoke(client, "HandleNetworkEvent", Event(1));
            Require(received == 2, "新连接允许服务器从小序号重新投递，不能继承旧去重高水位。");
        }

        private static void TestOwnerConfirmation()
        {
            using var scope = new PreviewScope();
            AK47 gun = scope.EquipRifle();
            ClientManager client = scope.CreateClient();
            client.Players.Add(73, scope.Root);
            int shots = 0;
            int confirmations = 0;
            scope.Controller.OnShot += _ => shots++;
            client.EventReceived += _ => confirmations++;
            for (uint tick = 0; tick <= gun.Config.PrepareTicks; tick++) scope.Controller.StepLocal(true);
            Require(shots == 1, "测试前必须已经完成一次本地预测射击。");
            uint localTick = scope.Controller.LocalTick;
            int rounds = gun.Magazine.Rounds;
            Invoke(client, "HandleNetworkEvent", Event());
            Require(confirmations == 1 && shots == 1 && gun.Magazine.Rounds == rounds && scope.Controller.LocalTick == localTick,
                "自己的开火消息用于确认，不得再次驱动预测枪械、扣弹或发送本地 OnShot。");
        }

        private static void TestAuthoritativeCadence()
        {
            using var scope = new PreviewScope();
            AK47 gun = scope.EquipRifle();
            scope.SetRole(PawnRole.Authoritative);
            int initial = gun.Magazine.Rounds;
            int reserve = gun.Magazine.ReserveRounds;
            int gunShots = 0;
            int localShots = 0;
            gun.OnShot += () => gunShots++;
            scope.Controller.OnShot += _ => localShots++;
            Require(!scope.Controller.StepServer(true), "第一拍只能开始准备。");
            for (uint i = 1; i < gun.Config.PrepareTicks; i++)
                Require(!scope.Controller.StepServer(true), "准备结束前不能产生成功开火。");
            Require(scope.Controller.StepServer(true) && gun.Magazine.Rounds == initial - 1 && gunShots == 1,
                "准备结束当拍应成功一次并只消耗一发。");
            for (uint i = 1; i < gun.Config.ShotIntervalTicks; i++)
                Require(!scope.Controller.StepServer(true), "按住 Fire 不能绕过射速限制。");
            Require(scope.Controller.StepServer(true) && gunShots == 2 && gun.Magazine.Rounds == initial - 2,
                "冷却到期应正好允许第二发。");
            int beforeRelease = gun.Magazine.Rounds;
            for (uint i = 0; i < gun.Config.ShotIntervalTicks * 2; i++)
                Require(!scope.Controller.StepServer(false), "松开 Fire 只能推进冷却，不能产生新射击。");
            Require(gun.Magazine.Rounds == beforeRelease, "松开阶段不得消耗弹药。");
            int successes = 2;
            for (uint i = 0; i < (uint)initial * gun.Config.ShotIntervalTicks + 1; i++)
                if (scope.Controller.StepServer(true)) successes++;
            Require(successes == initial && gunShots == initial && gun.Magazine.Empty && gun.Magazine.ReserveRounds == reserve,
                "每个成功返回必须对应一发子弹，空弹后不能继续成功或自动消耗备弹。");
            Require(localShots == initial && scope.Controller.LocalTick == 0,
                "权威枪械生命周期应逐发转发，但不得推进本地武器时钟。");
        }

        private static void TestAuthoritativeRoles()
        {
            using var scope = new PreviewScope();
            AK47 gun = scope.EquipRifle();
            int rounds = gun.Magazine.Rounds;
            foreach (PawnRole role in new[] { PawnRole.None, PawnRole.Predicted, PawnRole.Interpolated })
            {
                scope.SetRole(role);
                for (uint i = 0; i < gun.Config.PrepareTicks + gun.Config.ShotIntervalTicks + 1; i++)
                    Require(!scope.Controller.StepServer(true), role + " 不得经服务器入口发射。");
                Require(gun.GunState == GunState.None && gun.Magazine.Rounds == rounds && scope.Controller.LocalTick == 0,
                    role + " 经服务器入口不能推进准备状态、扣弹或修改本地时钟。");
            }
            scope.SetRole(PawnRole.Authoritative);
            scope.Controller.enabled = false;
            Require(!scope.Controller.StepServer(true) && gun.GunState == GunState.None, "停用控制器也不得由服务器推进。");
        }

        private static void TestInputRedundancy()
        {
            var queue = new PlayerInputQueue();
            queue.Push(new PlayerInputFrame { tick = 1, fire = true });
            queue.Push(new PlayerInputFrame { tick = 1, fire = false });
            PlayerInputFrame frame = queue.ResolveNext();
            Require(frame.fire && queue.PendingCount == 1, "冗余输入不能改写同一 Tick 已接收的 Fire。");
            queue.Commit(frame.tick);
            queue.Push(new PlayerInputFrame { tick = 1, fire = true });
            frame = queue.ResolveNext();
            Require(frame.tick == 2 && !frame.fire && queue.PendingCount == 0, "旧输入不能重射，缺失下一拍不得复用 Fire。");
            queue.Commit(frame.tick);
            queue.Push(new PlayerInputFrame { tick = 3, fire = true });
            frame = queue.ResolveNext();
            Require(frame.tick == 3 && frame.fire, "新的有效 Fire 不能被旧输入去重误丢弃。");
        }

        private static void TestMovementReplay()
        {
            using var scope = new PreviewScope();
            AK47 gun = scope.EquipRifle();
            int shots = 0;
            scope.Controller.OnShot += _ => shots++;
            for (uint i = 0; i <= gun.Config.PrepareTicks; i++) scope.Controller.StepLocal(true);
            uint weaponTick = scope.Controller.LocalTick;
            int rounds = gun.Magazine.Rounds;
            PlayerSimulation simulation = scope.CreateSimulation();
            var history = new PredictionHistory(8);
            PlayerSimState baseline = simulation.State;
            history.Seed(0, baseline);
            for (uint tick = 1; tick <= 2; tick++)
            {
                var frame = new PlayerInputFrame { tick = tick, fire = true };
                simulation.Step(frame);
                history.Record(frame, simulation.State);
            }
            baseline.position += Vector3.right;
            var reconciler = new PlayerReconciler(simulation, history, new ReconciliationSettings());
            simulation.SetReplayMode(true);
            ReconcileResult result;
            try
            {
                result = reconciler.Reconcile(new PlayerAuthoritativeSnapshot
                {
                    Epoch = 1, ServerTick = 1, StateInputTick = 0, State = baseline
                }, 2);
            }
            finally { simulation.SetReplayMode(false); }
            Require(result.Status == ReconcileStatus.Replayed && result.ReplayedTicks == 2,
                "验证必须实际恢复基准并重放两拍含 Fire 的运动输入。");
            PlayerPrediction prediction = scope.Root.AddComponent<PlayerPrediction>();
            prediction.ResetPrediction();
            Require(shots == 1 && gun.Magazine.Rounds == rounds && scope.Controller.LocalTick == weaponTick,
                "真实移动重放和预测重置不能重放射击或重置独立武器时钟。");
            scope.Controller.StepLocal(true);
            for (uint i = 1; i < gun.Config.ShotIntervalTicks; i++) scope.Controller.StepLocal(true);
            Require(shots == 2 && gun.Magazine.Rounds == rounds - 1,
                "重放后下一发仍应遵守原有冷却继续正常射击。");
        }

        private static object Invoke(object target, string name, params object[] arguments)
        {
            MethodInfo method = target.GetType().GetMethod(name, PrivateInstance);
            Require(method != null, "缺少隔离验证入口 " + name);
            return method.Invoke(target, arguments);
        }

        private static void Expect<T>(Action action, string message) where T : Exception
        {
            try { action(); }
            catch (T) { return; }
            throw new InvalidOperationException(message);
        }

        private static void RunCase(string name, Action test, StringBuilder report, ref int passed)
        {
            test();
            passed++;
            report.AppendLine("PASS " + name);
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }

        private sealed class UnknownEvent : NetworkEventMessage { }

        private sealed class PreviewScope : IDisposable
        {
            private readonly Scene _scene = EditorSceneManager.NewPreviewScene();
            private readonly ClientManager _previousClient = ClientManager.Instance;
            private ClientManager _client;
            private PlayerMovementDataSO _movement;
            public GameObject Root { get; }
            public PlayerHolder Holder { get; }
            public PlayerBrain Brain { get; }
            public PlayerWeaponController Controller { get; }
            public AK47SO Definition { get; }
            public NetBridge Bridge { get; private set; }

            public PreviewScope()
            {
                Root = CreateRoot("NetworkEventValidation");
                Holder = Root.AddComponent<PlayerHolder>();
                Brain = Root.AddComponent<PlayerBrain>();
                CreateRoot("wpn").transform.SetParent(Root.transform, false);
                Controller = Root.AddComponent<PlayerWeaponController>();
                SetRole(PawnRole.Predicted);
                Invoke(Controller, "OnDisable");
                Invoke(Controller, "OnEnable");
                AK47SO source = AssetDatabase.LoadAssetAtPath<AK47SO>("Assets/Scripts/AK47SO.asset");
                Require(source != null, "真实 AK47SO 必须存在。");
                Definition = UnityEngine.Object.Instantiate(source);
                Definition.hideFlags = HideFlags.HideAndDontSave;
                var serialized = new SerializedObject(Definition);
                serialized.FindProperty("_initialTotalBullets").intValue = 20;
                serialized.FindProperty("_capacity").intValue = 4;
                serialized.FindProperty("_prepareTicks").intValue = 2;
                serialized.FindProperty("_shotIntervalTicks").intValue = 3;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }

            public AK47 EquipRifle()
            {
                Require(Holder.EquipWeapon(Definition), "测试应能装备真实 AK47 模型和逻辑。");
                return (AK47)Holder.CurrentWeapon;
            }

            public ClientManager CreateClient()
            {
                GameObject root = CreateRoot("NetworkEventValidationClient");
                Bridge = root.AddComponent<NetBridge>();
                _client = root.AddComponent<ClientManager>();
                _client.Initialize(Bridge);
                return _client;
            }

            public PlayerSimulation CreateSimulation()
            {
                Root.AddComponent<PlayerSightController>();
                PlayerSimulation simulation = Root.AddComponent<PlayerSimulation>();
                _movement = ScriptableObject.CreateInstance<PlayerMovementDataSO>();
                _movement.hideFlags = HideFlags.HideAndDontSave;
                typeof(PlayerSimulation).GetField("_playerMovementDataSO", PrivateInstance).SetValue(simulation, _movement);
                typeof(PlayerSimulation).GetField("_gravityScale", PrivateInstance).SetValue(simulation, 0f);
                Invoke(simulation, "Awake");
                simulation.CaptureFromBody();
                return simulation;
            }

            public void SetRole(PawnRole role) => typeof(PlayerBrain)
                .GetField("<Role>k__BackingField", PrivateInstance).SetValue(Brain, role);

            private GameObject CreateRoot(string name)
            {
                GameObject root = EditorUtility.CreateGameObjectWithHideFlags(name, HideFlags.HideAndDontSave);
                SceneManager.MoveGameObjectToScene(root, _scene);
                return root;
            }

            public void Dispose()
            {
                try
                {
                    if (_client != null) _client.ShutDown();
                    if (Controller != null) Invoke(Controller, "OnDisable");
                }
                finally
                {
                    typeof(ClientManager).GetField("<Instance>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic)
                        .SetValue(null, _previousClient);
                    if (_scene.IsValid()) EditorSceneManager.ClosePreviewScene(_scene);
                    if (Definition != null) UnityEngine.Object.DestroyImmediate(Definition);
                    if (_movement != null) UnityEngine.Object.DestroyImmediate(_movement);
                }
            }
        }
    }
}
