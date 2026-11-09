using System;
using System.Reflection;
using System.Text;
using Core.Input;
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
    /// <summary>在隔离场景中验证本地射击命令，不启动网络、不修改配置资源。</summary>
    public static class LocalWeaponValidation
    {
        private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
        private static bool _running;

        [CliCommand("local_weapon_validate", "Validate predicted weapon commands, cadence and input consumption", Tags = new[] { "fpsgame/validation" })]
        public static string Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return "REFUSED：请先退出 Play Mode，再运行隔离射击验证。";
            if (_running)
                return "REFUSED：本地射击验证已经在执行。";

            _running = true;
            var report = new StringBuilder();
            int passed = 0;
            try
            {
                RunCase("真实 SO 的准备时间、连射间隔和成功事件一致", TestPreparationAndCadence, report, ref passed);
                RunCase("松开停止新射击，空弹不消耗备弹或发送成功事件", TestReleaseAndEmpty, report, ref passed);
                RunCase("切刀解绑旧枪，切回保留弹药并重新准备", TestSwitching, report, ref passed);
                RunCase("服务器、远端与未绑定身份不执行本地命令", TestRoleIsolation, report, ref passed);
                RunCase("组件启停保持时钟且不会累积射击订阅", TestLifecycle, report, ref passed);
                RunCase("移动预测重置与旧运动结果不会重射或重置武器时钟", TestPredictionIsolation, report, ref passed);
                RunCase("短点击只消费一次，按住跨多拍同步进入预测和网络输入", TestLocalInput, report, ref passed);
                RunCase("预测输入重置清除积压的本地开火", TestInputReset, report, ref passed);
                return "PASS " + passed + " checks\n" + report;
            }
            finally
            {
                _running = false;
            }
        }

        private static void TestPreparationAndCadence(PreviewScope scope)
        {
            AK47 gun = scope.EquipRifle();
            int initial = gun.Magazine.Rounds;
            Require(initial >= 2, "当前 SO 需至少两发弹药以验证连续两次射击。");
            int shots = 0;
            scope.Controller.OnShot += emitted =>
            {
                Require(ReferenceEquals(emitted, gun), "成功事件必须携带装备栏里的实际枪实例。");
                shots++;
            };

            scope.Controller.StepLocal(true);
            Require(gun.GunState == GunState.Preparing && shots == 0 && gun.Magazine.Rounds == initial,
                "第一拍应开始准备，不能直接开火。");
            Advance(scope.Controller, gun.Config.PrepareTicks - 1, true);
            Require(shots == 0 && gun.Magazine.Rounds == initial, "准备结束前不得发射。");
            scope.Controller.StepLocal(true);
            Require(shots == 1 && gun.Magazine.Rounds == initial - 1, "准备结束当拍应成功发射并且只扣一发。");
            Advance(scope.Controller, gun.Config.ShotIntervalTicks - 1, true);
            Require(shots == 1 && gun.Magazine.Rounds == initial - 1, "冷却期间按住不能提前开火。");
            scope.Controller.StepLocal(true);
            Require(shots == 2 && gun.Magazine.Rounds == initial - 2, "配置的间隔到期后应恰好发出第二发。");
        }

        private static void TestReleaseAndEmpty(PreviewScope scope)
        {
            AK47 gun = scope.EquipRifle();
            int initial = gun.Magazine.Rounds;
            int reserve = gun.Magazine.ReserveRounds;
            int shots = 0;
            scope.Controller.OnShot += _ => shots++;
            Advance(scope.Controller, gun.Config.PrepareTicks + 1, true);
            int afterFirst = gun.Magazine.Rounds;
            Require(afterFirst == initial - 1 && shots == 1, "验证开始前应已发射一发。");

            Advance(scope.Controller, gun.Config.ShotIntervalTicks * 2, false);
            Require(gun.Magazine.Rounds == afterFirst && shots == 1, "松开左键后不能继续射击。");
            Advance(scope.Controller, checked((uint)initial * gun.Config.ShotIntervalTicks + 1), true);
            Require(gun.Magazine.Empty && shots == initial && gun.Magazine.ReserveRounds == reserve,
                "连射只能消耗弹匣，弹匣耗尽后不得自动装填。");
            Advance(scope.Controller, gun.Config.ShotIntervalTicks * 2, true);
            Require(shots == initial && gun.Magazine.Empty, "空弹请求不能产生新的成功射击事件。");
        }

        private static void TestSwitching(PreviewScope scope)
        {
            AK47 gun = scope.EquipRifle();
            int shots = 0;
            scope.Controller.OnShot += _ => shots++;
            Advance(scope.Controller, gun.Config.PrepareTicks + 1, true);
            int rounds = gun.Magazine.Rounds;
            Require(scope.Holder.SelectSlot(WeaponSlot.Knife), "应能切换到默认刀。");
            Require(gun.GunState == GunState.None && ShotListeners(gun, scope.Controller) == 0,
                "收起枪械必须取消动作并解绑其射击事件。");
            Advance(scope.Controller, gun.Config.PrepareTicks + gun.Config.ShotIntervalTicks, true);
            Require(gun.Magazine.Rounds == rounds && shots == 1, "持刀期间不能继续驱动旧枪。");

            Require(scope.Holder.SelectSlot(WeaponSlot.Rifle) && ReferenceEquals(scope.Holder.CurrentWeapon, gun),
                "切回长枪必须复用已有实例。");
            Require(ShotListeners(gun, scope.Controller) == 1, "切回时只应绑定一次射击监听。");
            scope.Controller.StepLocal(true);
            Advance(scope.Controller, gun.Config.PrepareTicks - 1, true);
            Require(gun.Magazine.Rounds == rounds && shots == 1 && gun.GunState == GunState.Preparing,
                "切回后必须重新完成准备，不能立即发射。");
            scope.Controller.StepLocal(true);
            Require(gun.Magazine.Rounds == rounds - 1 && shots == 2, "重新准备结束后应继续使用剩余弹药射击。");
        }

        private static void TestRoleIsolation(PreviewScope scope)
        {
            AK47 gun = scope.EquipRifle();
            int rounds = gun.Magazine.Rounds;
            int shots = 0;
            scope.Controller.OnShot += _ => shots++;
            foreach (PawnRole role in new[] { PawnRole.None, PawnRole.Authoritative, PawnRole.Interpolated })
            {
                scope.SetRole(role);
                Advance(scope.Controller, gun.Config.PrepareTicks + gun.Config.ShotIntervalTicks + 1, true);
                Require(scope.Controller.LocalTick == 0 && gun.GunState == GunState.None &&
                        gun.Magazine.Rounds == rounds && shots == 0,
                    role + " 不得推进本地枪械或消耗弹药。");
            }
            scope.SetRole(PawnRole.Predicted);
            Advance(scope.Controller, gun.Config.PrepareTicks + 1, true);
            Require(shots == 1 && gun.Magazine.Rounds == rounds - 1, "切回本地预测身份后才可执行射击。");
        }

        private static void TestLifecycle(PreviewScope scope)
        {
            AK47 gun = scope.EquipRifle();
            int shots = 0;
            scope.Controller.OnShot += _ => shots++;
            Advance(scope.Controller, gun.Config.PrepareTicks + 1, true);
            uint tick = scope.Controller.LocalTick;
            int rounds = gun.Magazine.Rounds;

            Invoke(scope.Controller, "OnDisable");
            scope.Controller.enabled = false;
            Require(ShotListeners(gun, scope.Controller) == 0, "停用时应退订当前枪。");
            Advance(scope.Controller, gun.Config.ShotIntervalTicks + 1, true);
            Require(scope.Controller.LocalTick == tick && gun.Magazine.Rounds == rounds && shots == 1,
                "停用组件不能执行命令。");

            scope.Controller.enabled = true;
            // 显式成对调用，编辑模式不依赖 MonoBehaviour 的运行时生命周期。
            Invoke(scope.Controller, "OnDisable");
            for (int i = 0; i < 3; i++)
            {
                Invoke(scope.Controller, "OnEnable");
                Require(ShotListeners(gun, scope.Controller) == 1, "重新启用只能有一个射击订阅。");
                if (i < 2) Invoke(scope.Controller, "OnDisable");
            }
            Require(scope.Controller.LocalTick == tick, "组件启停不能重置武器 Tick。");
            Advance(scope.Controller, gun.Config.ShotIntervalTicks, true);
            Require(gun.Magazine.Rounds == rounds - 1 && shots == 2,
                "多次启停后下一发仍应只转发一次成功事件。");
        }

        private static void TestPredictionIsolation(PreviewScope scope)
        {
            AK47 gun = scope.EquipRifle();
            int shots = 0;
            scope.Controller.OnShot += _ => shots++;
            Advance(scope.Controller, gun.Config.PrepareTicks + 1, true);
            uint tick = scope.Controller.LocalTick;
            int rounds = gun.Magazine.Rounds;
            PlayerPrediction prediction = scope.Root.AddComponent<PlayerPrediction>();
            prediction.ResetPrediction();
            PlayerMotionController motion = scope.Root.AddComponent<PlayerMotionController>();
            Invoke(motion, "OnEnable");
            try { motion.Present(new PlayerStepResult { fired = true }); }
            finally { Invoke(motion, "OnDisable"); }
            Require(scope.Controller.LocalTick == tick && gun.Magazine.Rounds == rounds && shots == 1,
                "移动预测重置和旧 fired 结果都不能推进本地枪械或重新播放射击。");
            Advance(scope.Controller, gun.Config.ShotIntervalTicks, true);
            Require(scope.Controller.LocalTick == tick + gun.Config.ShotIntervalTicks &&
                    gun.Magazine.Rounds == rounds - 1 && shots == 2,
                "预测重置后独立武器时钟必须继续推进并允许下一次射击。");
        }

        private static void TestLocalInput(PreviewScope scope)
        {
            PlayerInputDriver input = scope.CreateInputDriver();
            input.Consume(0); // 丢弃连接前输入的首拍。
            Accumulate(input, true);
            Accumulate(input, false);
            Require(input.Consume(1).fire && !input.Consume(2).fire,
                "同一拍前已经松开的短点击必须进入同一份预测和网络输入，并只消费一次。");

            Accumulate(input, true);
            for (uint tick = 3; tick < 7; tick++)
                Require(input.Consume(tick).fire,
                    "同一渲染帧跨多个模拟 Tick 时，预测和网络输入必须持续保持按住状态。");
            Accumulate(input, false);
            Require(!input.Consume(7).fire, "松开后不能残留按住状态。");
        }

        private static void TestInputReset(PreviewScope scope)
        {
            PlayerInputDriver input = scope.CreateInputDriver();
            input.Consume(0);
            Accumulate(input, true);
            input.ResetPendingInput();
            Require(!input.Consume(1).fire && !input.Consume(2).fire, "预测重置必须同时清空按住和待消费点击。");
            Accumulate(input, true);
            Require(input.Consume(3).fire, "重置后新收到的左键输入仍应正常工作。");
        }

        private static void Accumulate(PlayerInputDriver input, bool held) =>
            Invoke(input, "Accumulate", new PlayerInput { lMouse = held });

        private static void Advance(PlayerWeaponController controller, uint count, bool fire)
        {
            Require(count <= 100000, "隔离验证拒绝过大的模拟拍数。");
            for (uint i = 0; i < count; i++) controller.StepLocal(fire);
        }

        private static int ShotListeners(Gun gun, PlayerWeaponController controller)
        {
            var handlers = (Delegate)typeof(Gun).GetField("OnShot", PrivateInstance).GetValue(gun);
            if (handlers == null) return 0;
            int count = 0;
            foreach (Delegate handler in handlers.GetInvocationList())
                if (ReferenceEquals(handler.Target, controller)) count++;
            return count;
        }

        private static void Invoke(object target, string name, params object[] arguments)
        {
            MethodInfo method = target.GetType().GetMethod(name, PrivateInstance);
            Require(method != null, "缺少隔离验证入口 " + name);
            method.Invoke(target, arguments);
        }

        private static void RunCase(string name, Action<PreviewScope> test, StringBuilder report, ref int passed)
        {
            using (var scope = new PreviewScope()) test(scope);
            passed++;
            report.AppendLine("PASS " + name);
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }

        private sealed class PreviewScope : IDisposable
        {
            private readonly Scene _scene = EditorSceneManager.NewPreviewScene();
            public GameObject Root { get; }
            public PlayerHolder Holder { get; }
            public PlayerBrain Brain { get; }
            public PlayerWeaponController Controller { get; }

            public PreviewScope()
            {
                Root = CreateRoot("LocalWeaponValidation");
                Holder = Root.AddComponent<PlayerHolder>();
                Brain = Root.AddComponent<PlayerBrain>();
                GameObject mount = CreateRoot("wpn");
                mount.transform.SetParent(Root.transform, false);
                Controller = Root.AddComponent<PlayerWeaponController>();
                SetRole(PawnRole.Predicted);
                Invoke(Controller, "OnDisable");
                Invoke(Controller, "OnEnable");
            }

            public AK47 EquipRifle()
            {
                var definition = AssetDatabase.LoadAssetAtPath<AK47SO>("Assets/Scripts/AK47SO.asset");
                Require(definition != null && Holder.EquipWeapon(definition), "真实 AK47SO 应能创建并装备当前枪械。");
                var gun = Holder.CurrentWeapon as AK47;
                Require(gun != null && gun.Magazine.Rounds > 0, "真实 SO 必须创建可用于射击验证的 AK47。");
                return gun;
            }

            public void SetRole(PawnRole role)
            {
                // 只设置身份，不启动网络、运动或预测组件。
                typeof(PlayerBrain).GetField("<Role>k__BackingField", PrivateInstance).SetValue(Brain, role);
            }

            public PlayerInputDriver CreateInputDriver()
            {
                GameObject root = CreateRoot("LocalInputValidation");
                root.SetActive(false); // 不运行 Awake，避免替换场景中的输入单例。
                return root.AddComponent<PlayerInputDriver>();
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
                    if (Controller != null) Invoke(Controller, "OnDisable");
                }
                finally
                {
                    if (_scene.IsValid()) EditorSceneManager.ClosePreviewScene(_scene);
                }
            }
        }
    }
}
