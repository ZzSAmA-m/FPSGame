using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using GamePlay.HoldSystem;
using GamePlay.Player;
using GamePlay.Player.Simulation;
using Unity.Pipeline.Commands;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FPSGame.Editor.Validation
{
    /// <summary>隔离验证枪械逻辑事件及本地角色转发，不修改场景或配置资源。</summary>
    public static class WeaponLifecycleValidation
    {
        private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
        private static readonly string[] GunEvents =
        {
            "OnShot", "OnPrepare", "OnPrepareCompleted", "OnReload", "OnReloadCompleted",
            "OnShotCooldownEnd", "OnActionCanceled"
        };
        private static bool _running;

        [CliCommand("weapon_lifecycle_validate", "Validate weapon action starts, completion, cancellation and forwarding", Tags = new[] { "fpsgame/validation" })]
        public static string Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return "REFUSED：请先退出 Play Mode，再验证动作生命周期。";
            if (_running) return "REFUSED：动作生命周期验证已经在执行。";
            _running = true;
            var report = new StringBuilder();
            int passed = 0;
            try
            {
                RunCase("准备事件提交状态，失败请求与旧 Tick 不重复通知", TestPreparation, report, ref passed);
                RunCase("换弹完成兼容原事件且先提交弹药", TestReloadCompletion, report, ref passed);
                RunCase("每发冷却结束一次，并先于同 Tick 的下一发", TestCooldown, report, ref passed);
                RunCase("收枪取消准备、换弹和射击而不发送正常完成", TestCancellation, report, ref passed);
                RunCase("成功开火打断换弹时先取消再通知射击", TestReloadInterruption, report, ref passed);
                RunCase("取消射击保留冷却限制但不补发旧周期结束", TestRetainedCooldown, report, ref passed);
                RunCase("五种状态按转换矩阵接受动作且拒绝自转换", TestStateTransitionMatrix, report, ref passed);
                RunCase("换弹提前退出不会装填或补发完成", TestReloadEarlyExits, report, ref passed);
                RunCase("射击提前退出保留已发射结果和独立冷却", TestShootingEarlyExits, report, ref passed);
                RunCase("空弹射击失败继续换弹且无备弹时拒绝换弹", TestReloadRejections, report, ref passed);
                RunCase("准备与换弹进度遵守模拟拍且支持回绕", TestActionProgress, report, ref passed);
                RunCase("主动恢复就绪拒绝生命周期回调重入", TestReturnReentrancy, report, ref passed);
                RunCase("控制器转发全部开始及完成事件和实际枪实例", TestControllerForwarding, report, ref passed);
                RunCase("切枪在解绑前转发旧枪取消，切回不补历史事件", TestControllerSwitching, report, ref passed);
                RunCase("启停和替换枪械成对清理全部订阅", TestControllerLifecycle, report, ref passed);
                RunCase("预测和权威角色转发枪械生命周期，远端与未绑定身份保持隔离", TestControllerRoles, report, ref passed);
                return "PASS " + passed + " checks\n" + report;
            }
            finally { _running = false; }
        }

        private static AK47 NewGun(uint shotInterval = 3) => new(new GunConfig(20, 4, 2, shotInterval, 4));

        private static void Ready(Gun gun)
        {
            gun.Tick(0);
            Require(gun.TryPrepare(), "新枪应能开始准备。");
            gun.Tick(gun.Config.PrepareTicks);
            Require(gun.GunState == GunState.Normal, "准备截止拍应进入就绪。");
        }

        private static void TestPreparation()
        {
            AK47 gun = NewGun();
            var events = Trace(gun);
            gun.OnPrepare += () => Require(gun.GunState == GunState.Preparing, "开始事件必须在状态提交后通知。");
            gun.OnPrepareCompleted += () => Require(gun.GunState == GunState.Normal, "原准备完成事件必须看到就绪状态。");
            gun.Tick(0);
            Require(!gun.TryFire() && !gun.TryReload(), "未准备时请求应失败。");
            Require(gun.TryPrepare() && !gun.TryPrepare() && !gun.TryReload(), "重复准备或准备期间换弹应失败。");
            gun.Tick(1);
            gun.Tick(1);
            gun.Tick(0);
            Expect(events, "Prepare");
            Require(!gun.TryFire() && gun.Magazine.Rounds == 4, "准备未完成时不得扣弹。");
            gun.Tick(2);
            Require(gun.TryFire(), "准备完成当拍可直接射击。");
            Expect(events, "Prepare", "PrepareEnd", "Shot");
        }

        private static void TestReloadCompletion()
        {
            AK47 gun = NewGun();
            Ready(gun);
            Require(!gun.TryReload(), "满弹匣换弹应失败且不发送开始事件。");
            Require(gun.TryFire(), "应先消耗一发弹药。");
            gun.Tick(5);
            var events = Trace(gun);
            int reserve = gun.Magazine.ReserveRounds;
            gun.OnReload += () => Require(gun.GunState == GunState.Reloading && gun.Magazine.Rounds == 3,
                "换弹开始时不能提前装填。");
            gun.OnReloadCompleted += () => Require(gun.GunState == GunState.Normal && gun.Magazine.Rounds == 4 &&
                gun.Magazine.ReserveRounds == reserve - 1, "原换弹完成事件必须在状态和弹药提交后通知。");
            Require(gun.TryReload() && !gun.TryReload(), "只允许一次换弹开始。");
            gun.Tick(8);
            Expect(events, "Reload");
            gun.Tick(9);
            gun.Tick(9);
            gun.Tick(8);
            Expect(events, "Reload", "ReloadEnd");

            AK47 empty = new(new GunConfig(0, 4, 2, 3, 4));
            Ready(empty);
            var rejected = Trace(empty);
            Require(!empty.TryReload() && !empty.TryFire(), "无弹药时不得换弹或射击。");
            Expect(rejected);
        }

        private static void TestCooldown()
        {
            AK47 gun = NewGun();
            Ready(gun);
            var events = Trace(gun);
            gun.OnShotCooldownEnd += () => Require(gun.GunState == GunState.Normal, "冷却结束必须先进入就绪状态。");
            Require(gun.TryFire(), "应能开始第一发。");
            gun.Tick(4);
            Require(!gun.TryFire(), "冷却截止拍前不能再次射击。");
            Expect(events, "Shot");
            gun.Tick(5);
            Require(gun.TryFire(), "结束上一发冷却的同拍可以再次射击。");
            Expect(events, "Shot", "ShotCooldownEnd", "Shot");
            gun.Tick(5);
            gun.Tick(4);
            Expect(events, "Shot", "ShotCooldownEnd", "Shot");
            gun.Tick(8);
            gun.Tick(12);
            Expect(events, "Shot", "ShotCooldownEnd", "Shot", "ShotCooldownEnd");
        }

        private static void TestCancellation()
        {
            foreach (GunState canceled in new[] { GunState.Preparing, GunState.Reloading, GunState.Shooting })
            {
                AK47 gun = NewGun();
                if (canceled == GunState.Preparing) Require(gun.TryPrepare(), "应进入准备。");
                else
                {
                    Ready(gun);
                    Require(gun.TryFire(), "应进入射击。");
                    if (canceled == GunState.Reloading)
                    {
                        gun.Tick(5);
                        Require(gun.TryReload(), "应进入换弹。");
                    }
                }
                var events = Trace(gun);
                int rounds = gun.Magazine.Rounds;
                int reserve = gun.Magazine.ReserveRounds;
                gun.Unequip();
                Require(gun.GunState == GunState.None, "取消后应处于未装备状态。");
                gun.Unequip();
                gun.Tick(100);
                Expect(events, "Canceled:" + canceled);
                Require(gun.Magazine.Rounds == rounds && gun.Magazine.ReserveRounds == reserve,
                    "取消动作不得提交额外弹药变化。");
            }
        }

        private static void TestReloadInterruption()
        {
            AK47 gun = NewGun();
            Ready(gun);
            Require(gun.TryFire(), "先制造弹匣缺口。");
            gun.Tick(5);
            Require(gun.TryReload(), "应开始换弹。");
            var events = Trace(gun);
            int reserve = gun.Magazine.ReserveRounds;
            Require(gun.TryFire(), "剩余弹药可成功开火打断换弹。");
            Expect(events, "Canceled:Reloading", "Shot");
            Require(gun.Magazine.Rounds == 2 && gun.Magazine.ReserveRounds == reserve,
                "打断时只消耗本次射击，不得提前补弹。");
            gun.Tick(9);
            Require(!events.Contains("ReloadEnd"), "被打断的换弹不能在原截止拍补发完成事件。");
        }

        private static void TestRetainedCooldown()
        {
            AK47 gun = NewGun(10);
            Ready(gun);
            Require(gun.TryFire(), "应在 Tick 2 发射。");
            var events = Trace(gun);
            gun.Tick(3);
            gun.Unequip();
            Expect(events, "Canceled:Shooting");
            gun.Tick(4);
            Require(gun.TryPrepare(), "收起后可以重新准备。");
            gun.Tick(6);
            Require(!gun.TryFire(), "重新准备完成后仍应遵守保留的射击冷却。");
            gun.Tick(12);
            Require(!events.Contains("ShotCooldownEnd"), "重新装备后不得补发已取消周期的冷却结束。");
            Require(gun.TryFire(), "原冷却到期后仍可正常射击。");
            gun.Tick(22);
            Expect(events, "Canceled:Shooting", "Prepare", "PrepareEnd", "Shot", "ShotCooldownEnd");
        }

        private static void TestStateTransitionMatrix()
        {
            foreach (GunState source in Enum.GetValues(typeof(GunState)))
            foreach (GunState target in Enum.GetValues(typeof(GunState)))
            {
                AK47 gun = GunInState(source);
                var events = Trace(gun);
                int rounds = gun.Magazine.Rounds;
                int reserve = gun.Magazine.ReserveRounds;
                bool expected = source switch
                {
                    GunState.None => target == GunState.Preparing,
                    GunState.Preparing => target == GunState.None,
                    _ => target != source
                };
                bool changed = TryTransition(gun, target);
                Require(changed == expected, source + " 到 " + target + " 的接受结果错误。");
                Require(gun.GunState == (expected ? target : source), "转换后状态错误。");
                Require(gun.Magazine.Rounds == rounds - (expected && target == GunState.Shooting ? 1 : 0) &&
                    gun.Magazine.ReserveRounds == reserve, "动作转换只能为成功射击扣除一发。");
                if (!expected)
                    Expect(events);
            }
        }

        private static AK47 GunInState(GunState state)
        {
            AK47 gun = NewGun(10);
            gun.Tick(0);
            if (state == GunState.None)
                return gun;
            Require(gun.TryPrepare(), "应能建立准备状态。");
            if (state == GunState.Preparing)
                return gun;
            gun.Tick(2);
            Require(gun.TryFire(), "应能建立射击状态及弹匣缺口。");
            if (state == GunState.Shooting)
                return gun;
            gun.Tick(12);
            if (state == GunState.Reloading)
                Require(gun.TryReload(), "应能建立换弹状态。");
            return gun;
        }

        private static bool TryTransition(Gun gun, GunState target)
        {
            switch (target)
            {
                case GunState.None:
                    GunState previous = gun.GunState;
                    gun.Unequip();
                    return previous != gun.GunState;
                case GunState.Preparing: return gun.TryPrepare();
                case GunState.Shooting: return gun.TryFire();
                case GunState.Reloading: return gun.TryReload();
                case GunState.Normal: return gun.TryReturnToNormal();
                default: throw new ArgumentOutOfRangeException(nameof(target));
            }
        }

        private static void TestReloadEarlyExits()
        {
            foreach (GunState target in new[] { GunState.None, GunState.Preparing, GunState.Shooting, GunState.Normal })
            {
                AK47 gun = GunInState(GunState.Reloading);
                var events = Trace(gun);
                int rounds = gun.Magazine.Rounds;
                int reserve = gun.Magazine.ReserveRounds;
                Require(TryTransition(gun, target), "换弹应能提前转入 " + target + "。");
                Require(events.Count > 0 && events[0] == "Canceled:Reloading", "应先通知旧换弹取消。");
                gun.Tick(100);
                Require(!events.Contains("ReloadEnd"), "提前退出不能补发换弹完成。");
                Require(gun.Magazine.Rounds == rounds - (target == GunState.Shooting ? 1 : 0) &&
                    gun.Magazine.ReserveRounds == reserve, "提前退出不能提交装填。");
            }
        }

        private static void TestShootingEarlyExits()
        {
            foreach (GunState target in new[] { GunState.Normal, GunState.Preparing, GunState.Reloading })
            {
                AK47 gun = NewGun(10);
                Ready(gun);
                var events = Trace(gun);
                Require(gun.TryFire(), "应先成功发射一发。");
                int reserve = gun.Magazine.ReserveRounds;
                Require(TryTransition(gun, target), "射击应能提前转入 " + target + "。");
                Require(events[0] == "Shot" && events[1] == "Canceled:Shooting", "已成功射击应先于动作退出保留。");
                Require(gun.Magazine.Rounds == 3, "提前退出不能撤销已扣除的射击弹药。");
                gun.Tick(6);
                Require(gun.GunState == GunState.Normal, "准备或换弹结束后应恢复就绪。");
                Require(gun.Magazine.ReserveRounds == reserve - (target == GunState.Reloading ? 1 : 0),
                    "只有完成换弹才能消耗备弹。");
                Require(!gun.TryFire(), "动作退出与换弹完成都不能绕过原射击冷却。");
                gun.Tick(11);
                Require(!gun.TryFire(), "原射击冷却截止拍前仍不能开火。");
                gun.Tick(12);
                Require(!events.Contains("ShotCooldownEnd"), "提前退出的射击周期不能补发冷却完成。");
                Require(gun.TryFire(), "原射击冷却到期后应能再次开火。");
            }
        }

        private static void TestReloadRejections()
        {
            AK47 gun = new(new GunConfig(2, 1, 2, 10, 4));
            Ready(gun);
            Require(gun.TryFire() && gun.TryReload(), "空弹匣可在射击间隔内开始换弹。");
            var events = Trace(gun);
            Require(!gun.TryFire() && !gun.TryReload() && gun.GunState == GunState.Reloading,
                "空弹射击和重复换弹请求应继续原换弹。");
            Expect(events);
            gun.Tick(6);
            Expect(events, "ReloadEnd");
            Require(gun.Magazine.Rounds == 1 && gun.Magazine.ReserveRounds == 0, "应仅装填剩余备弹。");
            gun.Tick(12);
            Require(gun.TryFire(), "冷却到期后应能发射装填的一发。");
            events.Clear();
            Require(!gun.TryReload() && gun.GunState == GunState.Shooting, "无备弹时应保留当前射击动作。");
            Expect(events);
        }

        private static void TestActionProgress()
        {
            AK47 gun = NewGun();
            gun.Tick(0);
            Require(gun.ActionProgress == 0f && gun.TryPrepare(), "新枪进度应为零并可开始准备。");
            gun.Tick(1);
            Require(gun.ActionProgress == 0.5f, "准备进度应按模拟拍推进到一半。");
            gun.Tick(1);
            gun.Tick(0);
            Require(gun.ActionProgress == 0.5f && !gun.TryReturnToNormal(), "旧拍及主动取消不能提前结束准备。");
            gun.Tick(2);
            Require(gun.ActionProgress == 0f && gun.TryFire() && gun.TryReload(), "新换弹进度应从零开始。");
            gun.Tick(3);
            Require(gun.ActionProgress == 0.25f && !gun.TryReload(), "重复换弹不能重置动作进度。");
            gun.Tick(5);
            Require(gun.ActionProgress == 0.75f && gun.TryReturnToNormal(), "换弹可在完成前主动退出。");
            Require(gun.ActionProgress == 0f, "退出动作后进度应归零。");
            gun.Tick(6);
            Require(gun.Magazine.Rounds == 3, "已取消动作不得在旧截止拍装填。");

            AK47 wrapped = NewGun();
            wrapped.Tick(uint.MaxValue - 3);
            Require(wrapped.TryPrepare(), "回绕前应能开始准备。");
            wrapped.Tick(uint.MaxValue - 2);
            Require(wrapped.ActionProgress == 0.5f, "回绕前准备进度应正确。");
            wrapped.Tick(uint.MaxValue - 1);
            Require(wrapped.TryFire() && wrapped.TryReload(), "回绕前应能开始换弹。");
            wrapped.Tick(0);
            Require(wrapped.ActionProgress == 0.5f, "换弹进度应跨 uint 回绕正确推进。");
            wrapped.Tick(2);
            Require(wrapped.GunState == GunState.Normal && wrapped.ActionProgress == 0f && wrapped.Magazine.Full,
                "回绕后的截止拍应完成装填并退出动作进度。");
        }

        private static void TestReturnReentrancy()
        {
            AK47 gun = GunInState(GunState.Reloading);
            var events = Trace(gun);
            gun.OnActionCanceled += _ => Require(!gun.TryPrepare() && !gun.TryReload() && !gun.TryReturnToNormal(),
                "动作取消回调中的新操作应全部被拒绝。");
            Require(gun.TryReturnToNormal() && gun.GunState == GunState.Normal, "外层恢复就绪应成功。");
            Expect(events, "Canceled:Reloading");
        }

        private static void TestControllerForwarding()
        {
            using var scope = new PreviewScope();
            AK47 gun = scope.EquipRifle();
            var events = Trace(scope.Controller, gun);
            Advance(scope.Controller, 3, true);
            Expect(events, "Prepare", "PrepareEnd", "Shot");
            Advance(scope.Controller, 10, false);
            Require(gun.TryReload(), "控制器当前枪应能开始换弹。");
            Advance(scope.Controller, 3, false);
            Expect(events, "Prepare", "PrepareEnd", "Shot", "ShotCooldownEnd", "Reload", "ReloadEnd");
        }

        private static void TestControllerSwitching()
        {
            foreach (GunState canceled in new[] { GunState.Preparing, GunState.Reloading, GunState.Shooting })
            {
                using var scope = new PreviewScope();
                AK47 gun = scope.EquipRifle();
                var events = Trace(scope.Controller, gun);
                Advance(scope.Controller, canceled == GunState.Preparing ? 1u : 3u, true);
                if (canceled == GunState.Reloading)
                {
                    Advance(scope.Controller, 10, false);
                    Require(gun.TryReload(), "切枪测试前应开始换弹。");
                }
                events.Clear();
                scope.Controller.OnActionCanceled += (emitted, state) => Require(
                    ReferenceEquals(scope.Holder.CurrentWeapon, emitted), "旧枪取消应在当前实例更新和解绑前转发。");
                Require(scope.Holder.SelectSlot(WeaponSlot.Knife), "应能切到刀。");
                Expect(events, "Canceled:" + canceled);
                CheckListeners(gun, scope.Controller, 0);
                Advance(scope.Controller, 20, true);
                Expect(events, "Canceled:" + canceled);
                Require(scope.Holder.SelectSlot(WeaponSlot.Rifle), "应能切回已有长枪。");
                Require(ReferenceEquals(scope.Holder.CurrentWeapon, gun), "切回必须保留已有枪和弹药。");
                Expect(events, "Canceled:" + canceled);
                scope.Controller.StepLocal(false);
                Expect(events, "Canceled:" + canceled, "Prepare");
            }
        }

        private static void TestControllerLifecycle()
        {
            using var scope = new PreviewScope();
            AK47 gun = scope.EquipRifle();
            var events = Trace(scope.Controller, gun);
            CheckListeners(gun, scope.Controller, 1);
            for (int i = 0; i < 3; i++)
            {
                Invoke(scope.Controller, "OnDisable");
                CheckListeners(gun, scope.Controller, 0);
                Invoke(scope.Controller, "OnEnable");
                CheckListeners(gun, scope.Controller, 1);
                Expect(events);
            }
            Advance(scope.Controller, 3, true);
            Expect(events, "Prepare", "PrepareEnd", "Shot");
            Require(scope.Holder.EquipWeapon(scope.Definition), "应能替换当前枪械实例。");
            Expect(events, "Prepare", "PrepareEnd", "Shot", "Canceled:Shooting");
            CheckListeners(gun, scope.Controller, 0);
            var next = (AK47)scope.Holder.CurrentWeapon;
            CheckListeners(next, scope.Controller, 1);
            Invoke(scope.Controller, "OnDisable");
            scope.Controller.enabled = false;
            CheckListeners(next, scope.Controller, 0);
            int count = events.Count;
            next.TryPrepare();
            next.Tick(100);
            Require(events.Count == count, "停用后不得继续转发事件。");
        }

        private static void TestControllerRoles()
        {
            foreach (PawnRole role in new[] { PawnRole.None, PawnRole.Predicted, PawnRole.Authoritative, PawnRole.Interpolated })
            {
                using var scope = new PreviewScope();
                AK47 gun = scope.EquipRifle();
                var events = Trace(scope.Controller, gun);
                scope.SetRole(role);
                Ready(gun);
                Require(gun.TryFire(), "隔离枪械本身仍可接受测试命令。");
                gun.Tick(12);
                Require(gun.TryReload(), "应进入换弹。");
                gun.Tick(15);
                Require(gun.TryFire(), "装填后应能射击。");
                gun.Unequip();
                if (role is PawnRole.Predicted or PawnRole.Authoritative)
                    Expect(events, "Prepare", "PrepareEnd", "Shot", "ShotCooldownEnd", "Reload", "ReloadEnd", "Shot", "Canceled:Shooting");
                else
                    Expect(events);
            }
        }

        private static List<string> Trace(Gun gun)
        {
            var events = new List<string>();
            gun.OnPrepare += () => events.Add("Prepare");
            gun.OnPrepareCompleted += () => events.Add("PrepareEnd");
            gun.OnShot += () => events.Add("Shot");
            gun.OnShotCooldownEnd += () => events.Add("ShotCooldownEnd");
            gun.OnReload += () => events.Add("Reload");
            gun.OnReloadCompleted += () => events.Add("ReloadEnd");
            gun.OnActionCanceled += state => events.Add("Canceled:" + state);
            return events;
        }

        private static List<string> Trace(PlayerWeaponController controller, Gun expected)
        {
            var events = new List<string>();
            void Add(Gun gun, string name)
            {
                Require(ReferenceEquals(gun, expected), "转发必须携带对应的实际枪械实例。");
                events.Add(name);
            }
            controller.OnPrepare += gun => Add(gun, "Prepare");
            controller.OnPrepareEnd += gun => Add(gun, "PrepareEnd");
            controller.OnShot += gun => Add(gun, "Shot");
            controller.OnShotCooldownEnd += gun => Add(gun, "ShotCooldownEnd");
            controller.OnReload += gun => Add(gun, "Reload");
            controller.OnReloadEnd += gun => Add(gun, "ReloadEnd");
            controller.OnActionCanceled += (gun, state) => Add(gun, "Canceled:" + state);
            return events;
        }

        private static void Advance(PlayerWeaponController controller, uint count, bool fire)
        {
            for (uint i = 0; i < count; i++) controller.StepLocal(fire);
        }

        private static void CheckListeners(Gun gun, PlayerWeaponController controller, int expected)
        {
            foreach (string name in GunEvents)
            {
                var handlers = (Delegate)typeof(Gun).GetField(name, PrivateInstance).GetValue(gun);
                int count = 0;
                if (handlers != null)
                    foreach (Delegate handler in handlers.GetInvocationList())
                        if (ReferenceEquals(handler.Target, controller)) count++;
                Require(count == expected, name + " 控制器订阅数量应为 " + expected + "，实际为 " + count);
            }
        }

        private static void Expect(List<string> actual, params string[] expected) => Require(
            string.Join(",", actual) == string.Join(",", expected),
            "事件顺序错误：期望 [" + string.Join(",", expected) + "]，实际 [" + string.Join(",", actual) + "]。");

        private static void Invoke(object target, string name) => target.GetType().GetMethod(name, PrivateInstance).Invoke(target, null);
        private static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }

        private static void RunCase(string name, Action test, StringBuilder report, ref int passed)
        {
            test();
            passed++;
            report.AppendLine("PASS " + name);
        }

        private sealed class PreviewScope : IDisposable
        {
            private readonly Scene _scene = EditorSceneManager.NewPreviewScene();
            public PlayerHolder Holder { get; }
            public PlayerBrain Brain { get; }
            public PlayerWeaponController Controller { get; }
            public AK47SO Definition { get; }

            public PreviewScope()
            {
                GameObject root = CreateRoot("WeaponLifecycleValidation");
                Holder = root.AddComponent<PlayerHolder>();
                Brain = root.AddComponent<PlayerBrain>();
                CreateRoot("wpn").transform.SetParent(root.transform, false);
                Controller = root.AddComponent<PlayerWeaponController>();
                SetRole(PawnRole.Predicted);
                Invoke(Controller, "OnDisable");
                Invoke(Controller, "OnEnable");
                var source = AssetDatabase.LoadAssetAtPath<AK47SO>("Assets/SO/GunData/Definitions/AK47SO.asset");
                Require(source != null, "真实 AK47SO 资源必须存在。");
                Definition = UnityEngine.Object.Instantiate(source);
                Definition.hideFlags = HideFlags.HideAndDontSave;
                var serialized = new SerializedObject(Definition);
                serialized.FindProperty("_initialTotalBullets").intValue = 20;
                serialized.FindProperty("_capacity").intValue = 4;
                serialized.FindProperty("_prepareTicks").intValue = 2;
                serialized.FindProperty("_shotIntervalTicks").intValue = 10;
                serialized.FindProperty("_reloadTicks").intValue = 3;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }

            public AK47 EquipRifle()
            {
                Require(Holder.EquipWeapon(Definition), "应能通过 SO 装备真实 AK47 控制器预制体。");
                return (AK47)Holder.CurrentWeapon;
            }

            public void SetRole(PawnRole role) => typeof(PlayerBrain).GetField("<Role>k__BackingField", PrivateInstance).SetValue(Brain, role);

            private GameObject CreateRoot(string name)
            {
                GameObject root = EditorUtility.CreateGameObjectWithHideFlags(name, HideFlags.HideAndDontSave);
                SceneManager.MoveGameObjectToScene(root, _scene);
                return root;
            }

            public void Dispose()
            {
                try { if (Controller != null) Invoke(Controller, "OnDisable"); }
                finally
                {
                    if (_scene.IsValid()) EditorSceneManager.ClosePreviewScene(_scene);
                    if (Definition != null) UnityEngine.Object.DestroyImmediate(Definition);
                }
            }
        }
    }
}
