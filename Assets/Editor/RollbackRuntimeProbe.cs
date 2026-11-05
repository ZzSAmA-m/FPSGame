using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using GamePlay.Player;
using GamePlay.Player.Presentation;
using GamePlay.Player.Simulation;
using kcp2k;
using Mirror;
using Network.Common;
using Network.Server;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Unity.Pipeline.Commands;
using UnityEditor;
using UnityEngine;

namespace FPSGame.Editor
{
    /// <summary>只供编辑器联机验收使用，所有注入都发生在 Play Mode。</summary>
    public static class RollbackRuntimeProbe
    {
        const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
        static readonly FieldInfo PendingInput = typeof(PlayerInputDriver).GetField("_pending", PrivateInstance);
        static readonly FieldInfo AcceptingInput = typeof(PlayerInputDriver).GetField("_accepting", PrivateInstance);
        static readonly FieldInfo InputHandlers = typeof(NetBridge).GetField("PlayerInputReceived", PrivateInstance);
        static readonly FieldInfo ServerTick = typeof(ServerManager).GetField("_tick", PrivateInstance);
        static readonly PropertyInfo ServerPendingTicks = typeof(ServerManager).GetProperty("PendingSimulationTicks", PrivateInstance);
        static readonly MethodInfo BeginResync = typeof(PlayerPrediction).GetMethod("BeginResync", PrivateInstance);
        static readonly MethodInfo BeginEpoch = typeof(ServerManager).GetMethod("BeginPredictionEpoch", PrivateInstance);
        static readonly FieldInfo RemoteSamples = typeof(RemotePoseInterpolator).GetField("_samples", PrivateInstance);
        static readonly FieldInfo RemoteClock = typeof(RemotePoseInterpolator).GetField("_serverTickEstimate", PrivateInstance);

        static PlayerInputDriver _manualDriver;
        static bool _driverWasEnabled;
        static NetBridge _dropBridge;
        static Delegate _savedInputHandlers;
        static bool _frameCapChanged;
        static int _previousFrameRate;
        static int _previousVSync;

        /// <summary>直接调用已编译探针，避免动态求值造成额外帧停顿。</summary>
        [CliCommand("rollback_probe", "Run an editor-only rollback validation probe", Tags = new[] { "fpsgame/validation" })]
        public static JObject Probe(string action = "capture", float x = 0f, float y = 0f, float z = 0f,
            bool jump = false, bool enabled = false, int port = 17777, float lookX = 0f, float lookY = 0f,
            bool reposition = false, int milliseconds = 1000, int fps = 60)
        {
            string result = action switch
            {
                "capture" => Capture(),
                "server" => StartServer(port),
                "client" => StartClient(port: port),
                "input" => SetInput(x, y, jump, lookX, lookY),
                "clear" => ClearInput(),
                "offset" => OffsetPrediction(x, y, z),
                "resync" => ForceResync(),
                "drop" => SetServerInputDrop(enabled),
                "stage" => Stage(reposition),
                "stall" => StallServer(milliseconds),
                "framecap" => SetFrameCap(enabled, fps),
                _ => throw new ArgumentException("未知的回滚验收操作。", nameof(action))
            };
            return JObject.Parse(result);
        }

        [CliCommand("rollback_validate", "Run deterministic rollback checks", Tags = new[] { "fpsgame/validation" })]
        public static string Validate() => RollbackValidation.RunAll();

