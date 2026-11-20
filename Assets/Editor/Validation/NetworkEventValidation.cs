using System;
using System.Collections.Generic;
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
                RunCase("发布器完整映射玩法事实，并为不同射手分配连续全局序号", TestPublisherMapping, report, ref passed);
                RunCase("发布器空集合保持序号，空参数和未知玩法事件明确拒绝", TestPublisherInvalidEvents, report, ref passed);
                RunCase("服务器关闭后重建玩法逻辑和发布器，并从新事件序号开始", TestServerPublisherLifecycle, report, ref passed);
                RunCase("客户端事件定位远端射手，选择栏位并只播放一次表现", TestRemoteFirePresentation, report, ref passed);
                RunCase("事件处理器覆盖缺失装备、未知事件和表现异常，并继续交付", TestEventHandlerFailures, report, ref passed);
                RunCase("本地预测射击在没有服务器确认时保留已发生结果", TestUnconfirmedOwnerShot, report, ref passed);
                RunCase("已有开火音效支持空、单个和多个音频，并在连续开火时返回有效索引", TestFireAudioSelection, report, ref passed);
                RunCase("切枪事件经 Mirror 完整往返，发布器与开火共用连续全局序号", TestWeaponSwitchProtocol, report, ref passed);
                RunCase("切枪事件定位远端玩家并保持幂等，本地预测通过确认保留选择", TestWeaponSwitchRouting, report, ref passed);
                RunCase("远端准备计时随切刀、替换和停用取消，开火完成准备且保留模拟状态", TestRemotePrepareLifecycle, report, ref passed);
                RunCase("换弹、准备和取消消息经 Mirror 完整往返并共用全局序号", TestWeaponActionProtocol, report, ref passed);
                RunCase("刀攻击输入和动作经 Mirror 往返，远端播放一次且本机确认不重播", TestKnifeProtocolAndRouting, report, ref passed);
                RunCase("权威枪械按真实顺序记录换弹、取消和准备，完成时只补弹一次", TestAuthoritativeReloadLifecycle, report, ref passed);
                RunCase("远端换弹和取消只播放一次，本地确认保留预测结果", TestRemoteReloadRouting, report, ref passed);
                RunCase("服务器主动武器动作同步拥有者，同源确认保持本地预测", TestServerInitiatedOwnerActions, report, ref passed);
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
            weaponSlot = WeaponSlot.Rifle,
            position = new Vector3(1.25f, -2f, 30f),
            origin = new Vector3(1.25f, -0.25f, 30.5f),
            direction = new Vector3(0.6f, 0f, 0.8f)
        };

        private static void TestWireRoundTrip()
        {
            CheckWireRoundTrip(Event(0x0102030405060708UL));
            FireEventMessage pistol = Event(2);
            pistol.weaponSlot = WeaponSlot.Pistol;
            CheckWireRoundTrip(pistol);
        }

        private static void CheckWireRoundTrip(FireEventMessage original)
        {
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
                    decoded.inputTick == original.inputTick && decoded.weaponSlot == original.weaponSlot &&
                    decoded.position == original.position &&
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
            queue.Push(new PlayerInputFrame { tick = 1, fire = true, reload = true });
            queue.Push(new PlayerInputFrame { tick = 1, fire = false, reload = false });
            PlayerInputFrame frame = queue.ResolveNext();
            Require(frame.fire && frame.reload && queue.PendingCount == 1, "冗余输入不能改写同一 Tick 已接收的开火与换弹。");
            queue.Commit(frame.tick);
            queue.Push(new PlayerInputFrame { tick = 1, fire = true });
            frame = queue.ResolveNext();
            Require(frame.tick == 2 && !frame.fire && !frame.reload && queue.PendingCount == 0,
                "旧输入不能重射，缺失下一拍不得复用开火与换弹。");
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

        private static void TestPublisherMapping()
        {
            using var scope = new PreviewScope();
            scope.CreateClient();
            var publisher = new ServerEventPublisher(scope.Bridge);
            FireEventMessage first = Event(1, 73);
            FireEventMessage second = Event(2, 74);
            second.serverTick++;
            second.predictionEpoch++;
            second.inputTick += 10;
            second.weaponSlot = WeaponSlot.Pistol;
            second.position += Vector3.right;
            second.origin += Vector3.up;
            second.direction = Vector3.back;

            foreach (FireEventMessage expected in new[] { first, second })
            {
                var actual = (FireEventMessage)Invoke(publisher, "CreateMessage", ShotEvent(expected));
                Require(actual.eventId == expected.eventId && actual.serverTick == expected.serverTick &&
                        actual.netId == expected.netId && actual.predictionEpoch == expected.predictionEpoch &&
                        actual.inputTick == expected.inputTick && actual.weaponSlot == expected.weaponSlot &&
                        actual.position == expected.position &&
                        actual.origin == expected.origin && actual.direction == expected.direction,
                    "发布器必须完整映射玩法事实，且跨射手沿用同一个事件序号。");
                CheckWireRoundTrip(actual);
            }

            Require(publisher.LastEventId == 2, "两名射手的两个事实必须正好分配两个全局事件序号。");

            Vector3 capturedOrigin = new Vector3(17f, 2f, -3f);
            Vector3 capturedDirection = Vector3.left;
            var laterInput = new ServerPlayerInput(73, 9, null, new PlayerInputFrame { tick = 700 });
            var laterResult = new PlayerLiveStepResult(default, false, Vector3.zero, Vector3.forward, WeaponSlot.Knife);
            var queuedShot = new ShotFiredEvent(2000, laterInput, laterResult,
                WeaponSlot.Pistol, capturedOrigin, capturedDirection);
            var queuedMessage = (FireEventMessage)Invoke(publisher, "CreateMessage", queuedShot);
            Require(queuedMessage.weaponSlot == WeaponSlot.Pistol && queuedMessage.origin == capturedOrigin &&
                    queuedMessage.direction == capturedDirection && queuedMessage.eventId == 3,
                "已排队的开火事实应使用发射时捕获的栏位与射线，后续拍的选择和视角不能改写它。");
            CheckWireRoundTrip(queuedMessage);
        }

        private static void TestPublisherInvalidEvents()
        {
            using var scope = new PreviewScope();
            scope.CreateClient();
            Expect<ArgumentNullException>(() => new ServerEventPublisher(null), "发布器必须拒绝空网络桥接。");
            var publisher = new ServerEventPublisher(scope.Bridge);
            publisher.Publish(Array.Empty<ServerGameEvent>());
            Expect<ArgumentNullException>(() => publisher.Publish(null), "发布器必须拒绝空事件集合。");
            Expect<ArgumentNullException>(() => publisher.Publish(new ServerGameEvent[] { null }),
                "发布器必须拒绝集合中的空玩法事件。");
            Expect<ArgumentException>(() => publisher.Publish(new ServerGameEvent[] { new UnknownGameEvent() }),
                "发布器必须明确拒绝尚未支持的玩法事件类型。");
            Require(publisher.LastEventId == 0, "空集合和非法事件不能消耗全局事件序号。");
        }

        private static void TestServerPublisherLifecycle()
        {
            using var scope = new PreviewScope();
            scope.CreateClient();
            ServerManager manager = scope.Root.AddComponent<ServerManager>();
            FieldInfo logicField = typeof(ServerManager).GetField("_gameLogic", PrivateInstance);
            FieldInfo publisherField = typeof(ServerManager).GetField("_eventPublisher", PrivateInstance);
            Require(logicField != null && publisherField != null, "服务器必须持有本次运行的玩法逻辑和发布器。");
            try
            {
                manager.Initialize(scope.Bridge);
                var firstLogic = (ServerGameLogic)logicField.GetValue(manager);
                var firstPublisher = (ServerEventPublisher)publisherField.GetValue(manager);
                Require(firstLogic != null && firstPublisher != null && firstPublisher.LastEventId == 0,
                    "首次初始化必须创建玩法逻辑和全新发布器。");
                Invoke(firstPublisher, "CreateMessage", ShotEvent(Event()));
                Require(firstPublisher.LastEventId == 1, "关闭前必须已实际分配过事件序号。");

                manager.Shutdown();
                Require(logicField.GetValue(manager) == null && publisherField.GetValue(manager) == null,
                    "服务器关闭必须释放本次运行的玩法逻辑和发布器引用。");
                manager.Initialize(scope.Bridge);
                var secondLogic = (ServerGameLogic)logicField.GetValue(manager);
                var secondPublisher = (ServerEventPublisher)publisherField.GetValue(manager);
                Require(secondLogic != null && !ReferenceEquals(firstLogic, secondLogic) &&
                        secondPublisher != null && !ReferenceEquals(firstPublisher, secondPublisher) &&
                        secondPublisher.LastEventId == 0,
                    "再次初始化必须更换玩法逻辑和发布器，并清空旧事件序号。");
                var firstAfterRestart = (FireEventMessage)Invoke(secondPublisher, "CreateMessage", ShotEvent(Event()));
                Require(firstAfterRestart.eventId == 1 && secondPublisher.LastEventId == 1,
                    "新服务器运行的第一条消息必须重新从序号一开始。");
            }
            finally { manager.Shutdown(); }
        }

        private static ShotFiredEvent ShotEvent(FireEventMessage source)
        {
            var input = new ServerPlayerInput(source.netId, source.predictionEpoch, null,
                new PlayerInputFrame { tick = source.inputTick });
            var result = new PlayerLiveStepResult(new PlayerSimState { position = source.position },
                true, source.origin, source.direction, source.weaponSlot);
            return new ShotFiredEvent(source.serverTick, input, result);
        }

        private static void TestRemoteFirePresentation()
        {
            using var shooter = new PreviewScope();
            using var other = new PreviewScope();
            AK47 gun = shooter.EquipRifle();
            AK47 otherGun = other.EquipRifle();
            shooter.SetRole(PawnRole.Interpolated);
            other.SetRole(PawnRole.Interpolated);
            Require(shooter.Holder.SelectSlot(WeaponSlot.Knife), "远端射手应从初始持刀状态开始。");
            ClientManager client = shooter.CreateClient();
            client.Players.Add(73, shooter.Root);
            client.Players.Add(74, other.Root);
            int shooterShots = 0;
            int otherShots = 0;
            int gunShots = 0;
            int delivered = 0;
            shooter.Controller.OnShot += _ => shooterShots++;
            other.Controller.OnShot += _ => otherShots++;
            gun.OnShot += () => gunShots++;
            client.EventReceived += _ => delivered++;
            int rounds = gun.Magazine.Rounds;
            int reserve = gun.Magazine.ReserveRounds;
            GunState state = gun.GunState;
            object cooldown = typeof(Gun).GetProperty("NextAllowedShotTick", PrivateInstance).GetValue(gun);
            Vector3 position = shooter.Root.transform.position;
            Quaternion rotation = shooter.Root.transform.rotation;

            Invoke(client, "HandleNetworkEvent", Event(10));
            Require(shooterShots == 1 && otherShots == 0 && delivered == 1 && gunShots == 0 &&
                    ReferenceEquals(shooter.Holder.CurrentWeapon, gun) &&
                    shooter.Holder.CurrentController is GunModelController,
                "开火事件应定位射手，切换到已拥有的指定枪械，并转发一次共用表现通知。");
            Invoke(client, "HandleNetworkEvent", Event(10));
            Invoke(client, "HandleNetworkEvent", Event(9));
            Invoke(client, "HandleNetworkEvent", Event(11, 74));
            Require(shooterShots == 1 && otherShots == 1 && delivered == 2 &&
                    ReferenceEquals(other.Holder.CurrentWeapon, otherGun),
                "重复和旧事件应去重，新射手事件应只作用于对应玩家。");
            Require(gun.Magazine.Rounds == rounds && gun.Magazine.ReserveRounds == reserve &&
                    gun.GunState == state && cooldown.Equals(typeof(Gun).GetProperty("NextAllowedShotTick", PrivateInstance).GetValue(gun)) &&
                    shooter.Controller.LocalTick == 0 && shooter.Controller.ServerTick == 0 &&
                    shooter.Root.transform.position == position && shooter.Root.transform.rotation == rotation,
                "远端开火表现应保留弹药、冷却、枪械状态和模拟时钟，并使用玩家当前插值姿态。");
        }

        private static void TestEventHandlerFailures()
        {
            Expect<ArgumentNullException>(() => new EventMessageHandler(null), "事件处理器必须绑定玩家管理器。");
            using var scope = new PreviewScope();
            scope.SetRole(PawnRole.Interpolated);
            ClientManager client = scope.CreateClient();
            client.Players.Add(73, scope.Root);
            int delivered = 0;
            int shots = 0;
            client.EventReceived += _ => delivered++;
            scope.Controller.OnShot += _ => shots++;
            Invoke(client, "HandleNetworkEvent", Event(1));
            Require(delivered == 1 && shots == 0 && scope.Holder.CurrentSlot == WeaponSlot.Knife,
                "缺失指定枪械时仍应交付事实，并保留可用的装备表现。");
            scope.EquipRifle();
            FireEventMessage missingPistol = Event(2);
            missingPistol.weaponSlot = WeaponSlot.Pistol;
            Invoke(client, "HandleNetworkEvent", missingPistol);
            FireEventMessage invalidSlot = Event(3);
            invalidSlot.weaponSlot = (WeaponSlot)byte.MaxValue;
            Invoke(client, "HandleNetworkEvent", invalidSlot);
            Invoke(client, "HandleNetworkEvent", new UnknownEvent { eventId = 4, netId = 73 });
            Require(delivered == 4 && shots == 0 && scope.Holder.CurrentSlot == WeaponSlot.Rifle,
                "缺失栏位、无效栏位和未知事件应保持事件交付，枪械表现按可用资源处理。");

            void ThrowPresentation(Gun _) => throw new InvalidOperationException("ExpectedClientEventPresentationFailure");
            scope.Controller.OnShot += ThrowPresentation;
            try
            {
                Invoke(client, "HandleNetworkEvent", Event(5));
                Invoke(client, "HandleNetworkEvent", Event(5));
            }
            finally { scope.Controller.OnShot -= ThrowPresentation; }
            Invoke(client, "HandleNetworkEvent", Event(6));
            Require(delivered == 6 && shots == 2 && scope.Controller.LocalTick == 0 && scope.Controller.ServerTick == 0,
                "表现异常应记录并隔离，同一事件只接受一次，随后事件继续进入表现和观察者。");
        }

        private static void TestUnconfirmedOwnerShot()
        {
            using var owner = new PreviewScope();
            using var authority = new PreviewScope();
            AK47 gun = owner.EquipRifle();
            authority.EquipRifle();
            authority.SetRole(PawnRole.Authoritative);
            ClientManager client = owner.CreateClient();
            client.Players.Add(73, owner.Root);
            int shots = 0;
            owner.Controller.OnShot += _ => shots++;
            for (uint tick = 0; tick <= gun.Config.PrepareTicks; tick++) owner.Controller.StepLocal(true);
            Require(shots == 1 && !authority.Controller.StepServer(true),
                "本地应已预测成功开火，同时权威枪械仍可因准备状态拒绝本次请求。");
            int rounds = gun.Magazine.Rounds;
            for (uint tick = 0; tick < gun.Config.ShotIntervalTicks; tick++) owner.Controller.StepLocal(false);
            Invoke(client, "HandleNetworkEvent", Event(1, 999));
            Require(shots == 1 && gun.Magazine.Rounds == rounds,
                "本地预测射击在权威确认缺失或拒绝时应保留已经发生的结果。");
        }

        private static void TestFireAudioSelection()
        {
            using var scope = new PreviewScope();
            scope.Root.AddComponent<AudioSource>();
            var audio = scope.Root.AddComponent<PlayerFireAudioTester>();
            Invoke(audio, "Awake");
            FieldInfo clips = typeof(PlayerFireAudioTester).GetField("_fireAudios", PrivateInstance);
            FieldInfo last = typeof(PlayerFireAudioTester).GetField("_lastRand", PrivateInstance);
            clips.SetValue(audio, new List<AudioClip>());
            Invoke(audio, "OnShot", new object[] { null });
            UnityEngine.Random.State randomState = UnityEngine.Random.state;
            try
            {
                UnityEngine.Random.InitState(7319);
                foreach (int count in new[] { 1, 2, 3 })
                {
                    clips.SetValue(audio, new List<AudioClip>(new AudioClip[count]));
                    last.SetValue(audio, -1);
                    int previous = -1;
                    var selected = new HashSet<int>();
                    for (int shot = 0; shot < 100; shot++)
                    {
                        int index = (int)Invoke(audio, "GetRand");
                        Require(index >= 0 && index < count && (count == 1 || index != previous),
                            "音效选择必须返回有效索引，多音频配置的相邻射击应选择不同音频。");
                        previous = index;
                        selected.Add(index);
                    }
                    Require(selected.Count == count, "随机音效选择应覆盖所有配置条目。");
                }
            }
            finally { UnityEngine.Random.state = randomState; }
        }

        private static WeaponSwitchEventMessage SwitchEvent(ulong id, uint netId, WeaponSlot slot) => new()
        {
            eventId = id,
            serverTick = 1234,
            netId = netId,
            predictionEpoch = 9,
            inputTick = 513,
            weaponSlot = slot
        };

        private static void CheckWireRoundTrip(WeaponSwitchEventMessage original)
        {
            using NetworkWriterPooled writer = NetworkWriterPool.Get();
            NetworkMessages.Pack(new NetworkEventEnvelope { payload = original }, writer);
            using NetworkReaderPooled reader = NetworkReaderPool.Get(writer.ToArraySegment());
            Require(NetworkMessages.UnpackId(reader, out ushort id) && id == NetworkMessageId<NetworkEventEnvelope>.Id,
                "切枪事件必须通过统一事件封装的真实 Mirror 消息 ID 传输。");
            NetworkEventEnvelope envelope = reader.Read<NetworkEventEnvelope>();
            Require(envelope.payload is WeaponSwitchEventMessage, "切枪派生类型必须完整往返。");
            var decoded = (WeaponSwitchEventMessage)envelope.payload;
            Require(decoded.eventId == original.eventId && decoded.serverTick == original.serverTick &&
                    decoded.netId == original.netId && decoded.predictionEpoch == original.predictionEpoch &&
                    decoded.inputTick == original.inputTick && decoded.weaponSlot == original.weaponSlot &&
                    decoded.serverInitiated == original.serverInitiated && reader.Remaining == 0,
                "切枪事件的公共头、玩家、预测轮次、输入拍和目标栏位必须逐项保留。");
        }

        private static void TestWeaponSwitchProtocol()
        {
            Require((byte)NetworkEventKind.Fire == 1 && (byte)NetworkEventKind.WeaponSwitch == 2,
                "开火与切枪使用已经确定的稳定线路类型码。");
            foreach (WeaponSlot slot in new[] { WeaponSlot.Rifle, WeaponSlot.Pistol, WeaponSlot.Knife })
            {
                WeaponSwitchEventMessage message = SwitchEvent(0x0102030405060708UL, 73, slot);
                message.serverInitiated = slot == WeaponSlot.Pistol;
                CheckWireRoundTrip(message);
            }

            using var scope = new PreviewScope();
            scope.CreateClient();
            var publisher = new ServerEventPublisher(scope.Bridge);
            var input = new ServerPlayerInput(73, 9, null, new PlayerInputFrame { tick = 513 });
            var switched = (WeaponSwitchEventMessage)Invoke(publisher, "CreateMessage",
                new WeaponSwitchedEvent(1234, input, WeaponSlot.Rifle, true));
            Require(switched.eventId == 1 && switched.serverTick == 1234 && switched.netId == 73 &&
                    switched.predictionEpoch == 9 && switched.inputTick == 513 && switched.weaponSlot == WeaponSlot.Rifle &&
                    switched.serverInitiated,
                "发布器必须完整映射实际切枪事实及其输入来源。");
            CheckWireRoundTrip(switched);
            var fired = (FireEventMessage)Invoke(publisher, "CreateMessage", ShotEvent(Event(2)));
            Require(fired.eventId == switched.eventId + 1, "同一输入的开火事件应接续切枪事件的全局序号。");
            CheckWireRoundTrip(fired);
            var nextInput = new ServerPlayerInput(74, 10, null, new PlayerInputFrame { tick = 514 });
            var nextSwitch = (WeaponSwitchEventMessage)Invoke(publisher, "CreateMessage",
                new WeaponSwitchedEvent(1235, nextInput, WeaponSlot.Knife));
            Require(nextSwitch.eventId == fired.eventId + 1 && publisher.LastEventId == 3 &&
                    nextSwitch.serverTick == 1235 && nextSwitch.netId == 74 && nextSwitch.predictionEpoch == 10 &&
                    nextSwitch.inputTick == 514 && nextSwitch.weaponSlot == WeaponSlot.Knife && !nextSwitch.serverInitiated,
                "不同玩家的切枪与开火事实必须共用一个连续全局序号。");
            CheckWireRoundTrip(nextSwitch);
        }

        private static void TestWeaponSwitchRouting()
        {
            using var first = new PreviewScope();
            using var second = new PreviewScope();
            using var owner = new PreviewScope();
            first.EquipRifle();
            second.EquipRifle();
            AK47 ownerGun = owner.EquipRifle();
            first.SetRole(PawnRole.Interpolated);
            second.SetRole(PawnRole.Interpolated);
            Require(first.Holder.SelectSlot(WeaponSlot.Knife) && second.Holder.SelectSlot(WeaponSlot.Knife) &&
                    owner.Holder.SelectSlot(WeaponSlot.Knife), "验证应从三名玩家各自持刀开始。");
            ClientManager client = first.CreateClient();
            client.Players.Add(73, first.Root);
            client.Players.Add(74, second.Root);
            client.Players.Add(75, owner.Root);
            int firstChanges = 0, secondChanges = 0, ownerChanges = 0;
            int firstPrepares = 0, secondPrepares = 0, ownerPrepares = 0;
            int delivered = 0;
            first.Holder.OnWeaponChanged += () => firstChanges++;
            second.Holder.OnWeaponChanged += () => secondChanges++;
            owner.Holder.OnWeaponChanged += () => ownerChanges++;
            first.Controller.OnPrepare += _ => firstPrepares++;
            second.Controller.OnPrepare += _ => secondPrepares++;
            owner.Controller.OnPrepare += _ => ownerPrepares++;
            client.EventReceived += _ => delivered++;

            Invoke(client, "HandleNetworkEvent", SwitchEvent(10, 73, WeaponSlot.Rifle));
            Invoke(client, "HandleNetworkEvent", SwitchEvent(10, 73, WeaponSlot.Rifle));
            Invoke(client, "HandleNetworkEvent", SwitchEvent(9, 73, WeaponSlot.Knife));
            Invoke(client, "HandleNetworkEvent", SwitchEvent(11, 73, WeaponSlot.Rifle));
            Require(firstChanges == 1 && firstPrepares == 0 && !first.Controller.IsRemotePreparing &&
                    secondChanges == 0 && second.Holder.CurrentSlot == WeaponSlot.Knife && delivered == 2,
                "切枪应定位指定远端玩家，准备由独立事件驱动，重复切枪保持幂等。");

            Invoke(client, "HandleNetworkEvent", SwitchEvent(12, 74, WeaponSlot.Rifle));
            Invoke(client, "HandleNetworkEvent", SwitchEvent(13, 73, WeaponSlot.Knife));
            Invoke(client, "HandleNetworkEvent", SwitchEvent(14, 74, WeaponSlot.Knife));
            Require(firstChanges == 2 && secondChanges == 2 && firstPrepares == 0 && secondPrepares == 0 &&
                    first.Holder.CurrentSlot == WeaponSlot.Knife && second.Holder.CurrentSlot == WeaponSlot.Knife &&
                    !first.Controller.IsRemotePreparing && !second.Controller.IsRemotePreparing,
                "两名远端玩家应各自完成切枪与切刀，切刀及时取消对应准备表现。");
            Invoke(client, "HandleNetworkEvent", SwitchEvent(15, 73, WeaponSlot.Rifle));
            Invoke(client, "HandleNetworkEvent", SwitchEvent(16, 73, WeaponSlot.Pistol));
            int ownerRounds = ownerGun.Magazine.Rounds;
            Invoke(client, "HandleNetworkEvent", SwitchEvent(17, 75, WeaponSlot.Rifle));
            Require(firstChanges == 3 && firstPrepares == 0 && first.Holder.CurrentSlot == WeaponSlot.Rifle &&
                    ownerChanges == 0 && ownerPrepares == 0 && owner.Holder.CurrentSlot == WeaponSlot.Knife &&
                    ownerGun.Magazine.Rounds == ownerRounds && owner.Controller.LocalTick == 0 &&
                    owner.Controller.ServerTick == 0 && delivered == 8,
                "缺失目标装备时保留现有表现并通知观察者，本地预测确认保留玩家当前选择。");
        }

        private static void TestRemotePrepareLifecycle()
        {
            using var scope = new PreviewScope();
            AK47 firstGun = scope.EquipRifle();
            scope.SetRole(PawnRole.Interpolated);
            Require(scope.Holder.SelectSlot(WeaponSlot.Knife), "远端准备验证应从持刀开始。");
            int prepared = 0, completed = 0, shots = 0;
            Gun lastCompleted = null;
            scope.Controller.OnPrepare += _ => prepared++;
            scope.Controller.OnPrepareEnd += gun => { completed++; lastCompleted = gun; };
            scope.Controller.OnShot += _ => shots++;
            int firstRounds = firstGun.Magazine.Rounds;
            int firstReserve = firstGun.Magazine.ReserveRounds;
            PropertyInfo cooldown = typeof(Gun).GetProperty("NextAllowedShotTick", PrivateInstance);
            object firstCooldown = cooldown.GetValue(firstGun);

            Require(scope.Controller.PresentRemoteWeaponSwitch(WeaponSlot.Rifle) && !scope.Controller.IsRemotePreparing &&
                    scope.Controller.PresentRemotePrepare(WeaponSlot.Rifle) && scope.Controller.IsRemotePreparing,
                "切入远端枪械后，独立准备事实应进入共用准备表现。");
            ExpireRemoteAction(scope.Controller);
            Require(scope.Controller.PresentRemoteWeaponSwitch(WeaponSlot.Knife), "远端准备中应允许切刀。");
            Invoke(scope.Controller, "Update");
            Require(!scope.Controller.IsRemotePreparing && completed == 0, "切刀应取消旧计时并结束准备覆盖。");

            Require(scope.Controller.PresentRemoteWeaponSwitch(WeaponSlot.Rifle), "远端应能再次切入枪械。");
            Require(scope.Controller.PresentRemotePrepare(WeaponSlot.Rifle), "再次准备应建立新的表现计时。");
            Invoke(scope.Controller, "Update");
            Require(scope.Controller.IsRemotePreparing && prepared == 2 && completed == 0,
                "再次切枪应建立新的截止时间，先前到期的计时应已被清理。");
            Require(scope.Holder.EquipWeapon(scope.Definition), "准备中应能通过装备栏替换枪械实例。");
            var secondGun = (Gun)scope.Holder.CurrentWeapon;
            int secondRounds = secondGun.Magazine.Rounds;
            int secondReserve = secondGun.Magazine.ReserveRounds;
            object secondCooldown = cooldown.GetValue(secondGun);
            Invoke(scope.Controller, "Update");
            Require(!ReferenceEquals(firstGun, secondGun) && !scope.Controller.IsRemotePreparing && completed == 0,
                "替换枪械应取消旧枪准备，旧计时与完成通知留在原动作生命周期内。");

            scope.Controller.PresentRemoteWeaponSwitch(WeaponSlot.Knife);
            scope.Controller.PresentRemoteWeaponSwitch(WeaponSlot.Rifle);
            scope.Controller.PresentRemotePrepare(WeaponSlot.Rifle);
            ExpireRemoteAction(scope.Controller);
            Invoke(scope.Controller, "Update");
            Invoke(scope.Controller, "Update");
            Require(!scope.Controller.IsRemotePreparing && completed == 1 && ReferenceEquals(lastCompleted, secondGun),
                "有效准备到期只完成一次，并将完成通知关联到当前枪械。");

            scope.Controller.PresentRemoteWeaponSwitch(WeaponSlot.Knife);
            scope.Controller.PresentRemoteWeaponSwitch(WeaponSlot.Rifle);
            scope.Controller.PresentRemotePrepare(WeaponSlot.Rifle);
            Require(scope.Controller.PresentRemoteFire(WeaponSlot.Rifle) && !scope.Controller.IsRemotePreparing &&
                    completed == 2 && shots == 1 && ReferenceEquals(lastCompleted, secondGun),
                "服务器开火到达时应先完成当前准备表现，再发布一次共用射击表现。");
            ExpireRemoteAction(scope.Controller);
            Invoke(scope.Controller, "Update");
            Require(completed == 2 && shots == 1, "开火接续后先前的准备计时应已清理。");

            scope.Controller.PresentRemoteWeaponSwitch(WeaponSlot.Knife);
            scope.Controller.PresentRemoteWeaponSwitch(WeaponSlot.Rifle);
            scope.Controller.PresentRemotePrepare(WeaponSlot.Rifle);
            Invoke(scope.Controller, "OnDisable");
            Invoke(scope.Controller, "OnEnable");
            ExpireRemoteAction(scope.Controller);
            Invoke(scope.Controller, "Update");
            Require(!scope.Controller.IsRemotePreparing && completed == 2 && prepared == 5 &&
                    firstGun.Magazine.Rounds == firstRounds && firstGun.Magazine.ReserveRounds == firstReserve &&
                    secondGun.Magazine.Rounds == secondRounds && secondGun.Magazine.ReserveRounds == secondReserve &&
                    firstCooldown.Equals(cooldown.GetValue(firstGun)) && secondCooldown.Equals(cooldown.GetValue(secondGun)) &&
                    firstGun.GunState == GunState.None && secondGun.GunState == GunState.None &&
                    scope.Controller.LocalTick == 0 && scope.Controller.ServerTick == 0,
                "停用应清理远端准备，全部表现流程应保留两把枪的弹药、状态与模拟时钟。");
        }

        private static PrepareEventMessage PrepareEvent(ulong id, uint netId = 73) => new()
        {
            eventId = id,
            serverTick = 1234,
            netId = netId,
            predictionEpoch = 9,
            inputTick = 513,
            weaponSlot = WeaponSlot.Rifle
        };

        private static ReloadEventMessage ReloadEvent(ulong id, uint netId = 73) => new()
        {
            eventId = id,
            serverTick = 1234,
            netId = netId,
            predictionEpoch = 9,
            inputTick = 513,
            weaponSlot = WeaponSlot.Rifle
        };

        private static WeaponActionCanceledEventMessage CanceledEvent(ulong id, uint netId = 73,
            GunState state = GunState.Reloading) => new()
        {
            eventId = id,
            serverTick = 1234,
            netId = netId,
            predictionEpoch = 9,
            inputTick = 513,
            weaponSlot = WeaponSlot.Rifle,
            canceledState = state
        };

        private static void CheckActionWireRoundTrip(NetworkEventMessage original)
        {
            using NetworkWriterPooled writer = NetworkWriterPool.Get();
            NetworkMessages.Pack(new NetworkEventEnvelope { payload = original }, writer);
            using NetworkReaderPooled reader = NetworkReaderPool.Get(writer.ToArraySegment());
            Require(NetworkMessages.UnpackId(reader, out ushort id) && id == NetworkMessageId<NetworkEventEnvelope>.Id,
                "武器动作事实必须通过统一 Mirror 事件封装传输。");
            NetworkEventMessage decoded = reader.Read<NetworkEventEnvelope>().payload;
            Require(decoded.GetType() == original.GetType() && decoded.eventId == original.eventId &&
                    decoded.serverTick == original.serverTick && decoded.netId == original.netId && reader.Remaining == 0,
                "武器动作事件的运行类型与公共头必须完整往返。");
            bool fieldsMatch = (original, decoded) switch
            {
                (ReloadEventMessage source, ReloadEventMessage target) =>
                    source.predictionEpoch == target.predictionEpoch && source.inputTick == target.inputTick &&
                    source.weaponSlot == target.weaponSlot && source.serverInitiated == target.serverInitiated,
                (PrepareEventMessage source, PrepareEventMessage target) =>
                    source.predictionEpoch == target.predictionEpoch && source.inputTick == target.inputTick &&
                    source.weaponSlot == target.weaponSlot && source.serverInitiated == target.serverInitiated,
                (WeaponActionCanceledEventMessage source, WeaponActionCanceledEventMessage target) =>
                    source.predictionEpoch == target.predictionEpoch && source.inputTick == target.inputTick &&
                    source.weaponSlot == target.weaponSlot && source.canceledState == target.canceledState &&
                    source.serverInitiated == target.serverInitiated,
                (KnifeAttackEventMessage source, KnifeAttackEventMessage target) =>
                    source.predictionEpoch == target.predictionEpoch && source.inputTick == target.inputTick &&
                    source.weaponSlot == target.weaponSlot && source.attackKind == target.attackKind,
                _ => false
            };
            Require(fieldsMatch, "武器动作事件的预测轮次、输入拍、栏位及被取消状态必须完整往返。");
        }

        private static void TestWeaponActionProtocol()
        {
            Require((byte)NetworkEventKind.Reload == 3 && (byte)NetworkEventKind.Prepare == 4 &&
                    (byte)NetworkEventKind.ActionCanceled == 5 && NetworkProtocol.Version >= 6,
                "新增武器动作应使用稳定线路类型码，并升级协议版本。");
            foreach (WeaponSlot slot in new[] { WeaponSlot.Rifle, WeaponSlot.Pistol })
            {
                ReloadEventMessage reload = ReloadEvent(0x0102030405060708UL);
                reload.weaponSlot = slot;
                reload.serverInitiated = slot == WeaponSlot.Pistol;
                CheckActionWireRoundTrip(reload);
                PrepareEventMessage prepare = PrepareEvent(0x0102030405060709UL);
                prepare.weaponSlot = slot;
                prepare.serverInitiated = slot == WeaponSlot.Pistol;
                CheckActionWireRoundTrip(prepare);
                foreach (GunState state in new[] { GunState.Preparing, GunState.Shooting, GunState.Reloading })
                {
                    WeaponActionCanceledEventMessage canceled = CanceledEvent(0x010203040506070AUL, 73, state);
                    canceled.weaponSlot = slot;
                    canceled.serverInitiated = slot == WeaponSlot.Pistol;
                    CheckActionWireRoundTrip(canceled);
                }
            }

            using var scope = new PreviewScope();
            scope.CreateClient();
            var publisher = new ServerEventPublisher(scope.Bridge);
            var input = new ServerPlayerInput(73, 9, null, new PlayerInputFrame { tick = 513 });
            var prepareMessage = (PrepareEventMessage)Invoke(publisher, "CreateMessage",
                new PrepareStartedEvent(1234, input, WeaponSlot.Rifle, true));
            var reloadMessage = (ReloadEventMessage)Invoke(publisher, "CreateMessage",
                new ReloadStartedEvent(1235, input, WeaponSlot.Pistol, true));
            var canceledMessage = (WeaponActionCanceledEventMessage)Invoke(publisher, "CreateMessage",
                new WeaponActionCanceledEvent(1236, input, WeaponSlot.Pistol, GunState.Reloading, true));
            var fireMessage = (FireEventMessage)Invoke(publisher, "CreateMessage", ShotEvent(Event()));
            Require(prepareMessage.eventId == 1 && prepareMessage.weaponSlot == WeaponSlot.Rifle &&
                    reloadMessage.eventId == 2 && reloadMessage.weaponSlot == WeaponSlot.Pistol &&
                    canceledMessage.eventId == 3 && canceledMessage.weaponSlot == WeaponSlot.Pistol &&
                    canceledMessage.canceledState == GunState.Reloading && fireMessage.eventId == 4,
                "所有枪械动作必须按提供顺序映射，并与开火共用连续全局序号。");
            Require(prepareMessage.predictionEpoch == 9 && prepareMessage.inputTick == 513 &&
                    reloadMessage.predictionEpoch == 9 && reloadMessage.inputTick == 513 &&
                    canceledMessage.predictionEpoch == 9 && canceledMessage.inputTick == 513 &&
                    prepareMessage.serverTick == 1234 && reloadMessage.serverTick == 1235 &&
                    canceledMessage.serverTick == 1236 && prepareMessage.netId == 73 &&
                    reloadMessage.netId == 73 && canceledMessage.netId == 73,
                "发布器必须保留动作来源的玩家、拍和预测轮次。");
            Require(prepareMessage.serverInitiated && reloadMessage.serverInitiated && canceledMessage.serverInitiated,
                "服务器主动动作标记必须从玩法事实传递到全部新动作消息。");
            CheckActionWireRoundTrip(prepareMessage);
            CheckActionWireRoundTrip(reloadMessage);
            CheckActionWireRoundTrip(canceledMessage);
        }

        private static void TestKnifeProtocolAndRouting()
        {
            Require((byte)NetworkEventKind.KnifeAttack == 6 && NetworkProtocol.Version >= 10,
                "刀攻击占用类型码 6，新输入格式要求协议版本至少为 10。");
            var input = new PlayerInputMessage { predictionEpoch = 9, frames = new[]
            {
                new PlayerInputFrame { tick = 513, fire = true, lightAttackPressed = true },
                new PlayerInputFrame { tick = 514, heavyAttackPressed = true }
            }};
            using (NetworkWriterPooled writer = NetworkWriterPool.Get())
            {
                NetworkMessages.Pack(input, writer);
                using NetworkReaderPooled reader = NetworkReaderPool.Get(writer.ToArraySegment());
                Require(NetworkMessages.UnpackId(reader, out ushort id) && id == NetworkMessageId<PlayerInputMessage>.Id,
                    "刀输入沿应走原有输入消息。");
                var decoded = reader.Read<PlayerInputMessage>();
                Require(decoded.predictionEpoch == 9 && decoded.frames.Length == 2 && decoded.frames[0].lightAttackPressed &&
                        !decoded.frames[0].heavyAttackPressed && decoded.frames[0].fire && !decoded.frames[1].lightAttackPressed &&
                        decoded.frames[1].heavyAttackPressed && !decoded.frames[1].fire && reader.Remaining == 0,
                    "左右键按下沿应独立往返，长按开火字段保持原值。");
            }

            using var remote = new PreviewScope();
            using var owner = new PreviewScope();
            remote.EquipRifle();
            owner.EquipRifle();
            remote.SetRole(PawnRole.Interpolated);
            var client = remote.CreateClient();
            client.Players.Add(73, remote.Root);
            client.Players.Add(74, owner.Root);
            int lights = 0, heavies = 0, ownerActions = 0, delivered = 0;
            remote.Controller.OnKnifeStateChanged += (_, state) =>
            {
                if (state == KnifeState.LightAttack) lights++;
                if (state == KnifeState.HeavyAttack) heavies++;
            };
            owner.Controller.OnKnifeStateChanged += (_, _) => ownerActions++;
            client.EventReceived += _ => delivered++;
            var publisher = new ServerEventPublisher(remote.Bridge);
            KnifeAttackEventMessage Create(uint netId, uint tick, KnifeAttackKind kind)
            {
                var source = new ServerPlayerInput(netId, 9, null, new PlayerInputFrame { tick = tick });
                return (KnifeAttackEventMessage)Invoke(publisher, "CreateMessage",
                    new KnifeAttackStartedEvent(tick + 100, source, WeaponSlot.Knife, kind));
            }
            foreach (KnifeAttackKind kind in new[] { KnifeAttackKind.Light, KnifeAttackKind.Heavy })
            {
                uint tick = kind == KnifeAttackKind.Light ? 513u : 514u;
                KnifeAttackEventMessage message = Create(73, tick, kind);
                CheckActionWireRoundTrip(message);
                Require(message.eventId == (kind == KnifeAttackKind.Light ? 1UL : 2UL) && message.netId == 73 &&
                        message.serverTick == tick + 100 && message.predictionEpoch == 9 && message.inputTick == tick &&
                        message.weaponSlot == WeaponSlot.Knife && message.attackKind == kind, "发布器应保留攻击事实的来源与类型。");
                Invoke(client, "HandleNetworkEvent", message);
                Invoke(client, "HandleNetworkEvent", message);
            }
            Invoke(client, "HandleNetworkEvent", Create(74, 515, KnifeAttackKind.Heavy));
            Require(delivered == 3 && lights == 1 && heavies == 1 && ownerActions == 0 &&
                    remote.Holder.CurrentSlot == WeaponSlot.Knife && owner.Holder.CurrentSlot == WeaponSlot.Rifle &&
                    remote.Controller.LocalTick == 0 && remote.Controller.ServerTick == 0,
                "每个远端动作只播放一次，不启动远端模拟；拥有者确认不能覆盖本地表现。");
        }

        private static void TestAuthoritativeReloadLifecycle()
        {
            using var scope = new PreviewScope();
            AK47 gun = scope.EquipRifle();
            scope.SetRole(PawnRole.Authoritative);
            for (uint tick = 0; tick <= gun.Config.PrepareTicks; tick++) scope.Controller.StepServer(false);
            IReadOnlyList<PlayerWeaponAction> actions = scope.Controller.ConsumeActions();
            Require(actions.Count == 1 && actions[0].Kind == PlayerWeaponActionKind.PrepareStarted,
                "权威准备入口应记录一次真实准备开始。");
            Require(scope.Controller.StepServer(true), "换弹验证需要先成功发射一发。");
            scope.Controller.ConsumeActions();
            int rounds = gun.Magazine.Rounds;
            int reserve = gun.Magazine.ReserveRounds;
            Require(gun.TryReload(), "射击间隔内应允许开始换弹。");
            actions = scope.Controller.ConsumeActions();
            Require(actions.Count == 2 && actions[0].Kind == PlayerWeaponActionKind.Canceled &&
                    actions[0].CanceledState == GunState.Shooting && actions[1].Kind == PlayerWeaponActionKind.ReloadStarted,
                "射击退出与换弹开始应按枪械真实生命周期顺序记录。");
            Require(gun.TryReturnToNormal(), "换弹应允许提前返回 Normal。");
            actions = scope.Controller.ConsumeActions();
            Require(actions.Count == 1 && actions[0].Kind == PlayerWeaponActionKind.Canceled &&
                    actions[0].CanceledState == GunState.Reloading && gun.Magazine.Rounds == rounds &&
                    gun.Magazine.ReserveRounds == reserve,
                "提前退出换弹只记录取消，弹匣与备弹均保持退出前数值。");
            Require(gun.TryReload() && gun.TryPrepare(), "换弹可被同枪重新准备打断。");
            actions = scope.Controller.ConsumeActions();
            Require(actions.Count == 3 && actions[0].Kind == PlayerWeaponActionKind.ReloadStarted &&
                    actions[1].Kind == PlayerWeaponActionKind.Canceled &&
                    actions[1].CanceledState == GunState.Reloading && actions[2].Kind == PlayerWeaponActionKind.PrepareStarted,
                "同枪换弹、取消、准备必须逐项保留，最终状态不能覆盖中间事实。");
            for (uint tick = 0; tick < gun.Config.PrepareTicks; tick++) scope.Controller.StepServer(false);
            int completions = 0;
            gun.OnReloadCompleted += () => completions++;
            Require(gun.TryReload(), "准备完成后应允许再次换弹。");
            for (uint tick = 0; tick < gun.Config.ReloadTicks; tick++) scope.Controller.StepServer(false);
            Require(gun.GunState == GunState.Normal && gun.Magazine.Rounds == gun.Magazine.Capacity &&
                    gun.Magazine.ReserveRounds == reserve - 1 && completions == 1,
                "完整权威换弹应按配置时长补回缺失的一发，并只完成一次。");
            scope.Controller.StepServer(false);
            actions = scope.Controller.ConsumeActions();
            Require(completions == 1 && actions.Count == 1 && actions[0].Kind == PlayerWeaponActionKind.ReloadStarted &&
                    scope.Controller.ConsumeActions().Count == 0,
                "自然完成不能误发取消，消费事实后不能在后续拍重复广播。");
        }

        private static void TestRemoteReloadRouting()
        {
            using var remote = new PreviewScope();
            using var owner = new PreviewScope();
            AK47 remoteGun = remote.EquipRifle();
            AK47 ownerGun = owner.EquipRifle();
            remote.SetRole(PawnRole.Interpolated);
            ClientManager client = remote.CreateClient();
            client.Players.Add(73, remote.Root);
            client.Players.Add(74, owner.Root);
            int reloads = 0, completions = 0, cancellations = 0, ownerReloads = 0, delivered = 0;
            remote.Controller.OnReload += _ => reloads++;
            remote.Controller.OnReloadEnd += _ => completions++;
            remote.Controller.OnActionCanceled += (_, state) => { if (state == GunState.Reloading) cancellations++; };
            owner.Controller.OnReload += _ => ownerReloads++;
            client.EventReceived += _ => delivered++;
            int rounds = remoteGun.Magazine.Rounds;
            int reserve = remoteGun.Magazine.ReserveRounds;
            int ownerRounds = ownerGun.Magazine.Rounds;

            Invoke(client, "HandleNetworkEvent", ReloadEvent(1));
            Invoke(client, "HandleNetworkEvent", ReloadEvent(1));
            Require(reloads == 1 && remote.Controller.IsRemoteReloading, "重复换弹事件只启动一次指定远端玩家表现。");
            Invoke(client, "HandleNetworkEvent", CanceledEvent(2));
            Invoke(client, "HandleNetworkEvent", CanceledEvent(2));
            Require(!remote.Controller.IsRemoteReloading && cancellations == 1 && completions == 0,
                "权威提前返回 Normal 时，取消消息应立即结束远端换弹且不冒充正常完成。");
            Invoke(client, "HandleNetworkEvent", ReloadEvent(3));
            Invoke(client, "HandleNetworkEvent", CanceledEvent(4));
            Invoke(client, "HandleNetworkEvent", PrepareEvent(5));
            Require(!remote.Controller.IsRemoteReloading && remote.Controller.IsRemotePreparing && cancellations == 2,
                "取消与准备按可靠事件顺序到达，应从换弹接续到同枪准备。");
            Invoke(client, "HandleNetworkEvent", CanceledEvent(6, 73, GunState.Preparing));
            Invoke(client, "HandleNetworkEvent", ReloadEvent(7));
            ExpireRemoteAction(remote.Controller);
            Invoke(remote.Controller, "Update");
            Invoke(remote.Controller, "Update");
            Invoke(client, "HandleNetworkEvent", ReloadEvent(8, 74));
            Require(reloads == 3 && completions == 1 && !remote.Controller.IsRemoteReloading &&
                    ownerReloads == 0 && delivered == 8 && remoteGun.Magazine.Rounds == rounds &&
                    remoteGun.Magazine.ReserveRounds == reserve && ownerGun.Magazine.Rounds == ownerRounds &&
                    remote.Controller.LocalTick == 0 && remote.Controller.ServerTick == 0,
                "远端自然结束只恢复一次表现，本地确认沿用预测，两者均不重复修改枪械弹药与模拟时钟。");
        }

        private static void TestServerInitiatedOwnerActions()
        {
            using var scope = new PreviewScope();
            AK47 gun = scope.EquipRifle();
            ClientManager client = scope.CreateClient();
            client.Players.Add(73, scope.Root);
            for (uint tick = 0; tick <= gun.Config.PrepareTicks; tick++) scope.Controller.StepLocal(true);
            for (uint tick = 0; tick < gun.Config.ShotIntervalTicks; tick++) scope.Controller.StepLocal(false);
            int rounds = gun.Magazine.Rounds;
            int reserve = gun.Magazine.ReserveRounds;
            int reloads = 0, cancellations = 0, prepares = 0;
            scope.Controller.OnReload += _ => reloads++;
            scope.Controller.OnActionCanceled += (_, state) => { if (state == GunState.Reloading) cancellations++; };
            scope.Controller.OnPrepare += _ => prepares++;

            Invoke(client, "HandleNetworkEvent", ReloadEvent(1));
            Require(gun.GunState == GunState.Normal && reloads == 0,
                "客户端自身输入产生的权威确认不得再次执行换弹。");
            ReloadEventMessage reload = ReloadEvent(2);
            reload.serverInitiated = true;
            Invoke(client, "HandleNetworkEvent", reload);
            Invoke(client, "HandleNetworkEvent", reload);
            Require(gun.GunState == GunState.Reloading && reloads == 1,
                "服务器主动换弹应同步到拥有者枪械，重复消息只执行一次。");

            WeaponActionCanceledEventMessage canceled = CanceledEvent(3);
            canceled.serverInitiated = true;
            Invoke(client, "HandleNetworkEvent", canceled);
            Require(gun.GunState == GunState.Normal && cancellations == 1 &&
                    gun.Magazine.Rounds == rounds && gun.Magazine.ReserveRounds == reserve,
                "服务器主动取消应结束拥有者换弹，提前结束保持取消前弹药数量。");
            PrepareEventMessage prepare = PrepareEvent(4);
            prepare.serverInitiated = true;
            Invoke(client, "HandleNetworkEvent", prepare);
            Require(gun.GunState == GunState.Preparing && prepares == 1,
                "服务器同枪重新准备应同步到拥有者状态机。");
            for (uint tick = 0; tick < gun.Config.PrepareTicks; tick++) scope.Controller.StepLocal(false);

            WeaponSwitchEventMessage toKnife = SwitchEvent(5, 73, WeaponSlot.Knife);
            toKnife.serverInitiated = true;
            Invoke(client, "HandleNetworkEvent", toKnife);
            Require(scope.Holder.CurrentSlot == WeaponSlot.Knife && gun.GunState == GunState.None,
                "服务器主动切刀应同步拥有者选择并收起原枪。");
            Invoke(client, "HandleNetworkEvent", SwitchEvent(6, 73, WeaponSlot.Rifle));
            Require(scope.Holder.CurrentSlot == WeaponSlot.Knife,
                "同源切枪确认应保留拥有者后来选择的栏位。");
            WeaponSwitchEventMessage toRifle = SwitchEvent(7, 73, WeaponSlot.Rifle);
            toRifle.serverInitiated = true;
            Invoke(client, "HandleNetworkEvent", toRifle);
            Require(scope.Holder.CurrentSlot == WeaponSlot.Rifle && gun.GunState == GunState.None,
                "服务器主动切回枪械先同步栏位，由后续准备事实启动动作。");
            prepare.eventId = 8;
            Invoke(client, "HandleNetworkEvent", prepare);
            Require(gun.GunState == GunState.Preparing && prepares == 2 &&
                    gun.Magazine.Rounds == rounds && gun.Magazine.ReserveRounds == reserve,
                "主动切枪与准备按事件顺序接续，不重复修改弹药。");
        }

        private static void ExpireRemoteAction(PlayerWeaponController controller)
        {
            FieldInfo start = typeof(PlayerWeaponController).GetField("_remoteActionStartTime", PrivateInstance);
            FieldInfo duration = typeof(PlayerWeaponController).GetField("_remoteActionDuration", PrivateInstance);
            Require(start != null && duration != null, "远端准备与换弹应持有独立表现计时。");
            start.SetValue(controller, Time.unscaledTimeAsDouble - (double)duration.GetValue(controller) - 1d);
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

        private sealed class UnknownGameEvent : ServerGameEvent
        {
            public UnknownGameEvent() : base(1, 2, 3, 4) { }
        }

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
                AK47SO source = AssetDatabase.LoadAssetAtPath<AK47SO>("Assets/SO/GunData/Definitions/AK47SO.asset");
                Require(source != null, "真实 AK47SO 必须存在。");
                Definition = UnityEngine.Object.Instantiate(source);
                Definition.hideFlags = HideFlags.HideAndDontSave;
                var serialized = new SerializedObject(Definition);
                serialized.FindProperty("_initialTotalBullets").intValue = 20;
                serialized.FindProperty("_capacity").intValue = 4;
                serialized.FindProperty("_prepareTicks").intValue = 2;
                serialized.FindProperty("_shotIntervalTicks").intValue = 3;
                serialized.FindProperty("_reloadTicks").intValue = 3;
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
                PlayerMovementController movementController = Root.GetComponent<PlayerMovementController>();
                Require(movementController != null, "运动模拟必须自动补齐独立的移动控制器。");
                _movement = ScriptableObject.CreateInstance<PlayerMovementDataSO>();
                _movement.hideFlags = HideFlags.HideAndDontSave;
                typeof(PlayerMovementController).GetField("_playerMovementDataSO", PrivateInstance).SetValue(movementController, _movement);
                typeof(PlayerMovementController).GetField("_gravityScale", PrivateInstance).SetValue(movementController, 0f);
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
