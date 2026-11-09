using System;
using System.Collections.Generic;
using System.Reflection;
using GamePlay.HoldSystem;
using GamePlay.Player;
using GamePlay.Player.Simulation;
using Mirror;
using Network.Client;
using Network.Common;
using Network.Common.Messages;
using Network.Server;
using Newtonsoft.Json.Linq;
using Unity.Pipeline.Commands;
using UnityEditor;
using UnityEngine;

namespace FPSGame.Editor.Validation
{
    /// <summary>三进程联机验收的编辑器探针；只观察消息和注入实时输入，不保存场景或资产。</summary>
    public static class NetworkEventIntegrationProbe
    {
        private const int MaxRecordedEvents = 2048;
        private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
        private static readonly FieldInfo PendingInput = Field("_pending", typeof(PlayerInputDriver));
        private static readonly FieldInfo AcceptingInput = Field("_accepting", typeof(PlayerInputDriver));
        private static readonly FieldInfo FireHeld = Field("_fireHeld", typeof(PlayerInputDriver));
        private static readonly FieldInfo FirePending = Field("_firePending", typeof(PlayerInputDriver));
        private static readonly FieldInfo EventSequence = Field("_eventSequence", typeof(ServerManager));
        private static readonly FieldInfo SimulationTick = Field("_tick", typeof(ServerManager));
        private static readonly Queue<JObject> Events = new();

        private static ClientManager _client;
        private static PlayerWeaponController _localWeapon;
        private static PlayerInputDriver _manualDriver;
        private static bool _driverWasEnabled;
        private static bool _started;
        private static ulong _receivedCount;
        private static ulong _localShotCount;

        static NetworkEventIntegrationProbe()
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        [CliCommand("network_event_probe_start", "Capture live network events and predicted shots for integration validation", Tags = new[] { "fpsgame/validation" })]
        public static JObject Start()
        {
            RequirePlay();
            Stop();
            _client = ClientManager.Instance;
            if (_client == null && !NetworkServer.active)
                throw new InvalidOperationException("请先启动服务器或客户端，再启动事件探针。");

            if (_client != null)
                _client.EventReceived += RecordEvent;
            _started = true;
            BindLocalWeapon();
            return new JObject { ["ok"] = true, ["operation"] = "StartEventProbe" };
        }

        [CliCommand("network_event_probe_input", "Inject fire and rifle selection through normal predicted input ticks", Tags = new[] { "fpsgame/validation" })]
        public static JObject Input(bool fire = false, bool select_rifle = false)
        {
            RequirePlay();
            if (!_started)
                throw new InvalidOperationException("请先运行 network_event_probe_start。");
            if (NetworkClient.localPlayer == null)
                throw new InvalidOperationException("本地联网玩家尚未生成。");

            PlayerInputDriver driver = PlayerInputDriver.Instance;
            if (driver == null)
                throw new InvalidOperationException("缺少本地 PlayerInputDriver。");
            if (_manualDriver != driver)
            {
                RestoreInput();
                _manualDriver = driver;
                _driverWasEnabled = driver.enabled;
                driver.enabled = false;
            }
            // Disable/重同步会清空首拍；先完成输入组件的首拍初始化，再写本次注入。
            // 不调用预测、模拟或枪械步进，实际输入仍由下一次正常 SimTick 消费。
            if (!(bool)AcceptingInput.GetValue(driver))
                driver.Consume(0);

            var pending = (PlayerInputFrame)PendingInput.GetValue(driver);
            pending.key1 |= select_rifle;
            PendingInput.SetValue(driver, pending);
            FireHeld.SetValue(driver, fire);
            FirePending.SetValue(driver, fire);
            BindLocalWeapon();
            return new JObject
            {
                ["ok"] = true,
                ["operation"] = "SetFireInput",
                ["fire"] = fire,
                ["selectRifle"] = select_rifle
            };
        }