        public static string Capture()
        {
            FPSNetworkManager manager = FindManager();
            ServerManager server = manager != null ? manager.ServerManagerInst : null;
            var sessions = new List<object>();
            if (server != null)
            {
                foreach (var pair in server.Sessions.Clients)
                {
                    var session = pair.Value;
                    PlayerSimulation simulation = session.Player != null
                        ? session.Player.GetComponent<PlayerSimulation>() : null;
                    sessions.Add(new
                    {
                        connectionId = session.ConnectionId,
                        netId = session.Player != null ? session.Player.netId : 0u,
                        state = session.State.ToString(),
                        epoch = session.PredictionEpoch,
                        ack = session.Inputs.LastProcessedTick,
                        pending = session.Inputs.PendingCount,
                        synthetic = session.Inputs.SyntheticInputCount,
                        awaitingInput = session.AwaitingInput,
                        startServerTick = session.InputStartServerTick,
                        simulation = StateData(simulation),
                        physicalPosition = simulation != null ? VectorData(simulation.transform.position) : null
                    });
                }
            }

            var players = new List<object>();
            foreach (PlayerBrain brain in UnityEngine.Object.FindObjectsByType<PlayerBrain>(FindObjectsSortMode.None))
            {
                NetworkIdentity identity = brain.GetComponent<NetworkIdentity>();
                PlayerPrediction prediction = brain.Prediction;
                players.Add(new
                {
                    netId = identity != null ? identity.netId : 0u,
                    role = brain.Role.ToString(),
                    simulation = StateData(brain.Simulation),
                    physicalPosition = VectorData(brain.transform.position),
                    physicalYaw = brain.transform.eulerAngles.y,
                    interpolation = InterpolationData(brain.Interpolator),
                    smootherOffset = brain.CorrectionSmoother != null ? VectorData(brain.CorrectionSmoother.Offset) : null,
                    prediction = prediction == null ? null : new
                    {
                        enabled = prediction.enabled,
                        epoch = prediction.Epoch,
                        currentInputTick = prediction.CurrentInputTick,
                        lastAckInputTick = prediction.LastAckInputTick,
                        initialized = prediction.IsInitialized,
                        resyncing = prediction.IsResyncing,
                        snapshotChecks = prediction.SnapshotChecks,
                        matchedSnapshotCount = prediction.MatchedSnapshotCount,
                        rollbackCount = prediction.RollbackCount,
                        totalReplayedTicks = prediction.TotalReplayedTicks,
                        resyncCount = prediction.ResyncCount,
                        lastReplayedTicks = prediction.LastReplayedTicks,
                        lastPositionError = prediction.LastPositionError,
                        lastReconcileMilliseconds = prediction.LastReconcileMilliseconds,
                        peakReconcileMilliseconds = prediction.PeakReconcileMilliseconds
                    }
                });
            }

            return Json(new
            {
                playing = Application.isPlaying,
                frame = Time.frameCount,
                time = Time.unscaledTimeAsDouble,
                frameSeconds = Time.unscaledDeltaTime,
                targetFrameRate = Application.targetFrameRate,
                vSyncCount = QualitySettings.vSyncCount,
                runInBackground = Application.runInBackground,
                focused = Application.isFocused,
                frameCapOverride = _frameCapChanged,
                serverActive = NetworkServer.active,
                clientActive = NetworkClient.active,
                clientConnected = NetworkClient.isConnected,
                serverTick = server != null && ServerTick != null ? ServerTick.GetValue(server) : null,
                serverPendingSimulationTicks = server != null ? ServerPendingTicks?.GetValue(server) : null,
                applicationInputDrop = _dropBridge != null,
                manualInput = _manualDriver != null && !_manualDriver.enabled,
                sessions,
                players
            });
        }

        public static string StartServer(int port = 17777)
        {
            RequirePlay();
            FPSNetworkManager manager = RequireManager();
            if (NetworkClient.active)
                throw new InvalidOperationException("客户端仍在运行，请先停止后再启动专用服务器。");
            if (!NetworkServer.active)
            {
                SetPort(manager, port);
                manager.StartServer();
            }
            return Json(new { ok = NetworkServer.active, operation = "StartServer", port });
        }

        public static string StartClient(string ip = "127.0.0.1", int port = 17777)
        {
            RequirePlay();
            FPSNetworkManager manager = RequireManager();
            if (NetworkServer.active)
                throw new InvalidOperationException("服务器仍在运行，请先停止后再启动专用客户端。");
            if (!NetworkClient.active)
            {
                SetPort(manager, port);
                manager.networkAddress = ip;
                manager.StartClient();
            }
            return Json(new { ok = NetworkClient.active, operation = "StartClient", ip, port });
        }

        /// <summary>暂停设备采样后注入输入，Consume 仍按正常预测 Tick 执行。</summary>
        public static string SetInput(float x, float y, bool jump = false, float lookX = 0f, float lookY = 0f)
        {
            RequirePlay();
            PlayerInputDriver driver = PlayerInputDriver.Instance;
            if (driver == null || PendingInput == null || AcceptingInput == null)
                throw new InvalidOperationException("本地输入组件或反射字段不存在。");
            if (_manualDriver != driver)
            {
                _manualDriver = driver;
                _driverWasEnabled = driver.enabled;
            }
            driver.enabled = false;
            PendingInput.SetValue(driver, new PlayerInputFrame
            {
                move = new Vector2(x, y),
                look = new Vector2(lookX, lookY),
                jump = jump
            });
            AcceptingInput.SetValue(driver, true);
            return Json(new { ok = true, operation = "SetInput", x, y, jump, lookX, lookY });
        }

