using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Core.Utility;
using GamePlay.HoldSystem;
using GamePlay.Player;
using GamePlay.Player.Presentation;
using GamePlay.Player.Simulation;
using Newtonsoft.Json.Linq;
using Unity.Pipeline.Commands;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using Object = UnityEngine.Object;

namespace FPSGame.Editor.Validation
{
    /// <summary>用真实 Player 和模型 Animator 按正常帧验证逻辑装填与换弹动画的独立生命周期。</summary>
    public static class ReloadAnimationValidation
    {
        private static readonly Stack<IEnumerator> Routines = new();
        private static readonly JArray Checks = new();
        private static readonly JArray Samples = new();
        private static Scope _scope;
        private static string _status = "idle";
        private static string _scenario;
        private static string _failure;
        private static int _lastFrame = -1;
        private static double _startedAt;
        private static double _finishedAt;

        static ReloadAnimationValidation()
        {
            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.ExitingPlayMode && _status == "running")
                    Finish("stopped");
            };
        }

        [CliCommand("gun_reload_animation_validate", "Validate real-time reload animation after an early logical ammo commit", Tags = new[] { "fpsgame/validation" })]
        public static JObject Run(string action = "start")
        {
            switch (action)
            {
                case "start":
                    Require(Application.isPlaying, "请先进入 Play Mode，再运行真实帧换弹动画验证。");
                    Require(_status != "running", "换弹动画验证正在运行，请通过 report 查看进度。");
                    Checks.Clear();
                    Samples.Clear();
                    _failure = null;
                    _scenario = null;
                    _lastFrame = -1;
                    _startedAt = Time.realtimeSinceStartupAsDouble;
                    _finishedAt = 0;
                    _status = "running";
                    Routines.Push(Execute());
                    EditorApplication.update += Update;
                    break;
                case "report":
                    break;
                case "stop":
                    if (_status == "running")
                        Finish("stopped");
                    break;
                default:
                    throw new ArgumentException("操作应为 start、report 或 stop。", nameof(action));
            }
            return Report();
        }

        private static JObject Report() => new()
        {
            ["status"] = _status,
            ["scenario"] = _scenario,
            ["failure"] = _failure,
            ["passed"] = Checks.Count,
            ["elapsedSeconds"] = _startedAt == 0 ? 0 :
                (_finishedAt == 0 ? Time.realtimeSinceStartupAsDouble : _finishedAt) - _startedAt,
            ["checks"] = Checks.DeepClone(),
            ["samples"] = Samples.DeepClone(),
            ["current"] = _scope?.Snapshot()
        };

        private static void Update()
        {
            if (_status != "running" || !Application.isPlaying || Time.frameCount == _lastFrame)
                return;
            _lastFrame = Time.frameCount;
            try
            {
                _scope?.AdvanceLogic();
                while (Routines.Count > 0)
                {
                    IEnumerator routine = Routines.Peek();
                    if (!routine.MoveNext())
                    {
                        (Routines.Pop() as IDisposable)?.Dispose();
                        continue;
                    }
                    if (routine.Current is IEnumerator nested)
                    {
                        Routines.Push(nested);
                        continue;
                    }
                    return;
                }
                Finish("PASS");
            }
            catch (Exception exception)
            {
                _failure = exception.ToString();
                Finish("FAIL");
            }
        }

        private static void Finish(string status)
        {
            EditorApplication.update -= Update;
            while (Routines.Count > 0)
                (Routines.Pop() as IDisposable)?.Dispose();
            _scope?.Dispose();
            _status = status;
            _finishedAt = Time.realtimeSinceStartupAsDouble;
        }

        private static IEnumerator Execute()
        {
            foreach (EPlayerModelType view in new[] { EPlayerModelType.FP, EPlayerModelType.TP })
            {
                _scenario = "LocalNatural" + view;
                using var scope = new Scope(view, false);
                yield return NaturalCompletion(scope);
            }

            _scenario = "EarlySwitchCancellation";
            using (var scope = new Scope(EPlayerModelType.FP, false))
                yield return SwitchCancellation(scope, false);

            _scenario = "FireDuringAnimationTail";
            using (var scope = new Scope(EPlayerModelType.TP, false))
                yield return FireDuringTail(scope);

            _scenario = "SwitchDuringAnimationTail";
            using (var scope = new Scope(EPlayerModelType.FP, false))
                yield return SwitchCancellation(scope, true);

            _scenario = "ViewChangesBeforeAndAfterCommit";
            using (var scope = new Scope(EPlayerModelType.FP, false))
                yield return ViewChanges(scope);

            _scenario = "RemoteNaturalAndIk";
            using (var scope = new Scope(EPlayerModelType.TP, true))
                yield return NaturalCompletion(scope);

            _scenario = "RemoteFireDuringAnimationTail";
            using (var scope = new Scope(EPlayerModelType.TP, true))
                yield return FireDuringTail(scope);

            foreach (bool remote in new[] { false, true })
            {
                _scenario = remote ? "RemoteLateCancellation" : "OwnerLateCancellation";
                using var scope = new Scope(EPlayerModelType.TP, remote);
                scope.StartReload();
                yield return WaitElapsed(scope, 1.7);
                scope.RequireLogicalCommit();
                scope.RequireReloadAnimation();
                bool canceled = remote
                    ? scope.Weapon.PresentRemoteActionCanceled(WeaponSlot.Rifle, GunState.Reloading)
                    : scope.Weapon.ApplyAuthoritativeAction(PlayerWeaponActionKind.Canceled, WeaponSlot.Rifle, GunState.Reloading);
                Require(canceled, "逻辑结束后到达的权威取消应立即结束剩余换弹表现。");
                yield return WaitFor(() => scope.IsHolding, 0.5, "晚到的取消事件未恢复持握。");
                yield return WaitElapsed(scope, scope.ClipLength + 0.1);
                Require(scope.IsHolding, "晚到取消后旧换弹动作应保持清理。");
                Samples.Add(scope.Snapshot());
                Pass("拥有者与远端在逻辑结束后接收换弹取消，立即结束动画尾部");
            }
        }

        private static IEnumerator NaturalCompletion(Scope scope)
        {
            scope.StartReload();
            yield return WaitFor(() => scope.LogicalReloadCompleted, 3, "逻辑换弹未按配置完成。");
            scope.RequireLogicalCommit();
            scope.RequireReloadAnimation();
            Samples.Add(scope.Snapshot());
            Pass("85 Tick 提交逻辑装填后，角色和枪械模型继续原时长换弹动画");
            Require(scope.Aim.CurrentAimWeight == 0f, "换弹动画尾部应继续让出双臂瞄准 IK。");

            yield return WaitFor(() => scope.IsHolding, scope.ClipLength + 1,
                "原换弹 Clip 结束后角色和模型未自然恢复持握。");
            Require(scope.Elapsed >= scope.ClipLength - 0.1,
                "角色和模型应在原换弹 Clip 完整播放后恢复持握。");
            Require(scope.Elapsed < scope.ClipLength + 0.5,
                "完整换弹动画结束后应及时恢复持握。");
            yield return WaitFor(() => scope.Aim.CurrentAimWeight == 1f || scope.View == EPlayerModelType.FP,
                1, "换弹动画自然完成后瞄准 IK 未恢复。");
            Samples.Add(scope.Snapshot());
            Pass("约 145 Tick 的原动画播放完毕后自然恢复持握及 TP 瞄准");
        }

        private static IEnumerator SwitchCancellation(Scope scope, bool afterCommit)
        {
            scope.StartReload();
            yield return WaitElapsed(scope, afterCommit ? 1.7 : 0.3);
            if (afterCommit)
                scope.RequireLogicalCommit();
            else
                Require(scope.Gun.GunState == GunState.Reloading, "提前中断样本必须仍处于逻辑换弹中。");
            int rounds = scope.Gun.Magazine.Rounds;
            int reserve = scope.Gun.Magazine.ReserveRounds;
            Require(scope.Holder.SelectSlot(WeaponSlot.Knife), "应能切刀中断换弹表现。");
            yield return null;
            yield return WaitFor(() => scope.IsKnifeAnimation, 0.5, "切刀后角色未进入刀具动作。");
            Require(!scope.Presenter.IsReloadAnimationPlaying && scope.Gun.GunState == GunState.None,
                "切刀应清理换弹动画跟踪并让旧枪进入未操控状态。");
            yield return WaitElapsed(scope, scope.ClipLength + 0.15);
            Require(scope.IsKnifeAnimation && scope.Gun.Magazine.Rounds == rounds &&
                    scope.Gun.Magazine.ReserveRounds == reserve,
                "旧换弹动作结束时应继续刀具表现，并保持切枪时的弹药结果。");
            Samples.Add(scope.Snapshot());
            Pass(afterCommit ? "逻辑装填完成后切刀，动画尾部立即让出控制" : "提前切刀取消换弹，旧动画计时保持清理且弹药正确");
        }

        private static IEnumerator FireDuringTail(Scope scope)
        {
            scope.StartReload();
            yield return WaitElapsed(scope, 1.9);
            scope.RequireLogicalCommit();
            scope.RequireReloadAnimation();
            Require(scope.Fire(), "逻辑换弹完成后应能立即开火中断动画尾部。");
            yield return null;
            yield return WaitFor(() => scope.IsFireAnimation, 0.4, "开火后角色未进入射击动画。");
            Require(!scope.Presenter.IsReloadAnimationPlaying, "开火应清理上一段换弹动画跟踪。");
            yield return WaitElapsed(scope, scope.ClipLength + 0.04);
            Require(scope.IsFireAnimation, "旧换弹 Clip 的结束时间不得覆盖仍在播放的射击动画。");
            Samples.Add(scope.Snapshot());
            Pass("本地与远端在换弹动画尾部开火，原换弹结束时间保持新射击表现");
        }

        private static IEnumerator ViewChanges(Scope scope)
        {
            scope.StartReload();
            yield return WaitElapsed(scope, 0.65);
            yield return ChangeView(scope, EPlayerModelType.TP);
            yield return WaitElapsed(scope, 1.7);
            scope.RequireLogicalCommit();
            yield return ChangeView(scope, EPlayerModelType.FP);
            yield return WaitFor(() => scope.IsHolding, scope.ClipLength + 1,
                "视角切换后的换弹动画未自然恢复持握。");
            Samples.Add(scope.Snapshot());
            Pass("逻辑装填前后切换 FP/TP，角色与枪械模型延续同一段原动画进度");
        }

        private static IEnumerator ChangeView(Scope scope, EPlayerModelType target)
        {
            Require(scope.Presenter.TryGetReloadAnimationTime(out float before), "切视角前应能读取正在播放的换弹时间。");
            scope.SetView(target);
            yield return null;
            yield return WaitFor(() => scope.ActorIs(WeaponPresentationAction.Reload), 0.4,
                "视角切换后应继续对应视角的换弹动画。");
            Require(scope.Presenter.TryGetReloadAnimationTime(out float after) && after >= before - 0.08f && after < before + 0.3f,
                "视角切换应延续换弹动画的已播放时间。");
            scope.RequireReloadAnimation();
            Samples.Add(scope.Snapshot());
        }

        private static IEnumerator WaitElapsed(Scope scope, double seconds)
        {
            while (scope.Elapsed < seconds)
                yield return null;
        }

        private static IEnumerator WaitFor(Func<bool> condition, double timeout, string failure)
        {
            double start = Time.realtimeSinceStartupAsDouble;
            while (!condition())
            {
                Require(Time.realtimeSinceStartupAsDouble - start <= timeout, failure);
                yield return null;
            }
        }

        private static void Pass(string message) => Checks.Add(new JObject { ["scenario"] = _scenario, ["check"] = message });

        private static void Require(bool condition, string message)
        {
            if (!condition)
                throw new InvalidOperationException(message);
        }

        private sealed class Scope : IDisposable
        {
            public readonly GameObject Root;
            public readonly PlayerBrain Brain;
            public readonly PlayerHolder Holder;
            public readonly PlayerWeaponController Weapon;
            public readonly PlayerAnimatorController Presenter;
            public readonly PlayerAimPoseController Aim;
            public readonly Animator Actor;
            public readonly Gun Gun;
            public readonly bool Remote;
            public readonly float ClipLength;
            public EPlayerModelType View { get; private set; }
            private readonly IWeaponPresentationResources _resources;
            private double _reloadStartedAt;
            private double _nextTickAt;
            private uint _elapsedTicks;
            private int _roundsBeforeReload;
            private int _reserveBeforeReload;
            private bool _stepping;
            private bool _disposed;

            public Scope(EPlayerModelType view, bool remote)
            {
                Require(_scope == null, "隔离换弹验证应按顺序创建角色。");
                _scope = this;
                Remote = remote;
                try
                {
                    GameObject source = Mirror.NetworkManager.singleton?.playerPrefab;
                    Require(source != null, "当前测试场景的 NetworkManager 需要配置联网玩家预制体。");
                    Root = Object.Instantiate(source);
                    Root.name = "ReloadAnimationValidation_" + _scenario;
                    Root.hideFlags = HideFlags.HideAndDontSave;
                    Root.transform.position = new Vector3(0f, -1000f, 0f);
                    Brain = Root.GetComponent<PlayerBrain>();
                    Brain.SetRole(remote ? PawnRole.Interpolated : PawnRole.Predicted);
                    Brain.Prediction.enabled = false;
                    Brain.Interpolator.enabled = false;
                    Brain.CorrectionSmoother.enabled = false;
                    Brain.Simulation.SetPhysical(false);
                    foreach (Renderer renderer in Root.GetComponentsInChildren<Renderer>(true))
                        renderer.forceRenderingOff = true;
                    foreach (Camera camera in Root.GetComponentsInChildren<Camera>(true))
                        camera.enabled = false;
                    foreach (AudioListener listener in Root.GetComponentsInChildren<AudioListener>(true))
                        listener.enabled = false;
                    PlayerFireAudioTester audio = Root.GetComponent<PlayerFireAudioTester>();
                    if (audio != null)
                        audio.enabled = false;
                    Holder = Root.GetComponent<PlayerHolder>();
                    Weapon = Root.GetComponent<PlayerWeaponController>();
                    Presenter = Root.GetComponent<PlayerAnimatorController>();
                    Aim = Root.GetComponent<PlayerAimPoseController>();
                    Actor = new SerializedObject(Presenter).FindProperty("_mainAnimator").objectReferenceValue as Animator;
                    Require(Actor != null && Aim != null, "真实 Player 需要角色 Animator 与瞄准姿态组件。");
                    Actor.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                    Actor.enabled = true;
                    GunSO definition = AssetDatabase.LoadAssetAtPath<GunSO>("Assets/SO/GunData/Definitions/AK47SO.asset");
                    Require(definition != null && Holder.InitializeLoadout() && Holder.EquipWeapon(definition),
                        "真实 AK47 应能通过装备栏创建枪械和模型。");
                    Gun = Holder.CurrentWeapon as Gun;
                    Require(Gun != null && Gun.Config.ReloadTicks == 85, "回归样本要求 AK47 逻辑换弹为 85 Tick。");
                    _resources = Holder.CurrentPresentationResources;
                    SetView(view);
                    Brain.Sight.ApplyAngles(0f, 45f);
                    ModelAnimator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                    foreach (Renderer renderer in Holder.CurrentController.GetComponentsInChildren<Renderer>(true))
                        renderer.forceRenderingOff = true;
                    float fpLength = Clip(Actor, PlayerAnimatorController.FPLayer, WeaponPresentationAction.Reload,
                        WeaponAnimationTarget.FirstPerson).length;
                    float tpLength = Clip(Actor, PlayerAnimatorController.UpLayer, WeaponPresentationAction.Reload,
                        WeaponAnimationTarget.ThirdPerson).length;
                    ClipLength = Clip(ModelAnimator, 0, WeaponPresentationAction.Reload, WeaponAnimationTarget.Model).length;
                    Require(Mathf.Abs(fpLength - 145f / 60f) < 0.001f && Mathf.Abs(tpLength - fpLength) < 0.001f &&
                            Mathf.Abs(ClipLength - fpLength) < 0.001f && Actor.speed == 1f && ModelAnimator.speed == 1f,
                        "FP、TP、枪械模型应保持约 2.4167 秒原动画时长与正常播放速度。");
                    if (!remote)
                        for (uint i = 0; i <= Gun.Config.PrepareTicks; i++)
                            Weapon.StepLocal(false, false);
                }
                catch
                {
                    Dispose();
                    throw;
                }
            }

            private Animator ModelAnimator => (Holder.CurrentController as GunModelController)?.ModelAnimator;
            public double Elapsed => _reloadStartedAt == 0 ? 0 : Time.unscaledTimeAsDouble - _reloadStartedAt;
            public bool LogicalReloadCompleted => Remote ? _stepping && !Weapon.IsRemoteReloading :
                _elapsedTicks >= Gun.Config.ReloadTicks && Gun.GunState == GunState.Normal;
            private int ActorLayer => View == EPlayerModelType.FP ? PlayerAnimatorController.FPLayer : PlayerAnimatorController.UpLayer;
            private WeaponAnimationTarget ActorTarget => View == EPlayerModelType.FP
                ? WeaponAnimationTarget.FirstPerson : WeaponAnimationTarget.ThirdPerson;
            public bool IsHolding => !Presenter.IsReloadAnimationPlaying && ActorIs(WeaponPresentationAction.Hold) &&
                ModelIs(WeaponPresentationAction.Hold);
            public bool IsFireAnimation => ActorIs(WeaponPresentationAction.Fire) && ModelIs(WeaponPresentationAction.Fire);
            public bool IsKnifeAnimation
            {
                get
                {
                    if (Holder.CurrentSlot != WeaponSlot.Knife) return false;
                    AnimatorStateInfo state = Actor.GetCurrentAnimatorStateInfo(ActorLayer);
                    if (View == EPlayerModelType.TP) return state.IsName("Up Layer.tp_draw_butterfly");
                    return state.IsName("FP Layer.fp_draw_butterfly") || state.IsName("FP Layer.fp_draw2_butterfly") ||
                        state.IsName("FP Layer.fp_idle1_butterfly");
                }
            }

            public void StartReload()
            {
                if (Remote)
                {
                    Require(Weapon.PresentRemoteFire(WeaponSlot.Rifle) && Weapon.PresentRemoteReload(WeaponSlot.Rifle),
                        "远端事件入口应能依次驱动射击和换弹表现。");
                }
                else
                {
                    Require(Weapon.StepLocal(true, false).Fired && Weapon.StepLocal(false, true).ReloadStarted,
                        "本地应先实际消耗一发，再通过正常控制器输入开始换弹。");
                }
                _roundsBeforeReload = Gun.Magazine.Rounds;
                _reserveBeforeReload = Gun.Magazine.ReserveRounds;
                _reloadStartedAt = Time.unscaledTimeAsDouble;
                _nextTickAt = _reloadStartedAt + SimTime.Dt;
                _elapsedTicks = 0;
                _stepping = true;
            }

            public void AdvanceLogic()
            {
                if (!_stepping || _disposed || Remote)
                    return;
                double now = Time.unscaledTimeAsDouble;
                while (now >= _nextTickAt)
                {
                    Weapon.StepLocal(false, false);
                    _elapsedTicks++;
                    _nextTickAt += SimTime.Dt;
                }
            }

            public bool Fire() => Remote ? Weapon.PresentRemoteFire(WeaponSlot.Rifle) : Weapon.StepLocal(true, false).Fired;

            public void SetView(EPlayerModelType view)
            {
                View = view;
                Brain.Motion.SetView(view);
            }

            public void RequireLogicalCommit()
            {
                Require(LogicalReloadCompleted, "逻辑换弹应在 85 Tick 时完成。");
                if (!Remote)
                    Require(Gun.Magazine.Rounds == Gun.Magazine.Capacity &&
                            Gun.Magazine.ReserveRounds == _reserveBeforeReload - (Gun.Magazine.Capacity - _roundsBeforeReload),
                        "逻辑完成应已提交正确的弹匣与备弹转移。");
            }

            public void RequireReloadAnimation()
            {
                Require(Presenter.IsReloadAnimationPlaying && ActorIs(WeaponPresentationAction.Reload) &&
                        ModelIs(WeaponPresentationAction.Reload), "角色和枪械模型应仍在换弹动画中。");
                float actorTime = Actor.GetCurrentAnimatorStateInfo(ActorLayer).normalizedTime;
                float modelTime = ModelAnimator.GetCurrentAnimatorStateInfo(0).normalizedTime;
                Require(actorTime > 0f && actorTime < 1f && modelTime > 0f && modelTime < 1f &&
                        Mathf.Abs(actorTime - modelTime) < 0.1f,
                    "角色和模型应以原速度同步播放换弹中段。");
                Require(Actor.speed == 1f && ModelAnimator.speed == 1f, "换弹应保持正常 Animator 播放速度。");
            }

            public bool ActorIs(WeaponPresentationAction action) => IsState(Actor, ActorLayer, action, ActorTarget);
            private bool ModelIs(WeaponPresentationAction action) => IsState(ModelAnimator, 0, action, WeaponAnimationTarget.Model);

            private bool IsState(Animator animator, int layer, WeaponPresentationAction action, WeaponAnimationTarget target)
            {
                if (animator == null || !_resources.TryGetAnimation(action, target, out string name))
                    return false;
                return animator.GetCurrentAnimatorStateInfo(layer).fullPathHash == Animator.StringToHash(name);
            }

            private AnimationClip Clip(Animator animator, int layer, WeaponPresentationAction action, WeaponAnimationTarget target)
            {
                Require(_resources.TryGetAnimation(action, target, out string name), "资源包缺少换弹动画名称。");
                var controller = animator.runtimeAnimatorController as AnimatorController;
                Require(controller != null, "真实 Animator 需要提供动画控制器。");
                AnimatorState state = FindState(controller.layers[layer].stateMachine, name);
                Require(state != null && state.motion is AnimationClip, "真实 Animator 状态需要绑定换弹 Clip。");
                return (AnimationClip)state.motion;
            }

            public JObject Snapshot()
            {
                if (_disposed || Root == null)
                    return null;
                return new JObject
                {
                    ["scenario"] = _scenario,
                    ["remote"] = Remote,
                    ["view"] = View.ToString(),
                    ["elapsedSeconds"] = Elapsed,
                    ["elapsedTicks"] = _elapsedTicks,
                    ["configuredReloadTicks"] = Gun.Config.ReloadTicks,
                    ["clipLength"] = ClipLength,
                    ["gunState"] = Gun.GunState.ToString(),
                    ["slot"] = Holder.CurrentSlot?.ToString(),
                    ["rounds"] = Gun.Magazine.Rounds,
                    ["reserveRounds"] = Gun.Magazine.ReserveRounds,
                    ["remoteLogicalReloading"] = Weapon.IsRemoteReloading,
                    ["reloadAnimationPlaying"] = Presenter.IsReloadAnimationPlaying,
                    ["aimWeight"] = Aim.CurrentAimWeight,
                    ["actor"] = StateData(Actor, ActorLayer),
                    ["model"] = StateData(ModelAnimator, 0)
                };
            }

            public void Dispose()
            {
                if (_disposed)
                    return;
                _disposed = true;
                _stepping = false;
                if (Root != null)
                {
                    Root.SetActive(false);
                    Object.Destroy(Root);
                }
                if (ReferenceEquals(_scope, this))
                    _scope = null;
            }
        }

        private static JObject StateData(Animator animator, int layer)
        {
            if (animator == null || animator.runtimeAnimatorController == null)
                return null;
            AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(layer);
            return new JObject
            {
                ["hash"] = state.fullPathHash,
                ["normalizedTime"] = state.normalizedTime,
                ["speed"] = animator.speed,
                ["clips"] = new JArray(animator.GetCurrentAnimatorClipInfo(layer).Select(info => info.clip.name))
            };
        }

        private static AnimatorState FindState(AnimatorStateMachine machine, string fullName)
        {
            foreach (ChildAnimatorState child in machine.states)
                if (fullName == child.state.name || fullName.EndsWith("." + child.state.name, StringComparison.Ordinal))
                    return child.state;
            foreach (ChildAnimatorStateMachine child in machine.stateMachines)
            {
                AnimatorState found = FindState(child.stateMachine, fullName);
                if (found != null)
                    return found;
            }
            return null;
        }
    }
}
