using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using GamePlay.Player;
using GamePlay.Player.Simulation;
using Mirror;
using Network.Client;
using Network.Common;
using Network.Common.Messages;
using Network.Server;
using SessionState = Network.Client.SessionState;
using Unity.Pipeline.Commands;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FPSGame.Editor.Validation
{
    /// <summary>在隔离预览场景中验证服务端数值路由、基准与订阅生命周期。</summary>
    public static class PlayerStatsServerValidation
    {
        private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
        private static bool _running;

        [CliCommand("player_stats_server_validate", "Validate server stats routing, baselines and lifecycle cleanup", Tags = new[] { "fpsgame/validation" })]
        public static string Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || NetworkServer.active || NetworkClient.active)
                return "REFUSED 请先退出 Play Mode 并停止联网运行，再验证隔离服务端。";
            if (_running)
                return "REFUSED 服务端数值验证已经在执行。";

            _running = true;
            var report = new StringBuilder();
            int passed = 0;
            try
            {
                RunCase("出生数字仅发拥有者，公开存活仅发 Ready 连接", TestInitialRouting, report, ref passed);
                RunCase("中途加入取得既有死亡基准和自己的完整数字", TestJoiningBaselines, report, ref passed);
                RunCase("无玩家输入时仍逐次发送数值快照，正常受伤不广播存活", TestChangesWithoutInputs, report, ref passed);
                RunCase("同拍死亡与重置按原顺序同步完整数字和存活转换", TestDeathResetOrder, report, ref passed);
                RunCase("移除连接幂等清除订阅及待发记录，保留其它玩家", TestRemoveClient, report, ref passed);
                RunCase("数值组件停止后注销订阅并移除待发状态", TestStatsStopped, report, ref passed);
                RunCase("服务器重复关闭清除全部数值订阅与队列", TestShutdown, report, ref passed);
                RunCase("输入处理故障通过统一入口清理数值并断线（预期一条错误日志）", TestInputFailure, report, ref passed);
                RunCase("等待首批输入超时清理数值并断线", TestFirstInputTimeout, report, ref passed);
                return "PASS " + passed + " checks\n" + report;
            }
            finally { _running = false; }
        }

        private static void TestInitialRouting()
        {
            using var scope = new PreviewScope();
            CapturedConnection observer = scope.CreateConnection(2);
            CapturedConnection notReady = scope.CreateConnection(3, false);
            TestPlayer player = scope.CreatePlayer(1);
            NetworkMessage[] ownerMessages = player.Connection.Drain();
            PlayerStatsStateMessage[] numbers = ownerMessages.OfType<PlayerStatsStateMessage>().ToArray();
            PlayerLifeStateMessage[] life = ownerMessages.OfType<PlayerLifeStateMessage>().ToArray();
            Require(numbers.Length == 1 && numbers[0].netId == player.Id && numbers[0].state.Health == 100 &&
                    numbers[0].state.Revision == 1, "拥有者应取得唯一且完整的出生数值基准。 ");
            Require(life.Length == 1 && life[0].netId == player.Id && life[0].lifeState == PlayerLifeState.Alive,
                "拥有者应独立接收自己的公共存活基准。");
            NetworkMessage[] publicMessages = observer.Drain();
            Require(publicMessages.Length == 1 && publicMessages[0] is PlayerLifeStateMessage publicLife &&
                    publicLife.netId == player.Id && publicLife.revision == 1,
                "观察者只应取得存活消息，不应取得他人的完整数值。");
            Require(notReady.Drain().Length == 0, "未 Ready 的连接不能接收公共存活广播。");
        }

        private static void TestJoiningBaselines()
        {
            using var scope = new PreviewScope();
            TestPlayer existing = scope.CreatePlayer(1);
            scope.DrainAll();
            existing.Stats.ApplyDamage(100);
            scope.Flush();
            scope.DrainAll();
            TestPlayer joining = scope.CreatePlayer(2);
            NetworkMessage[] received = joining.Connection.Drain();
            PlayerStatsStateMessage[] numbers = received.OfType<PlayerStatsStateMessage>().ToArray();
            PlayerLifeStateMessage[] life = received.OfType<PlayerLifeStateMessage>().ToArray();
            Require(numbers.Length == 1 && numbers[0].netId == joining.Id && numbers[0].state.Health == 100,
                "新连接只应取得自身完整数值。");
            Require(life.Length == 2 && life.Any(value => value.netId == existing.Id &&
                    value.lifeState == PlayerLifeState.Dead && value.revision == 2) &&
                    life.Any(value => value.netId == joining.Id && value.lifeState == PlayerLifeState.Alive),
                "新连接必须取得所有现存玩家的当前存活基准，包括已经死亡的玩家。");
            NetworkMessage[] previous = existing.Connection.Drain();
            Require(previous.Length == 1 && previous[0] is PlayerLifeStateMessage newLife &&
                    newLife.netId == joining.Id && newLife.lifeState == PlayerLifeState.Alive,
                "既有连接应只收到新增玩家的公开存活基准。");
        }

        private static void TestChangesWithoutInputs()
        {
            using var scope = new PreviewScope();
            CapturedConnection observer = scope.CreateConnection(2);
            TestPlayer player = scope.CreatePlayer(1);
            scope.DrainAll();
            player.Stats.ApplyDamage(10);
            player.Stats.Heal(5);
            player.Stats.RestoreArmor(25);
            Require(scope.PendingCount == 3 && player.Connection.Drain().Length == 0,
                "实际操作应先记录不可变结果并等待模拟拍结束。");
            Invoke(scope.Server, "SimulateTick");
            PlayerStatsStateMessage[] numbers = player.Connection.Drain().OfType<PlayerStatsStateMessage>().ToArray();
            Require(numbers.Length == 3 && numbers[0].state.Health == 90 && numbers[1].state.Health == 95 &&
                    numbers[2].state.Health == 95 && numbers[2].state.Armor == 25 &&
                    numbers.Select(value => value.state.Revision).SequenceEqual(new ulong[] { 2, 3, 4 }) &&
                    scope.PendingCount == 0,
                "等待首批输入的玩家仍应依次同步受伤、治疗和护甲恢复。");
            Require(!observer.Drain().Any(value => value is PlayerStatsStateMessage || value is PlayerLifeStateMessage),
                "保持存活的数字变化不能泄露完整状态或重复广播存活。");
        }

        private static void TestDeathResetOrder()
        {
            using var scope = new PreviewScope();
            CapturedConnection observer = scope.CreateConnection(2);
            TestPlayer player = scope.CreatePlayer(1);
            scope.DrainAll();
            player.Stats.ApplyDamage(100);
            player.Stats.ResetForSpawn();
            Require(scope.PendingCount == 2, "同拍死亡与重置必须各自保留待发结果。");
            scope.Flush();
            NetworkMessage[] owner = player.Connection.Drain();
            Require(owner.Length == 4 &&
                    owner[0] is PlayerStatsStateMessage dead && dead.state.Health == 0 && dead.state.Revision == 2 &&
                    owner[1] is PlayerLifeStateMessage died && died.lifeState == PlayerLifeState.Dead && died.revision == 2 &&
                    owner[2] is PlayerStatsStateMessage reset && reset.state.Health == 100 && reset.state.Revision == 3 &&
                    owner[3] is PlayerLifeStateMessage alive && alive.lifeState == PlayerLifeState.Alive && alive.revision == 3,
                "拥有者必须依次收到死亡数字、死亡状态、重置数字和存活状态。");
            PlayerLifeStateMessage[] publicLife = observer.Drain().OfType<PlayerLifeStateMessage>().ToArray();
            Require(publicLife.Length == 2 && publicLife[0].lifeState == PlayerLifeState.Dead &&
                    publicLife[1].lifeState == PlayerLifeState.Alive && publicLife[0].revision < publicLife[1].revision,
                "观察者必须保留死亡和重新存活两个有序转换。");
        }

        private static void TestRemoveClient()
        {
            using var scope = new PreviewScope();
            TestPlayer removed = scope.CreatePlayer(1);
            TestPlayer remaining = scope.CreatePlayer(2);
            scope.DrainAll();
            removed.Stats.ApplyDamage(10);
            remaining.Stats.ApplyDamage(20);
            scope.Server.RemoveClient(removed.Connection);
            scope.Server.RemoveClient(removed.Connection);
            removed.Stats.Heal(5);
            Require(scope.BindingCount == 1 && scope.PendingCount == 1 && scope.Server.Sessions.Count == 1,
                "重复移除不能保留旧订阅，也不能清除其它连接的待发状态。");
            scope.Flush();
            Require(removed.Connection.Drain().Length == 0 &&
                    remaining.Connection.Drain().OfType<PlayerStatsStateMessage>().Single().state.Health == 80,
                "已移除玩家不得继续收到数字，其余玩家仍应正常同步。");
        }

        private static void TestStatsStopped()
        {
            using var scope = new PreviewScope();
            TestPlayer player = scope.CreatePlayer(1);
            scope.DrainAll();
            player.Stats.ApplyDamage(10);
            player.Stats.Shutdown();
            Require(scope.BindingCount == 0 && scope.PendingCount == 0,
                "组件停止必须移除服务端绑定和所有旧生命周期的待发记录。");
            player.Stats.InitializeServer();
            player.Stats.ApplyDamage(10);
            Require(scope.PendingCount == 0, "组件重新初始化后不得继承旧网络订阅。");
            scope.Flush();
            Require(player.Connection.Drain().Length == 0, "已停止的绑定不能继续发送旧或新数值。");
        }

        private static void TestShutdown()
        {
            using var scope = new PreviewScope();
            TestPlayer first = scope.CreatePlayer(1);
            TestPlayer second = scope.CreatePlayer(2);
            scope.DrainAll();
            first.Stats.ApplyDamage(10);
            second.Stats.ApplyDamage(20);
            scope.Server.Shutdown();
            scope.Server.Shutdown();
            first.Stats.Heal(5);
            second.Stats.Heal(5);
            Require(scope.BindingCount == 0 && scope.PendingCount == 0,
                "服务器关闭必须取消全部订阅，后续组件变更不能重新入队。");
            Require(first.Connection.Drain().Length == 0 && second.Connection.Drain().Length == 0,
                "关闭时的待发记录不能被意外发送。");
        }

        private static void TestInputFailure()
        {
            using var scope = new PreviewScope();
            TestPlayer player = scope.CreatePlayer(1);
            scope.DrainAll();
            player.Stats.ApplyDamage(10);
            player.Session.AwaitingInput = false;
            Set(player.Brain, "<Role>k__BackingField", PawnRole.Predicted);
            Invoke(scope.Server, "SimulateTick");
            RequireCleanedPlayer(scope, player, "输入故障");
        }

        private static void TestFirstInputTimeout()
        {
            using var scope = new PreviewScope();
            TestPlayer player = scope.CreatePlayer(1);
            scope.DrainAll();
            player.Stats.ApplyDamage(10);
            player.Session.PredictionBaselineSentTime = Time.unscaledTimeAsDouble - 100d;
            Invoke(scope.Server, "SimulateTick");
            RequireCleanedPlayer(scope, player, "等待输入超时");
        }

        private static void RequireCleanedPlayer(PreviewScope scope, TestPlayer player, string reason)
        {
            Require(player.Connection.Disconnected && scope.Server.Sessions.Count == 0 &&
                    scope.BindingCount == 0 && scope.PendingCount == 0 && player.Connection.Drain().Length == 0,
                reason + "必须同时断线、移除会话并清除数值订阅与待发结果。");
            player.Stats.Heal(5);
            Require(scope.PendingCount == 0, reason + "之后的数值变更不能继续入队。");
        }

        private static object Invoke(object target, string method, params object[] arguments)
        {
            MethodInfo entry = target.GetType().GetMethod(method, PrivateInstance);
            Require(entry != null, "缺少隔离验证入口 " + method);
            return entry.Invoke(target, arguments);
        }

        private static object Get(object target, string field)
        {
            FieldInfo entry = target.GetType().GetField(field, PrivateInstance);
            Require(entry != null, "缺少隔离验证字段 " + field);
            return entry.GetValue(target);
        }

        private static void Set(object target, string field, object value)
        {
            FieldInfo entry = target.GetType().GetField(field, PrivateInstance);
            Require(entry != null, "缺少隔离验证字段 " + field);
            entry.SetValue(target, value);
        }

        private static void RunCase(string name, Action test, StringBuilder report, ref int passed)
        {
            try { test(); }
            catch (Exception exception) { throw new InvalidOperationException("服务端数值验证失败 " + name, exception); }
            passed++;
            report.AppendLine("PASS " + name);
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }

        private sealed class TestPlayer
        {
            public uint Id { get; }
            public CapturedConnection Connection { get; }
            public ClientSession Session { get; }
            public PlayerStatsController Stats { get; }
            public PlayerBrain Brain { get; }

            public TestPlayer(uint id, CapturedConnection connection, ClientSession session,
                PlayerStatsController stats, PlayerBrain brain)
            {
                Id = id;
                Connection = connection;
                Session = session;
                Stats = stats;
                Brain = brain;
            }
        }

        private sealed class CapturedConnection : NetworkConnectionToClient
        {
            public bool Disconnected { get; private set; }

            public CapturedConnection(int id) : base(id) { }

            public override void Disconnect()
            {
                Disconnected = true;
                isReady = false;
            }

            public NetworkMessage[] Drain()
            {
                var messages = new List<NetworkMessage>();
                foreach (KeyValuePair<int, Batcher> channel in batches)
                {
                    using NetworkWriterPooled writer = NetworkWriterPool.Get();
                    while (channel.Value.GetBatch(writer))
                    {
                        var unbatcher = new Unbatcher();
                        Require(unbatcher.AddBatch(writer.ToArraySegment()), "连接输出必须为有效 Mirror 批次。");
                        while (unbatcher.GetNextMessage(out ArraySegment<byte> bytes, out _))
                        {
                            using NetworkReaderPooled reader = NetworkReaderPool.Get(bytes);
                            Require(NetworkMessages.UnpackId(reader, out ushort id), "Mirror 消息必须带有效类型编号。");
                            NetworkMessage message;
                            if (id == NetworkMessageId<PlayerStatsStateMessage>.Id)
                                message = reader.Read<PlayerStatsStateMessage>();
                            else if (id == NetworkMessageId<PlayerLifeStateMessage>.Id)
                                message = reader.Read<PlayerLifeStateMessage>();
                            else if (id == NetworkMessageId<PredictionBaselineMessage>.Id)
                                message = reader.Read<PredictionBaselineMessage>();
                            else if (id == NetworkMessageId<PlayerSnapshotMessage>.Id)
                                message = reader.Read<PlayerSnapshotMessage>();
                            else
                                throw new InvalidOperationException("隔离连接收到未预期消息 " + id);

                            Require(reader.Remaining == 0, "真实线路消息解码后不能有多余字节。");
                            if (message is PlayerStatsStateMessage || message is PlayerLifeStateMessage)
                                Require(channel.Key == Channels.Reliable, "数值及存活状态必须使用可靠通道。");
                            messages.Add(message);
                        }
                        writer.Position = 0;
                    }
                }
                return messages.ToArray();
            }
        }

        private sealed class PreviewScope : IDisposable
        {
            private readonly Scene _scene = EditorSceneManager.NewPreviewScene();
            private readonly Transport _previousTransport = Transport.active;
            private readonly bool _previousServerActive = NetworkServer.active;
            private readonly Dictionary<int, NetworkConnectionToClient> _previousConnections = NetworkServer.connections;
            private readonly List<CapturedConnection> _connections = new();
            private readonly PlayerBasicDataSO _data;
            public ServerManager Server { get; }
            public int BindingCount => ((IDictionary)Get(Server, "_playerStatsBindings")).Count;
            public int PendingCount => ((IList)Get(Server, "_pendingPlayerStatsChanges")).Count;

            public PreviewScope()
            {
                GameObject runtime = CreateRoot("PlayerStatsServerValidation");
                Transport.active = runtime.AddComponent<TelepathyTransport>();
                NetworkServer.connections = new Dictionary<int, NetworkConnectionToClient>();
                typeof(NetworkServer).GetProperty(nameof(NetworkServer.active)).SetValue(null, true);
                _data = ScriptableObject.CreateInstance<PlayerBasicDataSO>();
                _data.hideFlags = HideFlags.HideAndDontSave;
                Set(_data, "_initialHealth", 100);
                Set(_data, "_maxHealth", 100);
                Set(_data, "_initialArmor", 0);
                Set(_data, "_maxArmor", 100);
                Set(_data, "_armorAbsorptionRatio", 0.5f);
                NetBridge bridge = runtime.AddComponent<NetBridge>();
                Server = runtime.AddComponent<ServerManager>();
                Server.Initialize(bridge);
            }

            public CapturedConnection CreateConnection(int id, bool ready = true)
            {
                var connection = new CapturedConnection(id) { isReady = ready };
                NetworkServer.connections.Add(id, connection);
                _connections.Add(connection);
                return connection;
            }

            public TestPlayer CreatePlayer(int connectionId)
            {
                CapturedConnection connection = CreateConnection(connectionId);
                GameObject root = CreateRoot("PlayerStatsServerValidationPlayer");
                NetworkIdentity identity = root.AddComponent<NetworkIdentity>();
                uint id = (uint)(1000 + connectionId);
                typeof(NetworkIdentity).GetProperty(nameof(NetworkIdentity.netId)).SetValue(identity, id);
                typeof(NetworkConnection).GetProperty(nameof(NetworkConnection.identity)).SetValue(connection, identity);
                PlayerBrain brain = root.AddComponent<PlayerBrain>();
                Set(brain, "<Role>k__BackingField", PawnRole.Authoritative);
                PlayerStatsController stats = root.AddComponent<PlayerStatsController>();
                Set(stats, "_playerBasicData", _data);
                Set(brain, "<Stats>k__BackingField", stats);
                Set(brain, "<Simulation>k__BackingField", root.AddComponent<PlayerSimulation>());
                stats.InitializeServer();
                Server.AddClient(connection);
                Require(Server.Sessions.TryGet(connectionId, out ClientSession session), "连接应注册服务器会话。");
                session.State = SessionState.Ready;
                Server.BindPlayer(connection);
                return new TestPlayer(id, connection, session, stats, brain);
            }

            public void Flush() => Invoke(Server, "FlushPlayerStatsChanges");

            public void DrainAll()
            {
                foreach (CapturedConnection connection in _connections)
                    connection.Drain();
            }

            private GameObject CreateRoot(string name)
            {
                GameObject root = EditorUtility.CreateGameObjectWithHideFlags(name, HideFlags.HideAndDontSave);
                root.SetActive(false);
                SceneManager.MoveGameObjectToScene(root, _scene);
                return root;
            }

            public void Dispose()
            {
                try
                {
                    if (Server != null) Server.Shutdown();
                    DrainAll();
                }
                finally
                {
                    NetworkServer.connections = _previousConnections;
                    typeof(NetworkServer).GetProperty(nameof(NetworkServer.active)).SetValue(null, _previousServerActive);
                    Transport.active = _previousTransport;
                    if (_scene.IsValid()) EditorSceneManager.ClosePreviewScene(_scene);
                    if (_data != null) UnityEngine.Object.DestroyImmediate(_data);
                }
            }
        }
    }
}
