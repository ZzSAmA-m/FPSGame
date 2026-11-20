using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using GamePlay.Player;
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
    /// <summary>隔离验证玩家数值、权威边界、消息编解码与生成前状态缓存。</summary>
    public static class PlayerStatsValidation
    {
        private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
        private static bool _running;

        [CliCommand("player_stats_validate", "Validate player health, armor, authoritative state and reliable message delivery", Tags = new[] { "fpsgame/validation" })]
        public static string Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return "REFUSED：请先退出 Play Mode，再运行隔离玩家数值验证。";
            if (_running) return "REFUSED：玩家数值验证已经在执行。";
            _running = true;
            var report = new StringBuilder();
            int passed = 0;
            try
            {
                RunCase("共享配置 SO 的玩家拥有独立数值状态", TestSharedConfiguration, report, ref passed);
                RunCase("旧血量字段迁移后保留初始值与上限", TestLegacyConfiguration, report, ref passed);
                RunCase("无护甲、充足护甲和不足护甲服从等额分摊", TestArmorAllocation, report, ref passed);
                RunCase("奇数伤害向下取整，零与全部分摊比例可用", TestArmorRounding, report, ref passed);
                RunCase("非正输入和满值恢复不产生变更或增加版本", TestNoChanges, report, ref passed);
                RunCase("大整数伤害与恢复安全限制在数值范围", TestIntegerLimits, report, ref passed);
                RunCase("死亡每次生命过程只触发一次，重置保留递增版本", TestDeathAndReset, report, ref passed);
                RunCase("客户端只接受权威结果，远端玩家不暴露完整数值", TestClientAuthority, report, ref passed);
                RunCase("数值与存活状态独立去重，死亡通知只来自存活状态", TestIndependentStreams, report, ref passed);
                RunCase("首次死亡基准只建立状态，同拍死亡重置顺序保留", TestLifeBaselines, report, ref passed);
                RunCase("服务器回调重入出生重置仍按提交顺序通知", TestServerReentrancy, report, ref passed);
                RunCase("客户端回调重入新版本保留数字与生命通知顺序", TestClientReentrancy, report, ref passed);
                RunCase("回调内关闭组件会结束旧批次通知", TestNotificationShutdown, report, ref passed);
                RunCase("非法数值与存活消息被拒绝", TestInvalidStates, report, ref passed);
                RunCase("两种数值消息经过真实 Mirror 打包解包后完整保留", TestWireRoundTrip, report, ref passed);
                RunCase("生成前缓存保留最新状态，注册后立即应用", TestPendingMessages, report, ref passed);
                RunCase("远端注册后仅接收公共存活状态", TestRemoteDelivery, report, ref passed);
                RunCase("玩家注销与客户端关闭清理待注册状态", TestClientCleanup, report, ref passed);
                RunCase("运动恢复重放保持玩家当前数值", TestMovementReplay, report, ref passed);
                RunCase("玩家预制体引用数值 SO，旧配置迁移为出生生命及上限", TestPrefabConfiguration, report, ref passed);
                return "PASS " + passed + " checks\n" + report;
            }
            finally { _running = false; }
        }

        private static void TestSharedConfiguration()
        {
            using var scope = new PreviewScope();
            PlayerStatsController first = scope.CreateServer();
            PlayerStatsController second = scope.CreateServer();
            PlayerStatsState before = State(second);
            first.ApplyDamage(30);
            first.InitializeServer();
            Require(State(first).Health == 70 && State(second).Equals(before),
                "同一 SO 上的两名玩家必须各自保存生命、护甲和版本。");
            Require(scope.Data.InitialHealth == 100 && scope.Data.InitialArmor == 0,
                "运行时受伤不能写回共享配置。");
        }

        private static void TestLegacyConfiguration()
        {
            using var scope = new PreviewScope();
            Set(scope.Data, "_playerHealth", 175);
            scope.Data.OnAfterDeserialize();
            Require(scope.Data.InitialHealth == 175 && scope.Data.MaxHealth == 175,
                "旧配置的单个血量值应同时迁移为初始生命和生命上限。");
            Set(scope.Data, "_initialHealth", 150);
            scope.Data.OnAfterDeserialize();
            Require(scope.Data.InitialHealth == 150 && scope.Data.MaxHealth == 175,
                "迁移完成后再次反序列化不能用遗留字段覆盖新的数值配置。");
        }

        private static void TestArmorAllocation()
        {
            using var scope = new PreviewScope();
            PlayerStatsController stats = scope.CreateServer();
            PlayerStatsChange noArmor = stats.ApplyDamage(40);
            Require(noArmor.HealthLost == 40 && noArmor.ArmorLost == 0 && State(stats).Health == 60,
                "无护甲时完整伤害应扣除生命。");
            stats.ResetForSpawn();
            stats.RestoreArmor(100);
            PlayerStatsChange armored = stats.ApplyDamage(100);
            Require(armored.HealthLost == 50 && armored.ArmorLost == 50 &&
                    State(stats).Health == 50 && State(stats).Armor == 50,
                "50% 分摊时伤害 100 应扣生命 50、护甲 50。");
            stats.ResetForSpawn();
            stats.RestoreArmor(20);
            PlayerStatsChange partial = stats.ApplyDamage(100);
            Require(partial.HealthLost == 80 && partial.ArmorLost == 20 &&
                    State(stats).Health == 20 && State(stats).Armor == 0,
                "护甲不足时未分摊的伤害必须回到生命。");
        }

        private static void TestArmorRounding()
        {
            using var scope = new PreviewScope();
            PlayerStatsController stats = scope.CreateServer();
            stats.RestoreArmor(100);
            PlayerStatsChange odd = stats.ApplyDamage(3);
            Require(odd.HealthLost == 2 && odd.ArmorLost == 1, "奇数伤害的护甲分摊应向下取整。");
            Set(scope.Data, "_armorAbsorptionRatio", 0f);
            PlayerStatsController zero = scope.CreateServer();
            zero.RestoreArmor(100);
            zero.ApplyDamage(9);
            Require(State(zero).Health == 91 && State(zero).Armor == 100, "零分摊比例应只消耗生命。");
            Set(scope.Data, "_armorAbsorptionRatio", 1f);
            PlayerStatsController full = scope.CreateServer();
            full.RestoreArmor(100);
            full.ApplyDamage(9);
            Require(State(full).Health == 100 && State(full).Armor == 91, "全部分摊比例应优先完整消耗护甲。");
        }

        private static void TestNoChanges()
        {
            using var scope = new PreviewScope();
            PlayerStatsController stats = scope.CreateServer();
            int changes = 0;
            stats.OnStatsChanged += _ => changes++;
            PlayerStatsState initial = State(stats);
            stats.ApplyDamage(0);
            stats.ApplyDamage(-1);
            stats.Heal(0);
            stats.Heal(-1);
            stats.Heal(int.MaxValue);
            stats.RestoreArmor(0);
            stats.RestoreArmor(-1);
            Require(State(stats).Equals(initial) && changes == 0, "无效输入和满血治疗必须保持版本与通知数量。");
            stats.RestoreArmor(int.MaxValue);
            PlayerStatsState full = State(stats);
            stats.RestoreArmor(int.MaxValue);
            Require(State(stats).Equals(full) && changes == 1, "满护甲后继续恢复不能产生额外变更。");
        }

        private static void TestIntegerLimits()
        {
            using var scope = new PreviewScope();
            PlayerStatsController stats = scope.CreateServer();
            stats.ApplyDamage(1);
            stats.Heal(int.MaxValue);
            stats.RestoreArmor(int.MaxValue);
            Require(State(stats).Health == 100 && State(stats).Armor == 100,
                "大整数恢复不能溢出或越过上限。");
            stats.ApplyDamage(int.MaxValue);
            Require(State(stats).Health == 0 && State(stats).Armor == 0 && stats.LifeState == PlayerLifeState.Dead,
                "大整数伤害不能溢出为治疗或负护甲。");
        }

        private static void TestDeathAndReset()
        {
            using var scope = new PreviewScope();
            PlayerStatsController stats = scope.CreateServer();
            int deaths = 0;
            stats.OnDied += () => deaths++;
            stats.ApplyDamage(100);
            PlayerStatsState dead = State(stats);
            stats.ApplyDamage(100);
            stats.Heal(100);
            stats.RestoreArmor(100);
            Require(deaths == 1 && State(stats).Equals(dead), "死亡后的伤害及恢复不能建立新生命过程。");
            stats.ResetForSpawn();
            Require(State(stats).Revision > dead.Revision && State(stats).Health == 100 &&
                    stats.LifeState == PlayerLifeState.Alive,
                "出生重置必须恢复配置并增加同一对象的版本。");
            stats.ApplyDamage(100);
            Require(deaths == 2, "下一次生命过程应再次发出一次死亡通知。");
        }

        private static void TestClientAuthority()
        {
            using var scope = new PreviewScope();
            PlayerStatsController owner = scope.CreateClientPlayer(true);
            Require(!owner.TryGetState(out _), "本人在收到权威基准前不应拥有推测数值。");
            Require(ApplyState(owner, new PlayerStatsState(80, 100, 25, 100, 2)), "本人应接受有效完整状态。");
            PlayerStatsState before = State(owner);
            foreach (string operation in new[] { "ApplyDamage", "Heal", "RestoreArmor", "ResetForSpawn" })
            {
                try
                {
                    if (operation == "ResetForSpawn") owner.ResetForSpawn();
                    else Invoke(owner, operation, 100);
                }
                catch (InvalidOperationException) { }
                Require(State(owner).Equals(before), "客户端不能通过 " + operation + " 写入权威数值。");
            }
            PlayerStatsController remote = scope.CreateClientPlayer(false);
            Require(!ApplyState(remote, new PlayerStatsState(60, 100, 10, 100, 3)) &&
                    !remote.TryGetState(out _), "远端对象应保持完整数值不可读。");
            Require(ApplyLife(remote, PlayerLifeState.Alive, 3) && remote.LifeState == PlayerLifeState.Alive,
                "远端对象应能独立建立公共存活状态。");
        }

        private static void TestIndependentStreams()
        {
            using var scope = new PreviewScope();
            PlayerStatsController owner = scope.CreateClientPlayer(true);
            int deaths = 0;
            int changes = 0;
            owner.OnDied += () => deaths++;
            owner.OnStatsChanged += _ => changes++;
            Require(ApplyLife(owner, PlayerLifeState.Alive, 1), "首次公共存活基准应有效。");
            Require(ApplyState(owner, new PlayerStatsState(100, 100, 0, 100, 1)), "相同版本的数字基准应独立接收。");
            Require(ApplyState(owner, new PlayerStatsState(0, 100, 0, 100, 2)), "数字死亡结果应更新生命值。");
            Require(deaths == 0 && owner.LifeState == PlayerLifeState.Alive,
                "数字消息不能重复驱动公共生命状态或死亡表现。");
            Require(ApplyLife(owner, PlayerLifeState.Dead, 2) && deaths == 1, "公共死亡结果应只通知一次。");
            Require(!ApplyLife(owner, PlayerLifeState.Dead, 2) && !ApplyLife(owner, PlayerLifeState.Alive, 1) &&
                    !ApplyState(owner, new PlayerStatsState(100, 100, 100, 100, 1)) &&
                    deaths == 1 && changes == 2 && State(owner).Health == 0,
                "重复与旧版本不能恢复旧数值或重复死亡通知。");
        }

        private static void TestLifeBaselines()
        {
            using var scope = new PreviewScope();
            PlayerStatsController remote = scope.CreateClientPlayer(false);
            int deaths = 0;
            remote.OnDied += () => deaths++;
            Require(ApplyLife(remote, PlayerLifeState.Dead, 9) && deaths == 0,
                "中途加入看见已死亡角色时只建立死亡基准。");
            ApplyLife(remote, PlayerLifeState.Alive, 10);
            ApplyLife(remote, PlayerLifeState.Dead, 11);
            ApplyLife(remote, PlayerLifeState.Alive, 12);
            Require(deaths == 1 && remote.LifeState == PlayerLifeState.Alive,
                "同拍有序死亡和重置应保留中间死亡通知及最终存活状态。");
        }

        private static void TestInvalidStates()
        {
            using var scope = new PreviewScope();
            PlayerStatsController owner = scope.CreateClientPlayer(true);
            foreach (PlayerStatsState invalid in new[]
                     {
                         new PlayerStatsState(-1, 100, 0, 100, 1),
                         new PlayerStatsState(101, 100, 0, 100, 1),
                         new PlayerStatsState(100, 100, -1, 100, 1),
                         new PlayerStatsState(100, 100, 101, 100, 1),
                         new PlayerStatsState(100, 100, 0, 100, 0)
                     })
                Require(!invalid.IsValid && !ApplyState(owner, invalid), "非法数值快照必须被拒绝。");
            Require(!ApplyLife(owner, PlayerLifeState.Uninitialized, 1) &&
                    !ApplyLife(owner, (PlayerLifeState)255, 1) && !ApplyLife(owner, PlayerLifeState.Alive, 0) &&
                    !owner.TryGetState(out _) && owner.LifeState == PlayerLifeState.Uninitialized,
                "非法公共状态不能初始化玩家。");
        }

        private static void TestServerReentrancy()
        {
            using var scope = new PreviewScope();
            PlayerStatsController stats = scope.CreateServer();
            var versions = new List<ulong>();
            var lifeStates = new List<PlayerLifeState>();
            int deaths = 0;
            stats.OnStatsChanged += change =>
            {
                if (change.After.Health == 0) stats.ResetForSpawn();
            };
            stats.OnStatsChanged += change => versions.Add(change.After.Revision);
            stats.OnLifeStateChanged += lifeStates.Add;
            stats.OnDied += () => deaths++;
            stats.ApplyDamage(100);
            Require(versions.Count == 2 && versions[0] == 2 && versions[1] == 3 &&
                    lifeStates.Count == 2 && lifeStates[0] == PlayerLifeState.Dead &&
                    lifeStates[1] == PlayerLifeState.Alive && deaths == 1 && State(stats).Health == 100,
                "数值监听者重入重置不能让后续监听者先看到较新版本，也不能丢失死亡转换。");
        }

        private static void TestClientReentrancy()
        {
            using var scope = new PreviewScope();
            PlayerStatsController stats = scope.CreateClientPlayer(true);
            var versions = new List<ulong>();
            stats.OnStatsChanged += change =>
            {
                if (change.After.Revision == 1)
                    ApplyState(stats, new PlayerStatsState(80, 100, 0, 100, 2));
            };
            stats.OnStatsChanged += change => versions.Add(change.After.Revision);
            ApplyState(stats, new PlayerStatsState(100, 100, 0, 100, 1));
            Require(versions.Count == 2 && versions[0] == 1 && versions[1] == 2 && State(stats).Health == 80,
                "客户端数值回调重入较新快照时仍应按版本递增通知全部监听者。");
            ApplyLife(stats, PlayerLifeState.Alive, 1);
            var lifeStates = new List<PlayerLifeState>();
            int deaths = 0;
            stats.OnLifeStateChanged += lifeState =>
            {
                if (lifeState == PlayerLifeState.Dead) ApplyLife(stats, PlayerLifeState.Alive, 3);
            };
            stats.OnLifeStateChanged += lifeStates.Add;
            stats.OnDied += () => deaths++;
            ApplyLife(stats, PlayerLifeState.Dead, 2);
            Require(lifeStates.Count == 2 && lifeStates[0] == PlayerLifeState.Dead &&
                    lifeStates[1] == PlayerLifeState.Alive && deaths == 1 && stats.LifeState == PlayerLifeState.Alive,
                "客户端生命状态回调重入时仍应保留一次死亡和后续存活转换。");
        }

        private static void TestNotificationShutdown()
        {
            using var scope = new PreviewScope();
            PlayerStatsController server = scope.CreateServer();
            int laterNotifications = 0;
            int stopped = 0;
            server.OnStatsChanged += _ => server.Shutdown();
            server.OnStatsChanged += _ => laterNotifications++;
            server.OnLifeStateChanged += _ => laterNotifications++;
            server.OnDied += () => laterNotifications++;
            server.Stopped += () => stopped++;
            server.ApplyDamage(100);
            server.Shutdown();
            Require(laterNotifications == 0 && stopped == 1 && !server.TryGetState(out _) &&
                    server.LifeState == PlayerLifeState.Uninitialized,
                "服务器监听者关闭组件后，应停止旧数值批次的后续通知，并幂等清理。");
            PlayerStatsController owner = scope.CreateClientPlayer(true);
            ApplyLife(owner, PlayerLifeState.Alive, 1);
            owner.OnLifeStateChanged += _ => owner.Shutdown();
            owner.OnLifeStateChanged += _ => laterNotifications++;
            owner.OnDied += () => laterNotifications++;
            ApplyLife(owner, PlayerLifeState.Dead, 2);
            Require(laterNotifications == 0 && owner.LifeState == PlayerLifeState.Uninitialized,
                "客户端生命回调关闭组件后，后续生命监听者及旧死亡通知应停止。");
        }

        private static void TestWireRoundTrip()
        {
            Require(NetworkProtocol.Version >= 7, "玩家数值消息需要协议版本至少为 7。");
            var original = new PlayerStatsStateMessage
            {
                netId = 73,
                state = new PlayerStatsState(67, 150, 32, 120, 0x0102030405060708UL)
            };
            using NetworkWriterPooled writer = NetworkWriterPool.Get();
            NetworkMessages.Pack(original, writer);
            using NetworkReaderPooled reader = NetworkReaderPool.Get(writer.ToArraySegment());
            Require(NetworkMessages.UnpackId(reader, out ushort id) && id == NetworkMessageId<PlayerStatsStateMessage>.Id,
                "完整数值消息必须使用自己的 Mirror 消息 ID。");
            PlayerStatsStateMessage decoded = reader.Read<PlayerStatsStateMessage>();
            Require(decoded.netId == original.netId && decoded.state.Equals(original.state) && reader.Remaining == 0,
                "完整数值消息所有字段应完整往返。");
            writer.Reset();
            var life = new PlayerLifeStateMessage
            {
                netId = 85, revision = 0x0203040506070809UL, lifeState = PlayerLifeState.Dead
            };
            NetworkMessages.Pack(life, writer);
            using NetworkReaderPooled lifeReader = NetworkReaderPool.Get(writer.ToArraySegment());
            Require(NetworkMessages.UnpackId(lifeReader, out id) && id == NetworkMessageId<PlayerLifeStateMessage>.Id,
                "公共状态消息必须使用独立的 Mirror 消息 ID。");
            PlayerLifeStateMessage decodedLife = lifeReader.Read<PlayerLifeStateMessage>();
            Require(decodedLife.netId == life.netId && decodedLife.revision == life.revision &&
                    decodedLife.lifeState == life.lifeState && lifeReader.Remaining == 0,
                "公共状态消息所有字段应完整往返。");
        }

        private static void TestPendingMessages()
        {
            using var scope = new PreviewScope();
            ClientManager client = scope.CreateClientManager();
            DeliverStats(client, 31, 0, 5);
            DeliverStats(client, 31, 90, 3);
            DeliverLife(client, 31, PlayerLifeState.Dead, 5);
            DeliverLife(client, 31, PlayerLifeState.Alive, 4);
            PlayerStatsController stats = scope.CreateClientPlayer(true);
            int deaths = 0;
            stats.OnDied += () => deaths++;
            ClientManager.RegisterPlayer(31, stats.gameObject);
            Require(State(stats).Health == 0 && State(stats).Revision == 5 &&
                    stats.LifeState == PlayerLifeState.Dead && deaths == 0 &&
                    Pending(client, "_pendingStatsStates") == 0 && Pending(client, "_pendingLifeStates") == 0,
                "对象注册应应用各流最新基准并移除缓存，首次死亡状态不能补播死亡。");
            DeliverStats(client, 31, 80, 4);
            DeliverLife(client, 31, PlayerLifeState.Alive, 4);
            Require(State(stats).Health == 0 && stats.LifeState == PlayerLifeState.Dead,
                "注册后的旧版本仍应拒绝。");
        }

        private static void TestRemoteDelivery()
        {
            using var scope = new PreviewScope();
            ClientManager client = scope.CreateClientManager();
            PlayerStatsController stats = scope.CreateClientPlayer(false);
            ClientManager.RegisterPlayer(42, stats.gameObject);
            DeliverStats(client, 42, 75, 2);
            DeliverLife(client, 42, PlayerLifeState.Alive, 2);
            Require(!stats.TryGetState(out _) && stats.LifeState == PlayerLifeState.Alive,
                "客户端消息路由不能将别人的精确数值应用到远端组件。");
        }

        private static void TestClientCleanup()
        {
            using var scope = new PreviewScope();
            ClientManager client = scope.CreateClientManager();
            DeliverStats(client, 51, 25, 4);
            DeliverLife(client, 51, PlayerLifeState.Dead, 4);
            ClientManager.UnregisterPlayer(51);
            Require(Pending(client, "_pendingStatsStates") == 0 && Pending(client, "_pendingLifeStates") == 0,
                "注销必须清理对象生成前积压的两类状态。");
            DeliverStats(client, 52, 25, 4);
            DeliverLife(client, 52, PlayerLifeState.Dead, 4);
            client.ShutDown();
            client.Initialize(scope.Bridge);
            PlayerStatsController owner = scope.CreateClientPlayer(true);
            ClientManager.RegisterPlayer(52, owner.gameObject);
            Require(!owner.TryGetState(out _) && owner.LifeState == PlayerLifeState.Uninitialized,
                "客户端重启后不能应用前一连接留下的数值状态。");
        }

        private static void TestMovementReplay()
        {
            using var scope = new PreviewScope();
            PlayerStatsController stats = scope.CreateServer();
            stats.RestoreArmor(30);
            stats.ApplyDamage(20);
            PlayerStatsState before = State(stats);
            var sight = stats.gameObject.AddComponent<PlayerSightController>();
            var simulation = stats.gameObject.AddComponent<PlayerSimulation>();
            var movement = stats.GetComponent<PlayerMovementController>();
            var data = ScriptableObject.CreateInstance<PlayerMovementDataSO>();
            data.hideFlags = HideFlags.HideAndDontSave;
            try
            {
                Set(movement, "_playerMovementDataSO", data);
                Invoke(simulation, "Awake");
                stats.gameObject.SetActive(true);
                simulation.CaptureFromBody();
                PlayerSimState baseline = simulation.State;
                simulation.SetReplayMode(true);
                simulation.Step(new PlayerInputFrame { tick = 1, fire = true });
                simulation.RestoreState(baseline);
                simulation.SetReplayMode(false);
                PlayerBrain brain = stats.GetComponent<PlayerBrain>();
                var smoother = stats.gameObject.AddComponent<PlayerCorrectionSmoother>();
                var interpolator = stats.gameObject.AddComponent<RemotePoseInterpolator>();
                var prediction = stats.gameObject.AddComponent<PlayerPrediction>();
                Set(brain, "<Simulation>k__BackingField", simulation);
                Set(brain, "<CorrectionSmoother>k__BackingField", smoother);
                Set(brain, "<Interpolator>k__BackingField", interpolator);
                Set(brain, "<Prediction>k__BackingField", prediction);
                Invoke(prediction, "Awake");
                brain.SetRole(PawnRole.Predicted);
                prediction.InitializePrediction(1, 0, baseline);
                prediction.InitializePrediction(2, 0, baseline);
                Require(State(stats).Equals(before) && sight != null && prediction.Epoch == 2,
                    "含射击输入的真实运动重放及快照恢复不能重扣伤害或恢复旧血量。");
            }
            finally { UnityEngine.Object.DestroyImmediate(data); }
        }

        private static void TestPrefabConfiguration()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player/Player.prefab");
            Require(prefab != null && prefab.GetComponents<PlayerStatsController>().Length == 1,
                "玩家预制体必须包含唯一数值控制器。");
            var serialized = new SerializedObject(prefab.GetComponent<PlayerStatsController>());
            var data = serialized.FindProperty("_playerBasicData").objectReferenceValue as PlayerBasicDataSO;
            Require(data != null && AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(data)) ==
                    "17c38229c6da4f645b01ba7f3e8232bd" && data.InitialHealth == 100 && data.MaxHealth == 100 &&
                    data.InitialArmor == 0 && data.MaxArmor == 100 && Mathf.Approximately(data.ArmorAbsorptionRatio, 0.5f),
                "原玩家 SO GUID、血量 100 和新护甲默认配置必须完整保留。");
        }

        private static PlayerStatsState State(PlayerStatsController stats)
        {
            Require(stats.TryGetState(out PlayerStatsState state), "完整玩家数值应已经初始化。");
            return state;
        }

        private static bool ApplyState(PlayerStatsController stats, PlayerStatsState state) =>
            (bool)Invoke(stats, "ApplyState", state);

        private static bool ApplyLife(PlayerStatsController stats, PlayerLifeState lifeState, ulong revision) =>
            (bool)Invoke(stats, "ApplyLifeState", lifeState, revision);

        private static void DeliverStats(ClientManager client, uint netId, int health, ulong revision) =>
            Invoke(client, "HandlePlayerStatsState", new PlayerStatsStateMessage
            {
                netId = netId, state = new PlayerStatsState(health, 100, 0, 100, revision)
            });

        private static void DeliverLife(ClientManager client, uint netId, PlayerLifeState lifeState, ulong revision) =>
            Invoke(client, "HandlePlayerLifeState", new PlayerLifeStateMessage
            {
                netId = netId, revision = revision, lifeState = lifeState
            });

        private static int Pending(ClientManager client, string field) =>
            ((IDictionary)typeof(ClientManager).GetField(field, PrivateInstance).GetValue(client)).Count;

        private static object Invoke(object target, string method, params object[] arguments)
        {
            MethodInfo entry = target.GetType().GetMethod(method,
                PrivateInstance | BindingFlags.Public);
            Require(entry != null, "缺少验证入口 " + method);
            try { return entry.Invoke(target, arguments); }
            catch (TargetInvocationException exception) when (exception.InnerException != null)
            {
                throw exception.InnerException;
            }
        }

        private static void Set(object target, string field, object value)
        {
            FieldInfo entry = target.GetType().GetField(field, PrivateInstance);
            Require(entry != null, "缺少验证字段 " + field);
            entry.SetValue(target, value);
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }

        private static void RunCase(string name, Action test, StringBuilder report, ref int passed)
        {
            try { test(); }
            catch (Exception exception) { throw new InvalidOperationException("玩家数值验证失败：" + name, exception); }
            passed++;
            report.AppendLine("PASS " + name);
        }

        private sealed class PreviewScope : IDisposable
        {
            private readonly Scene _scene = EditorSceneManager.NewPreviewScene();
            private readonly ClientManager _previousClient = ClientManager.Instance;
            private ClientManager _client;
            public PlayerBasicDataSO Data { get; }
            public NetBridge Bridge { get; private set; }

            public PreviewScope()
            {
                Data = ScriptableObject.CreateInstance<PlayerBasicDataSO>();
                Data.hideFlags = HideFlags.HideAndDontSave;
                Set(Data, "_initialHealth", 100);
                Set(Data, "_maxHealth", 100);
                Set(Data, "_initialArmor", 0);
                Set(Data, "_maxArmor", 100);
                Set(Data, "_armorAbsorptionRatio", 0.5f);
            }

            public PlayerStatsController CreateServer()
            {
                PlayerStatsController stats = CreatePlayer(PawnRole.Authoritative);
                stats.InitializeServer();
                return stats;
            }

            public PlayerStatsController CreateClientPlayer(bool owner)
            {
                PlayerStatsController stats = CreatePlayer(owner ? PawnRole.Predicted : PawnRole.Interpolated);
                NetworkIdentity identity = stats.gameObject.AddComponent<NetworkIdentity>();
                typeof(NetworkIdentity).GetProperty(nameof(NetworkIdentity.isLocalPlayer)).SetValue(identity, owner);
                PlayerController controller = stats.gameObject.AddComponent<PlayerController>();
                typeof(NetworkBehaviour).GetProperty(nameof(NetworkBehaviour.netIdentity)).SetValue(controller, identity);
                stats.InitializeClient(owner);
                return stats;
            }

            public ClientManager CreateClientManager()
            {
                GameObject root = CreateRoot("PlayerStatsValidationClient");
                Bridge = root.AddComponent<NetBridge>();
                _client = root.AddComponent<ClientManager>();
                _client.Initialize(Bridge);
                return _client;
            }

            private PlayerStatsController CreatePlayer(PawnRole role)
            {
                GameObject root = CreateRoot("PlayerStatsValidationPlayer");
                root.SetActive(false);
                PlayerBrain brain = root.AddComponent<PlayerBrain>();
                Set(brain, "<Role>k__BackingField", role);
                PlayerStatsController stats = root.AddComponent<PlayerStatsController>();
                Set(stats, "_playerBasicData", Data);
                Set(brain, "<Stats>k__BackingField", stats);
                return stats;
            }

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
                    foreach (GameObject root in _scene.GetRootGameObjects())
                    {
                        if (root.TryGetComponent(out NetworkIdentity identity))
                            typeof(NetworkIdentity).GetProperty(nameof(NetworkIdentity.isLocalPlayer)).SetValue(identity, false);
                    }
                }
                finally
                {
                    typeof(ClientManager).GetField("<Instance>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic)
                        .SetValue(null, _previousClient);
                    if (_scene.IsValid()) EditorSceneManager.ClosePreviewScene(_scene);
                    if (Data != null) UnityEngine.Object.DestroyImmediate(Data);
                }
            }
        }
    }
}