        public static string ClearInput(bool restoreDeviceInput = true)
        {
            RequirePlay();
            PlayerInputDriver driver = _manualDriver != null ? _manualDriver : PlayerInputDriver.Instance;
            if (driver != null)
            {
                driver.ResetPendingInput();
                if (restoreDeviceInput)
                    driver.enabled = _manualDriver != null ? _driverWasEnabled : true;
            }
            if (restoreDeviceInput)
                _manualDriver = null;
            return Json(new { ok = true, operation = "ClearInput", restoreDeviceInput });
        }

        /// <summary>制造本机预测误差，后续仍通过真实快照完成纠偏。</summary>
        public static string OffsetPrediction(float x, float y, float z)
        {
            RequirePlay();
            PlayerPrediction prediction = RequirePrediction();
            PlayerSimulation simulation = prediction.GetComponent<PlayerSimulation>();
            PlayerSimState state = simulation.State;
            state.position += new Vector3(x, y, z);
            simulation.RestoreState(state);
            return Json(new { ok = true, operation = "OffsetPrediction", position = VectorData(state.position) });
        }

        public static string ForceResync()
        {
            RequirePlay();
            PlayerPrediction prediction = RequirePrediction();
            if (BeginResync == null)
                throw new MissingMethodException("PlayerPrediction.BeginResync");
            uint previousEpoch = prediction.Epoch;
            BeginResync.Invoke(prediction, null);
            return Json(new { ok = true, operation = "ForceResync", previousEpoch, resyncing = prediction.IsResyncing });
        }

        /// <summary>临时丢弃应用层输入消息；这不模拟 KCP 网络层的延迟或丢包。</summary>
        public static string SetServerInputDrop(bool enabled)
        {
            RequirePlay();
            if (InputHandlers == null)
                throw new MissingFieldException("NetBridge.PlayerInputReceived");
            if (enabled)
            {
                FPSNetworkManager manager = RequireManager();
                if (!NetworkServer.active || manager.ServerManagerInst == null)
                    throw new InvalidOperationException("只能在运行中的服务器上丢弃输入。");
                NetBridge bridge = manager.Bridge;
                if (_dropBridge == null)
                {
                    _dropBridge = bridge;
                    _savedInputHandlers = InputHandlers.GetValue(bridge) as Delegate;
                    InputHandlers.SetValue(bridge, null);
                }
            }
            else
            {
                if (_dropBridge != null)
                    InputHandlers.SetValue(_dropBridge, _savedInputHandlers);
                _dropBridge = null;
                _savedInputHandlers = null;
            }
            return Json(new { ok = true, operation = "SetServerInputDrop", enabled, layer = "application-input-message" });
        }

        /// <summary>创建临时大地板；服务器可把玩家分开放置并重新发送预测基准。</summary>
        public static string Stage(bool reposition = false)
        {
            RequirePlay();
            ServerManager server = RequireManager().ServerManagerInst;
            if (reposition && (!NetworkServer.active || server == null || BeginEpoch == null))
                throw new InvalidOperationException("只有运行中的服务器能重新放置玩家。");

            const string floorName = "__RollbackValidationFloor";
            GameObject floor = GameObject.Find(floorName);
            if (floor == null)
            {
                floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
                floor.name = floorName;
                floor.isStatic = true;
                floor.transform.position = new Vector3(0f, -0.5f, 0f);
                floor.transform.localScale = new Vector3(200f, 1f, 200f);
            }

            int movedPlayers = 0;
            if (reposition)
            {
                foreach (var pair in server.Sessions.Clients)
                {
                    var session = pair.Value;
                    if (session.Player == null || !session.Player.TryGetComponent(out PlayerSimulation simulation))
                        continue;
                    PlayerSimState state = simulation.State;
                    state.position = new Vector3(movedPlayers * 8f, 2f, 0f);
                    state.velocity = Vector3.zero;
                    state.verticalVelocity = 0f;
                    state.yaw = 0f;
                    state.pitch = 0f;
                    state.grounded = false;
                    simulation.RestoreState(state);
                    BeginEpoch.Invoke(server, new object[] { session });
                    movedPlayers++;
                }
            }
            Physics.SyncTransforms();
            return Json(new { ok = true, operation = "Stage", floor = floorName, movedPlayers });
        }

        /// <summary>短暂阻塞服务器主线程，复现真实掉帧后的输入积压。</summary>
        public static string StallServer(int milliseconds = 1000)
        {
            RequirePlay();
            if (!NetworkServer.active)
                throw new InvalidOperationException("只能阻塞运行中的服务器。");
            if (milliseconds < 1 || milliseconds > 2000)
                throw new ArgumentOutOfRangeException(nameof(milliseconds), "停顿时长必须在 1 到 2000 毫秒之间。");
            var timer = System.Diagnostics.Stopwatch.StartNew();
            System.Threading.Thread.Sleep(milliseconds);
            return Json(new { ok = true, operation = "StallServer", milliseconds, elapsedMilliseconds = timer.Elapsed.TotalMilliseconds });
        }

