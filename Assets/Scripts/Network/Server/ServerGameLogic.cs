using System;
using System.Collections.Generic;
using GamePlay.HoldSystem;
using GamePlay.Player;
using GamePlay.Player.Combat;
using GamePlay.Player.Simulation;
using Network.Server.Combat;
using UnityEngine;

namespace Network.Server
{
    /// <summary>全体移动完成后采样历史，统一查询本拍射击并向当前数值提交伤害。</summary>
    public sealed class ServerGameLogic
    {
        readonly ServerShotResolver _resolver;
        readonly ServerShotTimeValidator _time;
        ulong _shotId;
        public ServerHitboxHistory History { get; }
        public ServerGameLogic(ServerCombatSettings settings = null)
        {
            settings ??= new ServerCombatSettings();
            History = new ServerHitboxHistory(settings.HistoryCapacity);
            _resolver = new ServerShotResolver(settings);
            _time = new ServerShotTimeValidator(settings);
        }

        public void RemovePlayer(uint id) { History.Remove(id); _time.Remove(id); }

        public GameTickResult Step(uint serverTick, IReadOnlyList<ServerPlayerInput> inputs,
            IReadOnlyDictionary<uint, PlayerBrain> players = null)
        {
            if (inputs == null) throw new ArgumentNullException(nameof(inputs));
            var actors = new Dictionary<uint, PlayerBrain>();
            if (players != null)
                foreach (var pair in players) actors[pair.Key] = pair.Value;
            foreach (var input in inputs) actors[input.PlayerId] = input.Brain;
            var processed = new List<ProcessedServerInput>();
            var events = new List<ServerGameEvent>();
            var failures = new List<ServerInputFailure>();
            var failed = new HashSet<uint>();
            var begun = new List<ServerPlayerInput>();
            var eligibility = new Dictionary<uint, bool>();
            var times = new Dictionary<uint, ValidatedShotTime>();
            var pending = new List<(ServerPlayerInput input, ShotFiredEvent shot, GunSO gun)>();
            var shots = new List<ServerShotRecord>();

            void Fail(ServerPlayerInput input, Exception error)
            {
                input.Brain?.AbortServerStep();
                failed.Add(input.PlayerId);
                RemovePlayer(input.PlayerId);
                failures.Add(new ServerInputFailure(input.PlayerId, input.PredictionEpoch, input.Frame.tick, error));
            }

            foreach (var input in inputs)
            {
                eligibility[input.PlayerId] = input.Brain != null && input.Brain.AcceptsGameplayInput;
                times[input.PlayerId] = _time.Validate(serverTick, input);
            }
            foreach (var input in inputs)
            {
                try
                {
                    input.Brain.BeginServerStep(input.Frame, eligibility[input.PlayerId]);
                    begun.Add(input);
                }
                catch (Exception error) { Fail(input, error); }
            }
            foreach (var input in begun)
            {
                try
                {
                    PlayerLiveStepResult result = input.Brain.CompleteServerStep(input.Frame);
                    processed.Add(new ProcessedServerInput(input.PlayerId, input.PredictionEpoch, input.Frame.tick));
                    foreach (var action in result.WeaponActions ?? Array.Empty<PlayerWeaponAction>())
                    {
                        ServerGameEvent fact = action.Kind switch
                        {
                            PlayerWeaponActionKind.Switched => new WeaponSwitchedEvent(serverTick, input, action.Slot, action.ServerInitiated),
                            PlayerWeaponActionKind.PrepareStarted => new PrepareStartedEvent(serverTick, input, action.Slot, action.ServerInitiated),
                            PlayerWeaponActionKind.ReloadStarted => new ReloadStartedEvent(serverTick, input, action.Slot, action.ServerInitiated),
                            PlayerWeaponActionKind.Canceled => new WeaponActionCanceledEvent(serverTick, input, action.Slot, action.CanceledState, action.ServerInitiated),
                            PlayerWeaponActionKind.Shot => new ShotFiredEvent(serverTick, input, result, action.Slot, action.FireOrigin, action.FireDirection),
                            PlayerWeaponActionKind.KnifeAttack => new KnifeAttackStartedEvent(serverTick, input, action.Slot, action.KnifeAttackKind),
                            _ => throw new ArgumentException($"不支持的武器动作 {action.Kind}。")
                        };
                        events.Add(fact);
                        if (fact is ShotFiredEvent shot)
                        {
                            var holder = input.Brain.GetComponent<PlayerHolder>();
                            var entry = action.Slot == WeaponSlot.Rifle ? holder?.Rifle : holder?.Pistol;
                            pending.Add((input, shot, entry?.Definition as GunSO));
                        }
                    }
                }
                catch (Exception error) { Fail(input, error); }
            }
            foreach (var pair in actors)
            {
                if (failed.Contains(pair.Key) || pair.Value == null) continue;
                try
                {
                    var brain = pair.Value;
                    if (brain.CombatPose != null && brain.CombatPose.IsServerDriven)
                        brain.CombatPose.StepServer(SimTime.StepSeconds);
                    if (brain.Hitboxes != null && brain.Hitboxes.IsInitialized)
                        History.Record(pair.Key, serverTick, brain.Hitboxes);
                }
                catch (Exception error)
                {
                    var input = new ServerPlayerInput(pair.Key, 0, pair.Value, default);
                    foreach (var candidate in inputs)
                        if (candidate.PlayerId == pair.Key) { input = candidate; break; }
                    Fail(input, error);
                }
            }

            // 所有几何查询先于任何伤害回调，维持本拍的互换资格。
            foreach (var item in pending)
            {
                var input = item.input;
                var shot = item.shot;
                var time = times[input.PlayerId];
                var ray = new Ray(shot.Origin, shot.Direction);
                var record = new ServerShotRecord
                {
                    ShotId = checked(++_shotId), ShooterId = input.PlayerId, InputTick = input.Frame.tick,
                    ServerTick = serverTick, RequestedTick = input.Frame.viewServerTick, Time = time, Ray = ray,
                    ReceiptTick = input.ReceiptTick, RoundTripSeconds = input.RoundTripSeconds
                };
                shots.Add(record);
                if (!eligibility[input.PlayerId] || failed.Contains(input.PlayerId) || !time.IsValid || item.gun == null)
                    continue;
                var view = new Ray(shot.Origin - shot.Direction * PlayerShootConfig.ShootIgnoreDistance, shot.Direction);
                record.Result = _resolver.Resolve(History, input.PlayerId, view, ray, time.QueryTick,
                    time.Clamped ? (uint?)null : input.Frame.viewFromTick,
                    time.Clamped ? (uint?)null : input.Frame.viewToTick);
                if (record.Result.Status == ServerShotStatus.Hit)
                {
                    ServerHitboxFrame frame;
                    HitboxHistoryFailure failure;
                    bool sampled = time.Clamped
                        ? History.TrySample(record.Result.TargetId, time.QueryTick, out frame, out failure)
                        : History.TrySamplePair(record.Result.TargetId, time.QueryTick,
                            input.Frame.viewFromTick, input.Frame.viewToTick, out frame, out failure);
                    if (sampled) record.TargetGeometry = frame.Geometry;
                }
            }
            for (int index = 0; index < pending.Count; index++)
            {
                var record = shots[index];
                if (record.Result.Status != ServerShotStatus.Hit ||
                    !actors.TryGetValue(record.Result.TargetId, out var target) || target == null ||
                    target.Hitboxes == null || target.Hitboxes.LifeGeneration != record.Result.LifeGeneration ||
                    target.Stats == null || target.Stats.LifeState != PlayerLifeState.Alive)
                    continue;
                var gun = pending[index].gun;
                record.Damage = target.Stats.ApplyDamage(record.Result.Region == HitRegion.Head ? gun.HeadDamage : gun.BodyDamage);
                record.Applied = true;
            }
            return new GameTickResult(processed, events, failures, shots);
        }
    }
}