        [CliCommand("network_event_probe_report", "Report received event facts, predicted shot counts and authoritative weapon state", Tags = new[] { "fpsgame/validation" })]
        public static JObject Report()
        {
            if (_started)
                BindLocalWeapon();
            var events = new JArray();
            foreach (JObject entry in Events)
                events.Add(entry.DeepClone());

            var players = new JArray();
            foreach (PlayerBrain brain in UnityEngine.Object.FindObjectsByType<PlayerBrain>(FindObjectsSortMode.None))
            {
                NetworkIdentity identity = brain.GetComponent<NetworkIdentity>();
                PlayerHolder holder = brain.GetComponent<PlayerHolder>();
                PlayerWeaponController weapon = brain.GetComponent<PlayerWeaponController>();
                Gun currentGun = holder != null ? holder.CurrentWeapon as Gun : null;
                Gun rifle = holder != null ? holder.Rifle?.Weapon as Gun : null;
                players.Add(new JObject
                {
                    ["netId"] = identity != null ? identity.netId : 0u,
                    ["role"] = brain.Role.ToString(),
                    ["position"] = Vector(brain.transform.position),
                    ["slot"] = holder != null ? holder.CurrentSlot?.ToString() : null,
                    ["localTick"] = weapon != null ? weapon.LocalTick : 0u,
                    ["serverTick"] = weapon != null ? weapon.ServerTick : 0u,
                    ["gunState"] = currentGun?.GunState.ToString(),
                    ["magazineRounds"] = currentGun != null ? new JValue(currentGun.Magazine.Rounds) : JValue.CreateNull(),
                    ["rifleRounds"] = rifle != null ? new JValue(rifle.Magazine.Rounds) : JValue.CreateNull(),
                    ["prepareTicks"] = rifle != null ? new JValue(rifle.Config.PrepareTicks) : JValue.CreateNull(),
                    ["shotIntervalTicks"] = rifle != null ? new JValue(rifle.Config.ShotIntervalTicks) : JValue.CreateNull(),
                    ["predictionEpoch"] = brain.Prediction != null ? brain.Prediction.Epoch : 0u,
                    ["predictionInputTick"] = brain.Prediction != null ? brain.Prediction.CurrentInputTick : 0u,
                    ["predictionInitialized"] = brain.Prediction != null && brain.Prediction.IsInitialized
                });
            }

            var manager = NetworkManager.singleton as FPSNetworkManager;
            ServerManager server = manager != null ? manager.ServerManagerInst : null;
            return new JObject
            {
                ["started"] = _started,
                ["playing"] = Application.isPlaying,
                ["time"] = Time.unscaledTimeAsDouble,
                ["serverActive"] = NetworkServer.active,
                ["clientConnected"] = NetworkClient.isConnected,
                ["localNetId"] = NetworkClient.localPlayer != null ? NetworkClient.localPlayer.netId : 0u,
                ["receivedCount"] = _receivedCount,
                ["localShotCount"] = _localShotCount,
                ["recordedCount"] = Events.Count,
                ["serverEventSequence"] = server != null ? new JValue((ulong)EventSequence.GetValue(server)) : JValue.CreateNull(),
                ["serverSimulationTick"] = server != null ? new JValue((uint)SimulationTick.GetValue(server)) : JValue.CreateNull(),
                ["manualInput"] = _manualDriver != null,
                ["events"] = events,
                ["players"] = players
            };
        }

        [CliCommand("network_event_probe_stop", "Unsubscribe event capture and restore device input after validation", Tags = new[] { "fpsgame/validation" })]
        public static JObject Stop()
        {
            if (_client != null)
                _client.EventReceived -= RecordEvent;
            if (_localWeapon != null)
                _localWeapon.OnShot -= RecordLocalShot;
            RestoreInput();
            _client = null;
            _localWeapon = null;
            _started = false;
            _receivedCount = 0;
            _localShotCount = 0;
            Events.Clear();
            return new JObject { ["ok"] = true, ["operation"] = "StopEventProbe" };
        }

        private static void RecordEvent(NetworkEventMessage message)
        {
            _receivedCount++;
            if (message is not FireEventMessage fire)
                return;
            if (Events.Count == MaxRecordedEvents)
                Events.Dequeue();
            Events.Enqueue(new JObject
            {
                ["eventId"] = fire.eventId,
                ["serverTick"] = fire.serverTick,
                ["netId"] = fire.netId,
                ["predictionEpoch"] = fire.predictionEpoch,
                ["inputTick"] = fire.inputTick,
                ["position"] = Vector(fire.position),
                ["origin"] = Vector(fire.origin),
                ["direction"] = Vector(fire.direction)
            });
        }

        private static void RecordLocalShot(Gun gun) => _localShotCount++;

        private static void BindLocalWeapon()
        {
            PlayerWeaponController next = NetworkClient.localPlayer != null
                ? NetworkClient.localPlayer.GetComponent<PlayerWeaponController>() : null;
            if (next == _localWeapon)
                return;
            if (_localWeapon != null)
                _localWeapon.OnShot -= RecordLocalShot;
            _localWeapon = next;
            if (_localWeapon != null)
                _localWeapon.OnShot += RecordLocalShot;
        }

        private static void RestoreInput()
        {
            if (_manualDriver != null)
            {
                _manualDriver.ResetPendingInput();
                _manualDriver.enabled = _driverWasEnabled;
            }
            _manualDriver = null;
        }

        private static JObject Vector(Vector3 value) => new()
        {
            ["x"] = value.x,
            ["y"] = value.y,
            ["z"] = value.z
        };

        private static FieldInfo Field(string name, Type type) =>
            type.GetField(name, PrivateInstance) ?? throw new MissingFieldException(type.FullName, name);

        private static void RequirePlay()
        {
            if (!Application.isPlaying)
                throw new InvalidOperationException("事件联机探针只能在 Play Mode 中使用。");
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingPlayMode)
                Stop();
        }
    }
}