        /// <summary>临时限制帧率，关闭限制或退出 Play Mode 时还原原值。</summary>
        public static string SetFrameCap(bool enabled, int fps = 60)
        {
            RequirePlay();
            if (enabled)
            {
                if (fps < 1 || fps > 240)
                    throw new ArgumentOutOfRangeException(nameof(fps), "测试帧率必须在 1 到 240 之间。");
                if (!_frameCapChanged)
                {
                    _previousFrameRate = Application.targetFrameRate;
                    _previousVSync = QualitySettings.vSyncCount;
                    _frameCapChanged = true;
                    EditorApplication.playModeStateChanged += RestoreFrameCapOnExit;
                    AssemblyReloadEvents.beforeAssemblyReload += RestoreFrameCap;
                }
                QualitySettings.vSyncCount = 0;
                Application.targetFrameRate = fps;
            }
            else
                RestoreFrameCap();
            return Json(new { ok = true, operation = "SetFrameCap", enabled, targetFrameRate = Application.targetFrameRate });
        }

        static void RestoreFrameCapOnExit(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingPlayMode)
                RestoreFrameCap();
        }

        static void RestoreFrameCap()
        {
            if (!_frameCapChanged)
                return;
            Application.targetFrameRate = _previousFrameRate;
            QualitySettings.vSyncCount = _previousVSync;
            _frameCapChanged = false;
            EditorApplication.playModeStateChanged -= RestoreFrameCapOnExit;
            AssemblyReloadEvents.beforeAssemblyReload -= RestoreFrameCap;
        }

        static object InterpolationData(RemotePoseInterpolator interpolator)
        {
            if (interpolator == null)
                return null;
            IList samples = RemoteSamples?.GetValue(interpolator) as IList;
            object latest = samples != null && samples.Count > 0 ? samples[samples.Count - 1] : null;
            const BindingFlags fields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            object ReadSample(string name) => latest?.GetType().GetField(name, fields)?.GetValue(latest);
            return new
            {
                enabled = interpolator.enabled,
                sampleCount = samples?.Count ?? 0,
                latestTick = ReadSample("tick"),
                latestPosition = ReadSample("position") is Vector3 position ? VectorData(position) : null,
                latestYaw = ReadSample("yaw"),
                serverTickEstimate = RemoteClock?.GetValue(interpolator)
            };
        }

        static object StateData(PlayerSimulation simulation)
        {
            if (simulation == null)
                return null;
            PlayerSimState state = simulation.State;
            return new
            {
                position = VectorData(state.position),
                velocity = VectorData(state.velocity),
                state.verticalVelocity,
                state.yaw,
                state.pitch,
                state.grounded,
                replaying = simulation.IsReplaying
            };
        }

        static float[] VectorData(Vector3 value) => new[] { value.x, value.y, value.z };

        static FPSNetworkManager FindManager() => NetworkManager.singleton as FPSNetworkManager
            ?? UnityEngine.Object.FindFirstObjectByType<FPSNetworkManager>();

        static FPSNetworkManager RequireManager() => FindManager()
            ?? throw new InvalidOperationException("场景内没有 FPSNetworkManager。");

        static PlayerPrediction RequirePrediction()
        {
            foreach (PlayerPrediction prediction in UnityEngine.Object.FindObjectsByType<PlayerPrediction>(FindObjectsSortMode.None))
            {
                PlayerBrain brain = prediction.GetComponent<PlayerBrain>();
                if (brain != null && brain.Role == PawnRole.Predicted && prediction.IsInitialized)
                    return prediction;
            }
            throw new InvalidOperationException("尚未找到已完成初始化的本地预测玩家。");
        }

        static void SetPort(FPSNetworkManager manager, int port)
        {
            if (port < 1 || port > ushort.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(port));
            KcpTransport transport = manager.GetComponent<KcpTransport>();
            if (transport == null)
                throw new InvalidOperationException("FPSNetworkManager 没有 KcpTransport。");
            transport.port = (ushort)port;
        }

        static void RequirePlay()
        {
            if (!Application.isPlaying)
                throw new InvalidOperationException("请先进入 Play Mode。");
        }

        static string Json(object value) => JsonConvert.SerializeObject(value, Formatting.None);
    }
}
