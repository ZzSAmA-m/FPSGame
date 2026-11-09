using System;
using System.Reflection;
using System.Text;
using GamePlay.HoldSystem;
using GamePlay.Player;
using GamePlay.Player.Simulation;
using GamePlay.Player.Simulation.Reconciliation;
using Unity.Pipeline.Commands;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FPSGame.Editor.Validation
{
    /// <summary>用真实角色运动与枪械验证实时输入门面的顺序、身份和回放边界。</summary>
    public static class PlayerInputFacadeValidation
    {
        private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
        private static bool _running;

        [CliCommand("player_input_facade_validate", "Validate player live-input facade, ordering and replay isolation", Tags = new[] { "fpsgame/validation" })]
        public static string Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return "REFUSED：请先退出 Play Mode，再运行隔离玩家输入门面验证。";
            if (_running) return "REFUSED：玩家输入门面验证已经在执行。";
            _running = true;
            var report = new StringBuilder();
            int passed = 0;
            try
            {
                RunCase("两种实时身份各执行一次运动及武器步进，成功结果服从准备、射速和弹药", TestLiveSteps, report, ref passed);
                RunCase("同拍切刀先于开火，重新切枪必须完成准备", TestSwitchBeforeFire, report, ref passed);
                RunCase("错误身份在运动、切栏、跳跃和枪械副作用前拒绝", TestRoleGuards, report, ref passed);
                RunCase("运动重放模式拒绝实时门面，且没有副作用", TestReplayGuards, report, ref passed);
                RunCase("真实接地起跳发布一次，空中再次请求不重复发布", TestGroundedJump, report, ref passed);
                RunCase("真实移动恢复重放不发布跳跃、不切栏也不重扣弹", TestMovementReplay, report, ref passed);
                RunCase("成功结果使用服务器模拟后的射线和独立状态值拷贝", TestAuthoritativeResult, report, ref passed);
                return "PASS " + passed + " checks\n" + report;
            }
            finally { _running = false; }
        }

        private static void TestLiveSteps()
        {
            foreach (PawnRole role in new[] { PawnRole.Predicted, PawnRole.Authoritative })
            {
                using var scope = new PreviewScope(role);
                AK47 gun = scope.EquipRifle();
                int initial = gun.Magazine.Rounds;
                int localShots = 0;
                scope.Weapon.OnShot += _ => localShots++;
                PlayerLiveStepResult result = scope.Step(new PlayerInputFrame
                {
                    tick = 1, fire = true, look = new Vector2(2f, -3f)
                });
                Require(!result.Fired && result.State.yaw == 2f && result.State.pitch == 3f &&
                        result.FireOrigin == Vector3.zero && result.FireDirection == Vector3.zero,
                    role + " 首拍应只积分一次视角并开始准备，不把原始 Fire 意图当作已发射。");
                CheckTick(scope, 1);
                for (uint tick = 2; tick <= gun.Config.PrepareTicks; tick++)
                    Require(!scope.Step(new PlayerInputFrame { tick = tick, fire = true }).Fired,
                        role + " 准备结束前不能返回开火成功。");
                uint next = gun.Config.PrepareTicks + 1;
                Require(scope.Step(new PlayerInputFrame { tick = next++, fire = true }).Fired && gun.Magazine.Rounds == initial - 1,
                    role + " 准备结束当拍应恰好发射并扣除一发。");
                int successes = 1;
                for (uint i = 0; i < (uint)initial * gun.Config.ShotIntervalTicks + 1; i++)
                {
                    PlayerLiveStepResult step = scope.Step(new PlayerInputFrame { tick = next++, fire = true });
                    if (step.Fired) successes++;
                    else Require(step.FireOrigin == Vector3.zero && step.FireDirection == Vector3.zero,
                        "未发射的步进不能暴露上一发遗留的射线。");
                }
                Require(successes == initial && gun.Magazine.Empty,
                    role + " 连发只能在射速允许时成功，空弹后不再报告成功。");
                Require(localShots == initial,
                    "预测和权威步进均应每发只转发一次枪械生命周期，表现消费者按各自视角选择动画。");
                CheckTick(scope, next - 1);
                Require(scope.Brain.State.Equals(scope.Simulation.State), "公共 State 必须代表该门面的运动模拟状态。");
            }
        }

        private static void TestSwitchBeforeFire()
        {
            foreach (PawnRole role in new[] { PawnRole.Predicted, PawnRole.Authoritative })
            {
                using var scope = new PreviewScope(role);
                AK47 gun = scope.EquipRifle();
                for (uint tick = 1; tick <= gun.Config.PrepareTicks + 1; tick++)
                    scope.Step(new PlayerInputFrame { tick = tick, fire = true });
                int rounds = gun.Magazine.Rounds;
                uint next = gun.Config.PrepareTicks + 2;
                PlayerLiveStepResult knife = scope.Step(new PlayerInputFrame { tick = next++, key3 = true, fire = true });
                Require(!knife.Fired && scope.Holder.CurrentSlot == WeaponSlot.Knife &&
                        gun.GunState == GunState.None && gun.Magazine.Rounds == rounds,
                    "同拍切刀必须先取消旧枪动作，Fire 不得继续驱动旧枪。");
                PlayerLiveStepResult rifle = scope.Step(new PlayerInputFrame { tick = next++, key1 = true, fire = true });
                Require(!rifle.Fired && ReferenceEquals(scope.Holder.CurrentWeapon, gun) &&
                        gun.GunState == GunState.Preparing && gun.Magazine.Rounds == rounds,
                    "同拍切回枪械应对当前枪开始准备，不能绕过准备直接射击。");
                for (uint i = 1; i < gun.Config.PrepareTicks; i++)
                    Require(!scope.Step(new PlayerInputFrame { tick = next++, fire = true }).Fired,
                        "重新切枪也必须等待完整准备时间。");
                Require(scope.Step(new PlayerInputFrame { tick = next, fire = true }).Fired && gun.Magazine.Rounds == rounds - 1,
                    "重新准备完成后应对保留的实际枪实例发射一次。");
            }
        }

        private static void TestRoleGuards()
        {
            foreach (bool server in new[] { false, true })
            foreach (PawnRole role in new[] { PawnRole.None, PawnRole.Predicted, PawnRole.Authoritative, PawnRole.Interpolated })
            {
                if (role == (server ? PawnRole.Authoritative : PawnRole.Predicted)) continue;
                using var scope = new PreviewScope(role);
                AK47 gun = scope.EquipRifle();
                PlayerSimState before = scope.Simulation.State;
                int changes = 0;
                int jumps = 0;
                scope.Holder.OnWeaponChanged += () => changes++;
                scope.Motion.OnJumped += () => jumps++;
                var frame = new PlayerInputFrame
                {
                    tick = 1, move = Vector2.one, look = Vector2.one,
                    jump = true, fire = true, key3 = true
                };
                ExpectInvalid(() =>
                {
                    if (server) scope.Brain.StepServer(frame);
                    else scope.Brain.StepPredicted(frame);
                });
                Require(scope.Simulation.State.Equals(before) && ReferenceEquals(scope.Holder.CurrentWeapon, gun) &&
                        gun.GunState == GunState.None && scope.Weapon.LocalTick == 0 && scope.Weapon.ServerTick == 0 &&
                        changes == 0 && jumps == 0,
                    role + " 身份错误时必须在所有输入副作用之前拒绝实时入口。");
            }
        }

        private static void TestReplayGuards()
        {
            foreach (PawnRole role in new[] { PawnRole.Predicted, PawnRole.Authoritative })
            {
                using var scope = new PreviewScope(role);
                AK47 gun = scope.EquipRifle();
                PlayerSimState before = scope.Simulation.State;
                int jumps = 0;
                scope.Motion.OnJumped += () => jumps++;
                scope.Simulation.SetReplayMode(true);
                try
                {
                    ExpectInvalid(() => scope.Step(new PlayerInputFrame
                    {
                        tick = 1, move = Vector2.one, look = Vector2.one, key3 = true, jump = true, fire = true
                    }));
                    Require(scope.Simulation.State.Equals(before) && ReferenceEquals(scope.Holder.CurrentWeapon, gun) &&
                            gun.GunState == GunState.None && scope.Weapon.LocalTick == 0 && scope.Weapon.ServerTick == 0 && jumps == 0,
                        "即使身份正确，重放期间也必须先拒绝实时门面且不修改任何状态。");
                }
                finally { scope.Simulation.SetReplayMode(false); }
            }
        }

        private static void TestGroundedJump()
        {
            foreach (PawnRole role in new[] { PawnRole.Predicted, PawnRole.Authoritative })
            {
                using var scope = new PreviewScope(role);
                scope.Ground();
                int jumps = 0;
                scope.Motion.OnJumped += () => jumps++;
                PlayerLiveStepResult first = scope.Step(new PlayerInputFrame { tick = 1, jump = true });
                Require(jumps == 1 && first.State.verticalVelocity > 0f && !first.State.grounded,
                    "真实接地的起跳成功结果必须通过动作层发布一次跳跃事件。");
                scope.Step(new PlayerInputFrame { tick = 2, jump = true });
                Require(jumps == 1, "空中再次请求跳跃不能产生第二次成功跳跃事件。");
            }
        }

        private static void TestMovementReplay()
        {
            using var scope = new PreviewScope(PawnRole.Predicted);
            AK47 gun = scope.EquipRifle();
            int shots = 0;
            int jumps = 0;
            scope.Weapon.OnShot += _ => shots++;
            scope.Motion.OnJumped += () => jumps++;
            for (uint tick = 1; tick <= gun.Config.PrepareTicks + 1; tick++)
                scope.Step(new PlayerInputFrame { tick = tick, fire = true });
            scope.Ground();
            PlayerSimState baseline = scope.Simulation.State;
            var history = new PredictionHistory(8);
            history.Seed(100, baseline);
            var first = new PlayerInputFrame { tick = 101, jump = true, fire = true, key3 = true };
            var second = new PlayerInputFrame { tick = 102, fire = true, key1 = true };
            scope.Step(first);
            history.Record(first, scope.Brain.State);
            scope.Step(second);
            history.Record(second, scope.Brain.State);
            Require(jumps == 1 && shots == 1 && ReferenceEquals(scope.Holder.CurrentWeapon, gun),
                "回放前的真实输入必须已产生一次跳跃、一次射击并完成切刀切枪。");
            int changes = 0;
            scope.Holder.OnWeaponChanged += () => changes++;
            uint weaponTick = scope.Weapon.LocalTick;
            int rounds = gun.Magazine.Rounds;
            GunState gunState = gun.GunState;
            baseline.position += Vector3.right;
            var reconciler = new PlayerReconciler(scope.Simulation, history, new ReconciliationSettings());
            scope.Simulation.SetReplayMode(true);
            ReconcileResult result;
            try
            {
                result = reconciler.Reconcile(new PlayerAuthoritativeSnapshot
                {
                    Epoch = 2, ServerTick = 200, StateInputTick = 100, State = baseline
                }, 102);
            }
            finally { scope.Simulation.SetReplayMode(false); }
            Require(result.Status == ReconcileStatus.Replayed && result.ReplayedTicks == 2,
                "必须由真实 Reconciler 恢复状态并执行两拍包含跳跃、切栏和 Fire 的运动输入。");
            Require(jumps == 1 && shots == 1 && changes == 0 && scope.Weapon.LocalTick == weaponTick &&
                    gun.Magazine.Rounds == rounds && gun.GunState == gunState && ReferenceEquals(scope.Holder.CurrentWeapon, gun),
                "运动回放只能修正运动，不能重播跳跃、重新切栏、推进枪械或再次扣弹。");
        }

        private static void TestAuthoritativeResult()
        {
            using var scope = new PreviewScope(PawnRole.Authoritative);
            AK47 gun = scope.EquipRifle();
            for (uint tick = 1; tick <= gun.Config.PrepareTicks; tick++)
                Require(!scope.Step(new PlayerInputFrame { tick = tick }).Fired, "无开火输入不得发射。");
            PlayerLiveStepResult result = scope.Step(new PlayerInputFrame
            {
                tick = gun.Config.PrepareTicks + 1, fire = true, look = new Vector2(30f, -15f), move = Vector2.up
            });
            Vector3 expected = Quaternion.Euler(result.State.pitch, result.State.yaw, 0f) * Vector3.forward;
            Require(result.Fired && result.State.yaw == 30f && result.State.pitch == 15f &&
                    Vector3.Distance(result.FireOrigin, scope.Sight.View.position) < 0.0001f &&
                    Vector3.Distance(result.FireDirection, expected) < 0.0001f &&
                    result.State.Equals(scope.Simulation.State),
                "射线必须来自该拍完成后的服务器状态和 Sight 挂点，返回状态应为同拍结果。");
            PlayerSimState detached = result.State;
            detached.position += Vector3.one * 100f;
            Require(!detached.position.Equals(scope.Brain.State.position) && result.State.Equals(scope.Brain.State),
                "调用方修改返回状态副本不能修改角色内部的权威状态。");
            Set(scope.Sight, "_view", null);
            for (uint i = 1; i < gun.Config.ShotIntervalTicks; i++)
                scope.Step(new PlayerInputFrame { tick = gun.Config.PrepareTicks + 1 + i });
            PlayerLiveStepResult fallback = scope.Step(new PlayerInputFrame
            {
                tick = gun.Config.PrepareTicks + gun.Config.ShotIntervalTicks + 1, fire = true
            });
            Require(fallback.Fired && fallback.FireOrigin == fallback.State.position,
                "没有 Sight 挂点时应按约定使用模拟位置作为开火原点。");
        }

        private static void CheckTick(PreviewScope scope, uint expected)
        {
            Require(scope.Weapon.LocalTick == (scope.Role == PawnRole.Predicted ? expected : 0) &&
                    scope.Weapon.ServerTick == (scope.Role == PawnRole.Authoritative ? expected : 0),
                "每个实时门面调用只能推进所属身份的武器时钟一次。");
        }

        private static void ExpectInvalid(Action action)
        {
            try { action(); }
            catch (InvalidOperationException) { return; }
            throw new InvalidOperationException("该实时输入调用应在修改状态前抛出 InvalidOperationException。");
        }

        private static void Set(object target, string name, object value) => target.GetType()
            .GetField(name, PrivateInstance).SetValue(target, value);

        private static void Bind(PlayerBrain brain, string name, object value) =>
            Set(brain, "<" + name + ">k__BackingField", value);

        private static void Invoke(object target, string name) => target.GetType()
            .GetMethod(name, PrivateInstance).Invoke(target, null);

        private static void Require(bool value, string message)
        {
            if (!value) throw new InvalidOperationException(message);
        }

        private static void RunCase(string name, Action test, StringBuilder report, ref int passed)
        {
            test();
            passed++;
            report.AppendLine("PASS " + name);
        }

        private sealed class PreviewScope : IDisposable
        {
            private readonly Scene _scene = EditorSceneManager.NewPreviewScene();
            private readonly AK47SO _definition;
            private readonly PlayerMovementDataSO _movement;
            public PawnRole Role { get; }
            public GameObject Root { get; }
            public PlayerBrain Brain { get; }
            public PlayerHolder Holder { get; }
            public PlayerSightController Sight { get; }
            public PlayerSimulation Simulation { get; }
            public PlayerMotionController Motion { get; }
            public PlayerWeaponController Weapon { get; }

            public PreviewScope(PawnRole role)
            {
                Role = role;
                Root = CreateRoot("PlayerInputFacadeValidation");
                Root.transform.position = new Vector3(10f, 2f, 20f);
                Holder = Root.AddComponent<PlayerHolder>();
                Brain = Root.AddComponent<PlayerBrain>();
                CreateRoot("wpn").transform.SetParent(Root.transform, false);
                Sight = Root.AddComponent<PlayerSightController>();
                Transform view = CreateRoot("View").transform;
                view.SetParent(Root.transform, false);
                view.localPosition = new Vector3(0f, 1.65f, 0.1f);
                Set(Sight, "_view", view);
                Sight.Sensitivity = 1f;
                Simulation = Root.AddComponent<PlayerSimulation>();
                _movement = ScriptableObject.CreateInstance<PlayerMovementDataSO>();
                _movement.hideFlags = HideFlags.HideAndDontSave;
                Set(_movement, "_moveSpeed", 6f);
                Set(_movement, "_jumpSpeed", 5f);
                Set(Simulation, "_playerMovementDataSO", _movement);
                Set(Simulation, "_gravityScale", 0f);
                Invoke(Simulation, "Awake");
                Simulation.CaptureFromBody();
                Weapon = Root.AddComponent<PlayerWeaponController>();
                Motion = Root.AddComponent<PlayerMotionController>();
                Bind(Brain, "Role", role);
                Bind(Brain, "Simulation", Simulation);
                Bind(Brain, "Sight", Sight);
                Bind(Brain, "Motion", Motion);
                Bind(Brain, "Weapon", Weapon);
                Invoke(Weapon, "OnDisable");
                Invoke(Weapon, "OnEnable");
                Invoke(Motion, "OnDisable");
                Invoke(Motion, "OnEnable");
                AK47SO source = AssetDatabase.LoadAssetAtPath<AK47SO>("Assets/Scripts/AK47SO.asset");
                Require(source != null, "真实 AK47SO 必须存在。");
                _definition = UnityEngine.Object.Instantiate(source);
                _definition.hideFlags = HideFlags.HideAndDontSave;
                var serialized = new SerializedObject(_definition);
                serialized.FindProperty("_initialTotalBullets").intValue = 20;
                serialized.FindProperty("_capacity").intValue = 4;
                serialized.FindProperty("_prepareTicks").intValue = 2;
                serialized.FindProperty("_shotIntervalTicks").intValue = 3;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }

            public AK47 EquipRifle()
            {
                Require(Holder.EquipWeapon(_definition), "测试应通过真实 SO 装备枪械和控制器预制体。");
                return (AK47)Holder.CurrentWeapon;
            }

            public PlayerLiveStepResult Step(in PlayerInputFrame frame) => Role == PawnRole.Authoritative
                ? Brain.StepServer(frame) : Brain.StepPredicted(frame);

            public void Ground()
            {
                GameObject ground = CreateRoot("FacadeValidationGround");
                ground.transform.position = new Vector3(10f, -0.5f, 20f);
                BoxCollider floor = ground.AddComponent<BoxCollider>();
                floor.size = new Vector3(30f, 1f, 30f);
                CharacterController body = Root.GetComponent<CharacterController>();
                body.enabled = false;
                Root.transform.position = new Vector3(10f, 1.1f, 20f);
                body.enabled = true;
                Physics.SyncTransforms();
                body.Move(Vector3.down * 0.5f);
                Require(body.isGrounded, "隔离地面必须与真实 CharacterController 建立接地接触。");
                Simulation.CaptureFromBody();
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
                    if (Motion != null) Invoke(Motion, "OnDisable");
                    if (Weapon != null) Invoke(Weapon, "OnDisable");
                }
                finally
                {
                    if (_scene.IsValid()) EditorSceneManager.ClosePreviewScene(_scene);
                    if (_definition != null) UnityEngine.Object.DestroyImmediate(_definition);
                    if (_movement != null) UnityEngine.Object.DestroyImmediate(_movement);
                }
            }
        }
    }
}
