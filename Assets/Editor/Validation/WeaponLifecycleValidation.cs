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
                var source = AssetDatabase.LoadAssetAtPath<AK47SO>("Assets/Scripts/AK47SO.asset");
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
