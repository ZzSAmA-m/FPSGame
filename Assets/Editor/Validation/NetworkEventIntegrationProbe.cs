using System;
using System.Collections.Generic;
using System.Reflection;
using GamePlay.HoldSystem;
using GamePlay.Player;
using GamePlay.Player.Presentation;
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
        private static readonly FieldInfo EventPublisher = Field("_eventPublisher", typeof(ServerManager));
        private static readonly FieldInfo SimulationTick = Field("_tick", typeof(ServerManager));
        private static readonly Queue<JObject> Events = new();
        private static readonly Dictionary<PlayerWeaponController, WeaponObservation> ObservedWeapons = new();
        private static readonly Dictionary<uint, ulong> PresentedShots = new();
        private static readonly Dictionary<uint, ulong> PresentedReloads = new();
        private static readonly Dictionary<uint, ulong> PresentedPrepares = new();
        private static readonly Dictionary<uint, ulong> PresentedCancellations = new();
        private static readonly Dictionary<uint, ulong> KnifePrepares = new();
        private static readonly Dictionary<uint, ulong> LightAttacks = new();
        private static readonly Dictionary<uint, ulong> HeavyAttacks = new();
        private static readonly Dictionary<PlayerHolder, Action> ObservedHolders = new();
        private static readonly Dictionary<uint, ulong> PresentedSwitches = new();

        private static ClientManager _client;
        private static PlayerWeaponController _localWeapon;
        private static PlayerInputDriver _manualDriver;
        private static bool _driverWasEnabled;
        private static bool _started;
        private static ulong _receivedCount;
        private static ulong _receivedFireCount;
        private static ulong _receivedSwitchCount;
        private static ulong _receivedReloadCount;
        private static ulong _receivedPrepareCount;
        private static ulong _receivedCanceledCount;
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
            EditorApplication.update += BindObservedWeapons;
            BindObservedWeapons();
            BindLocalWeapon();
            return new JObject { ["ok"] = true, ["operation"] = "StartEventProbe" };
        }

        [CliCommand("network_event_probe_input", "Inject fire, reload and weapon selection through normal predicted input ticks", Tags = new[] { "fpsgame/validation" })]
        public static JObject Input(bool fire = false, bool select_rifle = false,
            bool select_pistol = false, bool select_knife = false, bool reload = false,
            bool light_attack = false, bool heavy_attack = false, float move_y = 0f)
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
            pending.key2 |= select_pistol;
            pending.key3 |= select_knife;
            pending.reload |= reload;
            pending.lightAttackPressed |= light_attack;
            pending.heavyAttackPressed |= heavy_attack;
            pending.move.y = move_y;
            PendingInput.SetValue(driver, pending);
            FireHeld.SetValue(driver, fire);
            FirePending.SetValue(driver, fire);
            BindLocalWeapon();
            return new JObject
            {
                ["ok"] = true,
                ["operation"] = "SetFireInput",
                ["fire"] = fire,
                ["reload"] = reload,
                ["lightAttack"] = light_attack,
                ["heavyAttack"] = heavy_attack,
                ["selectRifle"] = select_rifle,
                ["selectPistol"] = select_pistol,
                ["selectKnife"] = select_knife
            };
        }

        [CliCommand("network_event_probe_weapon_action", "Invoke an authoritative C# weapon transition for a connected player", Tags = new[] { "fpsgame/validation" })]
        public static JObject WeaponAction(uint net_id, string action = "normal")
        {
            RequirePlay();
            if (!NetworkServer.active)
                throw new InvalidOperationException("权威动作注入需要专用服务器正在运行。");
            if (!NetworkServer.spawned.TryGetValue(net_id, out NetworkIdentity identity) || identity == null ||
                !identity.TryGetComponent(out PlayerBrain brain) || brain.Role != PawnRole.Authoritative ||
                !identity.TryGetComponent(out PlayerHolder holder))
                throw new InvalidOperationException("未找到该玩家的权威武器。");

            Gun gun = holder.CurrentWeapon as Gun;
            bool accepted = action switch
            {
                "prepare" => gun != null && gun.TryPrepare(),
                "normal" => gun != null && gun.TryReturnToNormal(),
                "reload" => gun != null && gun.TryReload(),
                "switch_knife" => holder.SelectSlot(WeaponSlot.Knife),
                "switch_rifle" => holder.SelectSlot(WeaponSlot.Rifle),
                _ => throw new ArgumentException("动作应为 prepare、normal、reload、switch_knife 或 switch_rifle。", nameof(action))
            };
            return new JObject
            {
                ["ok"] = true,
                ["accepted"] = accepted,
                ["netId"] = net_id,
                ["action"] = action,
                ["gunState"] = gun?.GunState.ToString(),
                ["rounds"] = gun != null ? new JValue(gun.Magazine.Rounds) : JValue.CreateNull(),
                ["reserve"] = gun != null ? new JValue(gun.Magazine.ReserveRounds) : JValue.CreateNull()
            };
        }

        [CliCommand("network_event_probe_report", "Report received event facts, predicted shot counts and authoritative weapon state", Tags = new[] { "fpsgame/validation" })]
        public static JObject Report()
        {
            if (_started)
            {
                BindLocalWeapon();
                BindObservedWeapons();
            }
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
                PlayerAimPoseController aim = brain.GetComponent<PlayerAimPoseController>();
                Animator actor = null;
                foreach (Animator candidate in brain.GetComponentsInChildren<Animator>(true))
                    if (candidate.layerCount > PlayerAnimatorController.FPLayer)
                    {
                        actor = candidate;
                        break;
                    }
                Animator model = holder?.CurrentController != null
                    ? holder.CurrentController.GetComponentInChildren<Animator>(true) : null;
                PlayerStatsState stats = default;
                bool hasStats = brain.Stats != null && brain.Stats.TryGetState(out stats);
                uint netId = identity != null ? identity.netId : 0u;
                players.Add(new JObject
                {
                    ["netId"] = identity != null ? identity.netId : 0u,
                    ["role"] = brain.Role.ToString(),
                    ["position"] = Vector(brain.transform.position),
                    ["slot"] = holder != null ? holder.CurrentSlot?.ToString() : null,
                    ["localTick"] = weapon != null ? weapon.LocalTick : 0u,
                    ["serverTick"] = weapon != null ? weapon.ServerTick : 0u,
                    ["presentedShotCount"] = identity != null && PresentedShots.TryGetValue(identity.netId, out ulong shots)
                        ? shots : 0UL,
                    ["presentedSwitchCount"] = identity != null && PresentedSwitches.TryGetValue(identity.netId, out ulong switches)
                        ? switches : 0UL,
                    ["remotePreparing"] = weapon != null && weapon.IsRemotePreparing,
                    ["remoteReloading"] = weapon != null && weapon.IsRemoteReloading,
                    ["presentedReloadCount"] = Count(PresentedReloads, netId),
                    ["presentedPrepareCount"] = Count(PresentedPrepares, netId),
                    ["presentedCanceledCount"] = Count(PresentedCancellations, netId),
                    ["knifePrepares"] = Count(KnifePrepares, netId),
                    ["lightAttacks"] = Count(LightAttacks, netId),
                    ["heavyAttacks"] = Count(HeavyAttacks, netId),
                    ["knifeState"] = weapon?.KnifePresentationState.ToString(),
                    ["knifeProgress"] = weapon?.KnifeActionProgress,
                    ["health"] = hasStats ? new JValue(stats.Health) : JValue.CreateNull(),
                    ["armor"] = hasStats ? new JValue(stats.Armor) : JValue.CreateNull(),
                    ["lowerAnimation"] = AnimationState(actor, PlayerAnimatorController.DownLayer),
                    ["aimWeight"] = aim != null ? new JValue(aim.CurrentAimWeight) : JValue.CreateNull(),
                    ["fpAnimation"] = AnimationState(actor, PlayerAnimatorController.FPLayer),
                    ["tpAnimation"] = AnimationState(actor, PlayerAnimatorController.UpLayer),
                    ["modelAnimation"] = AnimationState(model, 0),
                    ["gunState"] = currentGun?.GunState.ToString(),
                    ["presentationState"] = weapon != null ? weapon.PresentationState.ToString() : null,
                    ["actionProgress"] = weapon != null ? new JValue(weapon.ActionProgress) : JValue.CreateNull(),
                    ["magazineRounds"] = currentGun != null ? new JValue(currentGun.Magazine.Rounds) : JValue.CreateNull(),
                    ["reserveRounds"] = currentGun != null ? new JValue(currentGun.Magazine.ReserveRounds) : JValue.CreateNull(),
                    ["rifleRounds"] = rifle != null ? new JValue(rifle.Magazine.Rounds) : JValue.CreateNull(),
                    ["rifleReserveRounds"] = rifle != null ? new JValue(rifle.Magazine.ReserveRounds) : JValue.CreateNull(),
                    ["reloadTicks"] = rifle != null ? new JValue(rifle.Config.ReloadTicks) : JValue.CreateNull(),
                    ["prepareTicks"] = rifle != null ? new JValue(rifle.Config.PrepareTicks) : JValue.CreateNull(),
                    ["shotIntervalTicks"] = rifle != null ? new JValue(rifle.Config.ShotIntervalTicks) : JValue.CreateNull(),
                    ["predictionEpoch"] = brain.Prediction != null ? brain.Prediction.Epoch : 0u,
                    ["predictionInputTick"] = brain.Prediction != null ? brain.Prediction.CurrentInputTick : 0u,
                    ["predictionInitialized"] = brain.Prediction != null && brain.Prediction.IsInitialized
                });
            }

            var manager = NetworkManager.singleton as FPSNetworkManager;
            ServerManager server = manager != null ? manager.ServerManagerInst : null;
            var publisher = server != null ? (ServerEventPublisher)EventPublisher.GetValue(server) : null;
            return new JObject
            {
                ["started"] = _started,
                ["playing"] = Application.isPlaying,
                ["time"] = Time.unscaledTimeAsDouble,
                ["serverActive"] = NetworkServer.active,
                ["clientConnected"] = NetworkClient.isConnected,
                ["localNetId"] = NetworkClient.localPlayer != null ? NetworkClient.localPlayer.netId : 0u,
                ["receivedCount"] = _receivedCount,
                ["receivedFireCount"] = _receivedFireCount,
                ["receivedSwitchCount"] = _receivedSwitchCount,
                ["receivedReloadCount"] = _receivedReloadCount,
                ["receivedPrepareCount"] = _receivedPrepareCount,
                ["receivedCanceledCount"] = _receivedCanceledCount,
                ["localShotCount"] = _localShotCount,
                ["recordedCount"] = Events.Count,
                ["serverEventSequence"] = server != null ? new JValue(publisher?.LastEventId ?? 0UL) : JValue.CreateNull(),
                ["serverSimulationTick"] = server != null ? new JValue((uint)SimulationTick.GetValue(server)) : JValue.CreateNull(),
                ["manualInput"] = _manualDriver != null,
                ["events"] = events,
                ["players"] = players
            };
        }

        [CliCommand("network_event_probe_stop", "Unsubscribe event capture and restore device input after validation", Tags = new[] { "fpsgame/validation" })]
        public static JObject Stop()
        {
            EditorApplication.update -= BindObservedWeapons;
            foreach (var pair in ObservedWeapons)
                if (pair.Key != null)
                {
                    pair.Key.OnShot -= pair.Value.Shot;
                    pair.Key.OnReload -= pair.Value.Reload;
                    pair.Key.OnPrepare -= pair.Value.Prepare;
                    pair.Key.OnActionCanceled -= pair.Value.Canceled;
                    pair.Key.OnKnifeStateChanged -= pair.Value.KnifeState;
                }
            ObservedWeapons.Clear();
            PresentedShots.Clear();
            PresentedReloads.Clear();
            PresentedPrepares.Clear();
            PresentedCancellations.Clear();
            KnifePrepares.Clear();
            LightAttacks.Clear();
            HeavyAttacks.Clear();
            foreach (var pair in ObservedHolders)
                if (pair.Key != null)
                    pair.Key.OnWeaponChanged -= pair.Value;
            ObservedHolders.Clear();
            PresentedSwitches.Clear();
            if (_client != null)
                _client.EventReceived -= RecordEvent;
            if (_localWeapon != null)
                _localWeapon.OnShot -= RecordLocalShot;
            RestoreInput();
            _client = null;
            _localWeapon = null;
            _started = false;
            _receivedCount = 0;
            _receivedFireCount = 0;
            _receivedSwitchCount = 0;
            _receivedReloadCount = 0;
            _receivedPrepareCount = 0;
            _receivedCanceledCount = 0;
            _localShotCount = 0;
            Events.Clear();
            return new JObject { ["ok"] = true, ["operation"] = "StopEventProbe" };
        }

        private static void RecordEvent(NetworkEventMessage message)
        {
            _receivedCount++;
            if (Events.Count == MaxRecordedEvents)
                Events.Dequeue();
            var entry = new JObject
            {
                ["eventId"] = message.eventId,
                ["serverTick"] = message.serverTick,
                ["netId"] = message.netId
            };
            switch (message)
            {
                case KnifeAttackEventMessage attack:
                    entry["kind"] = "KnifeAttack";
                    entry["attack"] = attack.attackKind.ToString();
                    entry["inputTick"] = attack.inputTick;
                    break;
                case FireEventMessage fire:
                    _receivedFireCount++;
                    entry["kind"] = "Fire";
                    entry["predictionEpoch"] = fire.predictionEpoch;
                    entry["inputTick"] = fire.inputTick;
                    entry["weaponSlot"] = fire.weaponSlot.ToString();
                    entry["position"] = Vector(fire.position);
                    entry["origin"] = Vector(fire.origin);
                    entry["direction"] = Vector(fire.direction);
                    break;
                case WeaponSwitchEventMessage weaponSwitch:
                    _receivedSwitchCount++;
                    entry["kind"] = "WeaponSwitch";
                    entry["predictionEpoch"] = weaponSwitch.predictionEpoch;
                    entry["inputTick"] = weaponSwitch.inputTick;
                    entry["weaponSlot"] = weaponSwitch.weaponSlot.ToString();
                    entry["serverInitiated"] = weaponSwitch.serverInitiated;
                    break;
                case ReloadEventMessage reload:
                    _receivedReloadCount++;
                    entry["kind"] = "Reload";
                    entry["predictionEpoch"] = reload.predictionEpoch;
                    entry["inputTick"] = reload.inputTick;
                    entry["weaponSlot"] = reload.weaponSlot.ToString();
                    entry["serverInitiated"] = reload.serverInitiated;
                    break;
                case PrepareEventMessage prepare:
                    _receivedPrepareCount++;
                    entry["kind"] = "Prepare";
                    entry["predictionEpoch"] = prepare.predictionEpoch;
                    entry["inputTick"] = prepare.inputTick;
                    entry["weaponSlot"] = prepare.weaponSlot.ToString();
                    entry["serverInitiated"] = prepare.serverInitiated;
                    break;
                case WeaponActionCanceledEventMessage canceled:
                    _receivedCanceledCount++;
                    entry["kind"] = "Canceled";
                    entry["predictionEpoch"] = canceled.predictionEpoch;
                    entry["inputTick"] = canceled.inputTick;
                    entry["weaponSlot"] = canceled.weaponSlot.ToString();
                    entry["canceledState"] = canceled.canceledState.ToString();
                    entry["serverInitiated"] = canceled.serverInitiated;
                    break;
            }
            Events.Enqueue(entry);
        }

        private static void RecordLocalShot(Gun gun) => _localShotCount++;

        private static void BindObservedWeapons()
        {
            if (!_started)
                return;
            foreach (PlayerBrain brain in UnityEngine.Object.FindObjectsByType<PlayerBrain>(FindObjectsSortMode.None))
            {
                NetworkIdentity identity = brain.GetComponent<NetworkIdentity>();
                PlayerWeaponController weapon = brain.GetComponent<PlayerWeaponController>();
                if (identity == null || identity.netId == 0 || weapon == null || ObservedWeapons.ContainsKey(weapon))
                    continue;
                uint playerId = identity.netId;
                PlayerHolder holder = brain.GetComponent<PlayerHolder>();
                if (holder != null && !ObservedHolders.ContainsKey(holder))
                {
                    Action recordSwitch = () =>
                    {
                        PresentedSwitches.TryGetValue(playerId, out ulong count);
                        PresentedSwitches[playerId] = count + 1;
                    };
                    ObservedHolders.Add(holder, recordSwitch);
                    holder.OnWeaponChanged += recordSwitch;
                }
                var observation = new WeaponObservation
                {
                    Shot = _ => Increment(PresentedShots, playerId),
                    Reload = _ => Increment(PresentedReloads, playerId),
                    Prepare = _ => Increment(PresentedPrepares, playerId),
                    Canceled = (_, _) => Increment(PresentedCancellations, playerId),
                    KnifeState = (_, state) =>
                    {
                        if (state == KnifeState.Preparing) Increment(KnifePrepares, playerId);
                        if (state == KnifeState.LightAttack) Increment(LightAttacks, playerId);
                        if (state == KnifeState.HeavyAttack) Increment(HeavyAttacks, playerId);
                    }
                };
                ObservedWeapons.Add(weapon, observation);
                weapon.OnShot += observation.Shot;
                weapon.OnReload += observation.Reload;
                weapon.OnPrepare += observation.Prepare;
                weapon.OnActionCanceled += observation.Canceled;
                weapon.OnKnifeStateChanged += observation.KnifeState;
            }
        }

        private static ulong Count(Dictionary<uint, ulong> counts, uint playerId) =>
            counts.TryGetValue(playerId, out ulong count) ? count : 0UL;

        private static void Increment(Dictionary<uint, ulong> counts, uint playerId) =>
            counts[playerId] = Count(counts, playerId) + 1;

        private static JToken AnimationState(Animator animator, int layer)
        {
            if (animator == null || animator.runtimeAnimatorController == null || layer >= animator.layerCount)
                return JValue.CreateNull();
            AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(layer);
            AnimatorClipInfo[] clips = animator.GetCurrentAnimatorClipInfo(layer);
            return new JObject
            {
                ["stateHash"] = state.fullPathHash,
                ["clip"] = clips.Length > 0 ? clips[0].clip.name : null,
                ["normalizedTime"] = state.normalizedTime,
                ["layerWeight"] = animator.GetLayerWeight(layer)
            };
        }

        private sealed class WeaponObservation
        {
            public Action<Gun> Shot;
            public Action<Gun> Reload;
            public Action<Gun> Prepare;
            public Action<Gun, GunState> Canceled;
            public Action<Knife, KnifeState> KnifeState;
        }

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
