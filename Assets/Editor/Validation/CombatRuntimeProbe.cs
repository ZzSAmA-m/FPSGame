using System;
using System.Collections.Generic;
using System.Reflection;
using Core.Input;
using GamePlay.Player;
using GamePlay.Player.Combat;
using GamePlay.Player.Simulation;
using Mirror;
using Network.Client;
using Network.Common;
using Network.Common.Messages;
using Network.Server;
using Network.Server.Combat;
using Newtonsoft.Json.Linq;
using Unity.Pipeline.Commands;
using UnityEditor;
using UnityEngine;

namespace FPSGame.Editor.Validation
{
    /// <summary>仅供 Editor 多进程实测，注入普通输入并记录权威命中与当时显示的几何。</summary>
    public static class CombatRuntimeProbe
    {
        const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;
        static readonly MethodInfo Accumulate = typeof(PlayerInputDriver).GetMethod("Accumulate", Private);
        static readonly FieldInfo FireHeld = typeof(PlayerInputDriver).GetField("_fireHeld", Private);
        static readonly MethodInfo BeginEpoch = typeof(ServerManager).GetMethod("BeginPredictionEpoch", Private);
        static readonly List<JObject> Shots = new();
        static readonly List<JObject> Hits = new();
        static readonly List<JObject> Confirms = new();
        static readonly List<uint> NumericIds = new();
        static ServerManager _server;
        static ClientManager _client;
        static NetBridge _bridge;
        static JObject _lastView;
        static CombatRuntimeProbe()
        {
            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.ExitingPlayMode) Stop();
            };
        }

        [CliCommand("combat_probe", "Editor-only live KCP combat, time and hitbox validation", Tags = new[] {"fpsgame/validation"})]
        public static JObject Run(string action = "capture", float latency = 0, float jitter = 0,
            float loss = 0, uint target = 4, string region = "Body", float x = 0, float y = 0,
            uint shooter = 3)
        {
            if (!Application.isPlaying) throw new InvalidOperationException("请先进入 Play Mode。");
            switch (action)
            {
                case "configure":
                    Configure(); return new JObject {["ok"] = true};
                case "track":
                    Track(); return Capture();
                case "stage":
                    Stage(shooter, target); return Capture();
                case "latency":
                    var sim = UnityEngine.Object.FindAnyObjectByType<LatencySimulation>();
                    if (sim == null) throw new InvalidOperationException("请在启动网络前 configure。");
                    sim.latency = latency; sim.jitter = jitter; sim.unreliableLoss = loss; sim.unreliableScramble = loss;
                    return new JObject {["ok"] = true};
                case "fire_view":
                    return FireView(target, region);
                case "controls":
                    return TryControls();
                case "move":
                    FPSGame.Editor.RollbackRuntimeProbe.SetInput(x, y); return new JObject {["ok"] = true};
                case "reset":
                    NetworkServer.spawned[target].GetComponent<PlayerStatsController>().ResetForSpawn();
                    return new JObject {["ok"] = true};
                case "clear":
                    Shots.Clear(); Hits.Clear(); Confirms.Clear(); NumericIds.Clear(); return Capture();
                case "stop":
                    Stop(); return new JObject {["ok"] = true};
                case "capture":
                    return Capture();
                default: throw new ArgumentException("未知战斗探针操作。");
            }
        }

        static void Configure()
        {
            if (NetworkClient.active || NetworkServer.active)
                throw new InvalidOperationException("configure 需要网络已停止。");
            Application.runInBackground = true; Application.targetFrameRate = 60; QualitySettings.vSyncCount = 0;
            var manager = UnityEngine.Object.FindAnyObjectByType<FPSNetworkManager>();
            var sim = manager.GetComponentInChildren<LatencySimulation>(true);
            if (sim == null)
            {
                var root = new GameObject("CombatValidationLatency");
                root.SetActive(false); root.transform.SetParent(manager.transform, false);
                sim = root.AddComponent<LatencySimulation>();
                sim.wrap = manager.transport;
                sim.latency = 0; sim.jitter = 0; sim.unreliableLoss = 0; sim.unreliableScramble = 0;
                root.SetActive(true);
            }
            manager.transport = sim; Transport.active = sim;
            EnsureFloor();
        }

        static void EnsureFloor()
        {
            if (GameObject.Find("CombatValidationFloor") != null) return;
            var floor = new GameObject("CombatValidationFloor");
            floor.transform.position = new Vector3(0,49.5f,0);
            floor.AddComponent<BoxCollider>().size = new Vector3(100,1,100);
            Physics.SyncTransforms();
        }

        static void Track()
        {
            Stop();
            _server = UnityEngine.Object.FindAnyObjectByType<ServerManager>();
            _client = ClientManager.Instance;
            _bridge = UnityEngine.Object.FindAnyObjectByType<NetBridge>();
            if (_server != null) _server.ShotResolved += OnShot;
            if (_client != null)
            {
                _client.HitReceived += OnHit;
                _client.HitConfirmed += OnConfirm;
            }
            if (_bridge != null) _bridge.PlayerStatsStateReceived += OnNumeric;
            NetworkEventIntegrationProbe.Start();
            if (NetworkClient.localPlayer != null) NetworkEventIntegrationProbe.Input();
        }

        static void Stop()
        {
            if (_server != null) _server.ShotResolved -= OnShot;
            if (_client != null)
            {
                _client.HitReceived -= OnHit;
                _client.HitConfirmed -= OnConfirm;
            }
            if (_bridge != null) _bridge.PlayerStatsStateReceived -= OnNumeric;
            _server = null; _client = null; _bridge = null;
            Shots.Clear(); Hits.Clear(); Confirms.Clear(); NumericIds.Clear(); _lastView = null;
        }

        static void Stage(uint shooter, uint target)
        {
            EnsureFloor();
            if (!NetworkServer.active) return;
            var manager = UnityEngine.Object.FindAnyObjectByType<ServerManager>();
            int observer = 0;
            foreach (var pair in manager.Sessions.Clients)
            {
                var session = pair.Value;
                var brain = session.Player.GetComponent<PlayerBrain>();
                var state = brain.State;
                uint id = session.Player.netId;
                state.position = id == shooter ? new Vector3(0, 50.095f, 0) :
                    id == target ? new Vector3(0, 50.095f, 10) : new Vector3(10 + 3 * observer++, 50.095f, 0);
                state.yaw = 0; state.pitch = 0; state.velocity = Vector3.zero; state.verticalVelocity = 0;
                brain.Simulation.RestoreState(state);
                brain.Stats.ResetForSpawn(); brain.Stats.RestoreArmor(100);
                BeginEpoch.Invoke(manager, new object[] {session});
            }
            Shots.Clear();
        }

        static JObject TryControls()
        {
            var driver = PlayerInputDriver.Instance;
            if (NetworkClient.localPlayer == null || driver == null)
                throw new InvalidOperationException("本地玩家与输入驱动尚未就绪。");

            Accumulate.Invoke(driver, new object[] { new PlayerInput
            {
                move = Vector2.up, look = new Vector2(120f, 50f), space = true,
                lMouse = true, r = true, key3 = true
            } });
            FireHeld.SetValue(driver, false);
            return new JObject { ["ok"] = true };
        }

        static JObject FireView(uint target, string region)
        {
            var local = NetworkClient.localPlayer;
            if (local == null || ClientManager.Instance == null)
                return new JObject {["ok"] = false, ["reason"] = "NoLocalPlayer"};
            var shooter = local.GetComponent<PlayerBrain>();
            if (!shooter.Prediction.IsInitialized || shooter.Prediction.IsResyncing)
                return new JObject {["ok"] = false, ["reason"] = "PredictionNotReady"};
            var holder = local.GetComponent<PlayerHolder>();
            if (holder.CurrentWeapon is not GamePlay.HoldSystem.Gun gun || gun.GunState != GamePlay.HoldSystem.GunState.Normal)
                return new JObject {["ok"] = false, ["reason"] = "GunNotReady"};
            if (!ClientManager.Instance.Players.Players.TryGetValue(target, out var remote))
                return new JObject {["ok"] = false, ["reason"] = "NoTarget"};
            var geometry = remote.GetComponent<PlayerHitboxController>().Capture();
            Vector3 point = region == "Head" ? geometry.Head.Center : (geometry.Body.PointA + geometry.Body.PointB) * 0.5f;
            Ray ray = shooter.GetViewRay();
            Vector3 direction = (point - ray.origin).normalized;
            float desiredYaw = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
            float desiredPitch = -Mathf.Asin(direction.y) * Mathf.Rad2Deg;
            float currentYaw = Mathf.Atan2(ray.direction.x, ray.direction.z) * Mathf.Rad2Deg;
            float currentPitch = -Mathf.Asin(ray.direction.y) * Mathf.Rad2Deg;
            var sample = new PlayerInput
            {
                lMouse = true,
                look = new Vector2(Mathf.DeltaAngle(currentYaw, desiredYaw) / shooter.Sight.Sensitivity,
                    -(desiredPitch - currentPitch) / shooter.Sight.Sensitivity)
            };
            var driver = PlayerInputDriver.Instance;
            Accumulate.Invoke(driver, new object[] {sample});
            FireHeld.SetValue(driver, false);
            var timeline = ClientManager.Instance;
            _lastView = new JObject
            {
                ["ok"] = true, ["tick"] = timeline.LastPresentedServerTick,
                ["from"] = timeline.LastPresentedFromTick, ["to"] = timeline.LastPresentedToTick,
                ["head"] = V(geometry.Head.Center), ["bodyA"] = V(geometry.Body.PointA), ["bodyB"] = V(geometry.Body.PointB),
                ["rtt"] = NetworkTime.rtt
            };
            return (JObject)_lastView.DeepClone();
        }

        static void OnShot(ServerShotRecord shot)
        {
            var entry = new JObject
            {
                ["id"] = shot.ShotId, ["shooter"] = shot.ShooterId, ["input"] = shot.InputTick,
                ["tick"] = shot.ServerTick, ["requested"] = shot.RequestedTick, ["receipt"] = shot.ReceiptTick,
                ["rtt"] = shot.RoundTripSeconds, ["query"] = shot.Time.QueryTick,
                ["timeFailure"] = shot.Time.Failure.ToString(), ["clamped"] = shot.Time.Clamped,
                ["status"] = shot.Result.Status.ToString(), ["historyFailure"] = shot.Result.HistoryFailure.ToString(),
                ["target"] = shot.Result.TargetId, ["region"] = shot.Result.Region.ToString(),
                ["distance"] = shot.Result.Distance, ["applied"] = shot.Applied, ["killed"] = shot.Killed,
                ["healthLost"] = shot.Damage.HealthLost, ["armorLost"] = shot.Damage.ArmorLost,
                ["head"] = V(shot.TargetGeometry.Head.Center),
                ["bodyA"] = V(shot.TargetGeometry.Body.PointA), ["bodyB"] = V(shot.TargetGeometry.Body.PointB)
            };
            Shots.Add(entry);
            if (Shots.Count > 128) Shots.RemoveAt(0);
        }

        static JObject DescribeHit(HitConfirmMessage message) => new()
        {
            ["id"] = message.shotId, ["tick"] = message.serverTick,
            ["shooter"] = message.shooterNetId, ["target"] = message.targetNetId,
            ["region"] = message.region.ToString(), ["point"] = V(message.point), ["killed"] = message.killed
        };

        static void OnHit(HitConfirmMessage message)
        {
            Hits.Add(DescribeHit(message));
            if (Hits.Count > 128) Hits.RemoveAt(0);
        }

        static void OnConfirm(HitConfirmMessage message)
        {
            Confirms.Add(DescribeHit(message));
            if (Confirms.Count > 128) Confirms.RemoveAt(0);
        }
        static void OnNumeric(Network.Common.Messages.PlayerStatsStateMessage m)
        {
            NumericIds.Add(m.netId);
            if (NumericIds.Count > 128) NumericIds.RemoveAt(0);
        }

        static JObject Capture()
        {
            var rows = new JArray();
            foreach (var brain in UnityEngine.Object.FindObjectsByType<PlayerBrain>(FindObjectsSortMode.None))
            {
                var id = brain.GetComponent<NetworkIdentity>().netId;
                bool has = brain.Stats.TryGetState(out var state);
                var holder = brain.GetComponent<PlayerHolder>();
                var gun = holder.CurrentWeapon as GamePlay.HoldSystem.Gun;
                rows.Add(new JObject
                {
                    ["id"] = id, ["role"] = brain.Role.ToString(), ["position"] = V(brain.transform.position),
                    ["life"] = brain.Stats.LifeState.ToString(), ["hasValues"] = has,
                    ["health"] = has ? state.Health : -1, ["armor"] = has ? state.Armor : -1,
                    ["revision"] = state.Revision, ["rounds"] = gun?.Magazine.Rounds,
                    ["slot"] = holder.CurrentSlot?.ToString(), ["yaw"] = brain.State.yaw, ["pitch"] = brain.State.pitch,
                    ["inputTick"] = brain.Prediction.CurrentInputTick, ["ackTick"] = brain.Prediction.LastAckInputTick,
                    ["rollbacks"] = brain.Prediction.RollbackCount, ["epoch"] = brain.Prediction.Epoch,
                    ["initialized"] = brain.Prediction.IsInitialized
                });
            }
            return new JObject
            {
                ["players"] = rows, ["localId"] = NetworkClient.localPlayer != null ? NetworkClient.localPlayer.netId : 0,
                ["connected"] = NetworkClient.isConnected, ["rtt"] = NetworkTime.rtt,
                ["shots"] = new JArray(Shots), ["hits"] = new JArray(Hits),
                ["confirms"] = new JArray(Confirms), ["numericIds"] = new JArray(NumericIds),
                ["view"] = _lastView
            };
        }
        static JObject V(Vector3 v) => new() {["x"] = v.x, ["y"] = v.y, ["z"] = v.z};
    }
}

