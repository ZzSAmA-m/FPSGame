using System;
using System.Reflection;
using System.Text;
using GamePlay.Physics;
using GamePlay.Player;
using GamePlay.Player.Simulation;
using Unity.Pipeline.Commands;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FPSGame.Editor.Validation
{
    /// <summary>在隔离场景中验证移动组件、模拟状态和回放视角的边界。</summary>
    public static class PlayerMovementValidation
    {
        private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
        private const float PositionTolerance = 0.0005f;
        private const float VelocityTolerance = 0.005f;
        private static bool _running;

        [CliCommand("player_movement_validate", "Validate isolated player movement, state restoration and replay view", Tags = new[] { "fpsgame/validation" })]
        public static string Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return "REFUSED：请先退出 Play Mode，再运行隔离玩家移动验证。";
            if (_running) return "REFUSED：玩家移动验证已经在执行。";
            _running = true;
            var report = new StringBuilder();
            int passed = 0;
            try
            {
                RunCase("移动组件独立接收输入、状态、视角配置和步长", TestStandaloneMovement, report, ref passed);
                RunCase("同拍转向后立即沿新朝向移动", TestTurnAndMove, report, ref passed);
                RunCase("地面首拍速度 0.55，连续加速达到 6", TestGroundAcceleration, report, ref passed);
                RunCase("地面摩擦按高速、停止速度和归零三种情况减速", TestGroundFriction, report, ref passed);
                RunCase("反向输入先制动再反向，不瞬间切换满速", TestCounterStrafe, report, ref passed);
                RunCase("转向保留垂直于期望方向的惯性，不截断总速", TestTurnInertia, report, ref passed);
                RunCase("对角线归一化，模拟量输入保留强度", TestDiagonalSpeed, report, ref passed);
                RunCase("真实地面和墙体碰撞回写实际位置、速度和接地", TestCollisionState, report, ref passed);
                RunCase("完整恢复非零水平速度并从恢复惯性继续步进", TestRestoreAndContinue, report, ref passed);
                RunCase("非零惯性的快照恢复重放与连续地面步进一致", TestReplayConsistency, report, ref passed);
                RunCase("恢复保留物理启停状态，重新启用不重置模拟状态", TestPhysicalState, report, ref passed);
                RunCase("空中及起跳成功当拍保留原有水平移动规则", TestAirCompatibility, report, ref passed);
                RunCase("回放更新物理朝向，结束后统一提交最终视角", TestReplayView, report, ref passed);
                RunCase("每拍读取更新后的移动 SO 和视角配置", TestLiveConfiguration, report, ref passed);
                RunCase("玩家预制体保留移动 SO、重力倍率和接地力", TestPrefabConfiguration, report, ref passed);
                return "PASS " + passed + " checks\n" + report;
            }
            finally { _running = false; }
        }

        private static void TestStandaloneMovement()
        {
            using var scope = new PreviewScope(false);
            Require(scope.Root.GetComponent<PlayerSightController>() == null &&
                    scope.Root.GetComponent<PlayerSimulation>() == null,
                "独立移动组件不应依赖 Sight 或 Simulation 组件才能执行。");
            var state = new PlayerSimState();
            scope.Movement.CaptureFromBody(ref state);
            PlayerMotorConfig config = scope.Movement.BuildConfig(2f, -15f, 15f);
            var input = new PlayerInputFrame { move = Vector2.up, look = new Vector2(45f, -20f) };
            const float dt = 0.125f;
            scope.Movement.Step(ref state, input, config, dt);
            Require(Near(state.position, PreviewScope.Origin + Vector3.right * (4.125f * dt)) &&
                    Near(Horizontal(state.velocity), Vector3.right * 4.125f, VelocityTolerance) &&
                    Near(state.yaw, 90f) && Near(state.pitch, 15f),
                "移动必须使用注入的输入、灵敏度、俯仰限制及步长，并写回调用方状态。");
        }

        private static void TestTurnAndMove()
        {
            using var scope = new PreviewScope();
            scope.Ground();
            Vector3 before = scope.Root.transform.position;
            scope.Simulation.Step(new PlayerInputFrame
            {
                tick = 1, move = Vector2.up, look = new Vector2(90f, 0f)
            });
            PlayerSimState state = scope.Simulation.State;
            Require(Near(state.yaw, 90f) &&
                    Near(Horizontal(state.position - before), Vector3.right * (0.55f * SimTime.StepSeconds)) &&
                    Near(Horizontal(state.velocity), Vector3.right * 0.55f, VelocityTolerance),
                "同拍的 Look 必须只积分一次，Move 应使用该拍更新后的 yaw。");
        }

        private static void TestGroundAcceleration()
        {
            using var scope = new PreviewScope();
            scope.Ground();
            scope.Simulation.Step(new PlayerInputFrame { tick = 1, move = Vector2.up });
            Require(Near(Horizontal(scope.Simulation.State.velocity), Vector3.forward * 0.55f, VelocityTolerance),
                "默认 5.5 加速度、6 期望速度和 60 Hz 步长，静止起步首拍应得到 0.55 水平速度。");
            float previous = Horizontal(scope.Simulation.State.velocity).magnitude;
            for (uint tick = 2; tick <= 90; tick++)
            {
                scope.Simulation.Step(new PlayerInputFrame { tick = tick, move = Vector2.up });
                float speed = Horizontal(scope.Simulation.State.velocity).magnitude;
                Require(speed + VelocityTolerance >= previous && speed <= 6f + VelocityTolerance &&
                        scope.Simulation.State.grounded,
                    "持续同向输入必须在真实地面逐步加速，不能超出该方向期望速度或丢失接地。");
                previous = speed;
            }
            Require(Near(previous, 6f, VelocityTolerance), "持续按键应最终达到配置移动速度 6。");
        }

        private static void TestGroundFriction()
        {
            using var scope = new PreviewScope();
            scope.Ground();
            RestoreGroundVelocity(scope, Vector3.forward * 6f);
            scope.Simulation.Step(new PlayerInputFrame { tick = 1 });
            Require(Near(Horizontal(scope.Simulation.State.velocity), Vector3.forward * 5.48f, VelocityTolerance),
                "高速无输入时，应按当前速度 6 计算摩擦，首拍减至 5.48。");
            RestoreGroundVelocity(scope, Vector3.forward);
            scope.Simulation.Step(new PlayerInputFrame { tick = 2 });
            Require(Near(Horizontal(scope.Simulation.State.velocity), Vector3.forward * 0.8336f, VelocityTolerance),
                "低速时应按停止速度 1.92 计算摩擦，不能仅按当前速度衰减。");
            RestoreGroundVelocity(scope, Vector3.forward * 0.1f);
            scope.Simulation.Step(new PlayerInputFrame { tick = 3 });
            Require(Near(Horizontal(scope.Simulation.State.velocity), Vector3.zero, VelocityTolerance),
                "摩擦超过当前速度时必须归零，不能反向或出现负速度。");
            scope.Simulation.Step(new PlayerInputFrame { tick = 4 });
            Require(Near(Horizontal(scope.Simulation.State.velocity), Vector3.zero, VelocityTolerance),
                "静止无输入必须继续静止。");
        }

        private static void TestCounterStrafe()
        {
            using var scope = new PreviewScope();
            scope.Ground();
            RestoreGroundVelocity(scope, Vector3.forward * 6f);
            scope.Simulation.Step(new PlayerInputFrame { tick = 1, move = Vector2.down });
            Require(Near(Horizontal(scope.Simulation.State.velocity), Vector3.forward * 4.93f, VelocityTolerance),
                "反向首拍必须先摩擦至 5.48，再减去 0.55，不能直接把速度赋成反向满速。");
            bool reversed = false;
            for (uint tick = 2; tick <= 60; tick++)
            {
                scope.Simulation.Step(new PlayerInputFrame { tick = tick, move = Vector2.down });
                reversed |= scope.Simulation.State.velocity.z < 0f;
            }
            Require(reversed && Near(scope.Simulation.State.velocity.z, -6f, VelocityTolerance),
                "持续反向输入应在制动后反向加速，最终达到反向满速。");
        }

        private static void TestTurnInertia()
        {
            using var scope = new PreviewScope();
            scope.Ground();
            RestoreGroundVelocity(scope, Vector3.forward * 8f);
            scope.Simulation.Step(new PlayerInputFrame { tick = 1, move = Vector2.right });
            Vector3 velocity = Horizontal(scope.Simulation.State.velocity);
            Require(Near(velocity.x, 0.55f, VelocityTolerance) &&
                    Near(velocity.z, 7.3066667f, VelocityTolerance) && velocity.magnitude > 6f,
                "转向应在新方向增加投影速度，并保留摩擦后的旧方向分量，不能旋转惯性或截断总速。");
        }

        private static void TestDiagonalSpeed()
        {
            using var scope = new PreviewScope();
            scope.Ground();
            PlayerSimState baseline = scope.Simulation.State;
            scope.Simulation.Step(new PlayerInputFrame { tick = 1, move = Vector2.one });
            Vector3 diagonal = Horizontal(scope.Simulation.State.velocity);
            Require(Near(diagonal.magnitude, 0.55f, VelocityTolerance) && Near(diagonal.x, diagonal.z, VelocityTolerance),
                "对角线输入应归一化，起步加速度不能放大到根号二倍。");
            scope.Simulation.RestoreState(baseline);
            scope.Simulation.Step(new PlayerInputFrame { tick = 2, move = Vector2.up });
            Require(Near(Horizontal(scope.Simulation.State.velocity).magnitude, diagonal.magnitude, VelocityTolerance),
                "同样静止起步时，对角线和单轴输入应得到相同速度大小。");
            scope.Simulation.RestoreState(baseline);
            scope.Simulation.Step(new PlayerInputFrame { tick = 3, move = Vector2.up * 0.5f });
            Require(Near(Horizontal(scope.Simulation.State.velocity), Vector3.forward * 0.275f, VelocityTolerance),
                "半幅模拟量应使用一半期望速度，不应被无条件归一化为满幅输入。");
        }

        private static void TestCollisionState()
        {
            using var scope = new PreviewScope();
            scope.Ground();
            BoxCollider wall = scope.CreateBox("MovementValidationWall", new Vector3(100f, 2f, 101.25f), new Vector3(10f, 6f, 0.5f));
            Physics.SyncTransforms();
            bool blocked = false;
            for (uint tick = 1; tick <= 30; tick++)
            {
                Vector3 before = scope.Root.transform.position;
                scope.Simulation.Step(new PlayerInputFrame { tick = tick, move = Vector2.up });
                PlayerSimState state = scope.Simulation.State;
                Vector3 actual = scope.Root.transform.position - before;
                Require(Near(state.position, scope.Root.transform.position) &&
                        Near(state.velocity, actual / SimTime.StepSeconds, VelocityTolerance) &&
                        state.grounded == scope.Body.isGrounded,
                    "每拍必须回写 CharacterController 碰撞后的现场，不能保留期望位移或期望速度。");
                blocked |= tick > 15 && actual.z < 0.001f;
            }
            Require(blocked && scope.Simulation.State.position.z < 100.65f &&
                    Mathf.Abs(scope.Simulation.State.velocity.z) < 0.05f && scope.Simulation.State.grounded,
                "真实墙体必须阻挡前进，真实地面必须保持接地，撞墙后的水平速度应接近零。");
            scope.Simulation.Step(new PlayerInputFrame { tick = 31 });
            Require(Near(Horizontal(scope.Simulation.State.velocity), Vector3.zero, VelocityTolerance),
                "撞墙后松手应保持静止，下一拍不能继续消费碰撞前的隐藏速度。");
            wall.enabled = false;
            Physics.SyncTransforms();
            scope.Simulation.Step(new PlayerInputFrame { tick = 32, move = Vector2.up });
            Require(Near(Horizontal(scope.Simulation.State.velocity), Vector3.forward * 0.55f, VelocityTolerance),
                "移除墙后应从实际零速度重新加速，不能恢复原期望满速。");
        }

        private static void TestRestoreAndContinue()
        {
            using var scope = new PreviewScope();
            scope.Ground();
            var restored = new PlayerSimState
            {
                position = scope.Simulation.State.position + new Vector3(1f, 0f, 3f),
                velocity = new Vector3(2.25f, -1.5f, 4.75f),
                verticalVelocity = -1.5f, yaw = 37f, pitch = -21f, grounded = true
            };
            scope.Simulation.RestoreState(restored);
            Require(scope.Simulation.State.Equals(restored) && scope.Body.enabled &&
                    Near(scope.Root.transform.position, restored.position) &&
                    Mathf.Abs(Mathf.DeltaAngle(scope.Root.transform.eulerAngles.y, restored.yaw)) < 0.001f &&
                    Near(scope.Sight.AppliedPitch, restored.pitch),
                "恢复应保留快照全部字段、物理启用状态，并同步真实位置和视角。");
            scope.Simulation.Step(new PlayerInputFrame { tick = 2 });
            PlayerSimState after = scope.Simulation.State;
            Vector3 expected = new(2.055f, 0f, 4.3383333f);
            Require(Near(Horizontal(after.velocity), expected, VelocityTolerance) &&
                    Near(Horizontal(after.position - restored.position), expected * SimTime.StepSeconds) &&
                    Near(after.yaw, 37f) && Near(after.pitch, -21f) && after.grounded,
                "恢复后无输入应从快照的非零水平速度继续摩擦，不能清零或使用控制器旧惯性。");
        }

        private static void TestReplayConsistency()
        {
            using var scope = new PreviewScope();
            scope.Ground();
            PlayerSimState baseline = RestoreGroundVelocity(scope, new Vector3(1f, 0f, 2f));
            var inputs = new PlayerInputFrame[18];
            for (int i = 0; i < inputs.Length; i++)
            {
                inputs[i] = new PlayerInputFrame
                {
                    tick = (uint)i + 1,
                    move = i < 6 ? Vector2.up : i < 10 ? Vector2.zero : i < 14 ? Vector2.right : Vector2.down,
                    look = new Vector2(2f, -0.25f)
                };
                scope.Simulation.Step(inputs[i]);
            }
            PlayerSimState continuous = scope.Simulation.State;
            scope.Simulation.SetReplayMode(true);
            try
            {
                scope.Simulation.RestoreState(baseline);
                foreach (PlayerInputFrame input in inputs) scope.Simulation.Step(input);
            }
            finally { scope.Simulation.SetReplayMode(false); }
            PlayerSimState replayed = scope.Simulation.State;
            Require(Near(replayed.position, continuous.position) &&
                    Near(replayed.velocity, continuous.velocity, VelocityTolerance) &&
                    Near(replayed.verticalVelocity, continuous.verticalVelocity) &&
                    Near(replayed.yaw, continuous.yaw) && Near(replayed.pitch, continuous.pitch) &&
                    replayed.grounded == continuous.grounded,
                "恢复包含非零惯性的起点并重放同一组输入，必须得到与连续步进一致的完整状态。");
        }

        private static void TestPhysicalState()
        {
            using var scope = new PreviewScope();
            scope.Simulation.SetPhysical(false);
            Require(!scope.Body.enabled, "禁用模拟物理必须同步禁用 CharacterController。");
            var restored = new PlayerSimState
            {
                position = PreviewScope.Origin + Vector3.right,
                velocity = new Vector3(1f, 2f, 3f),
                verticalVelocity = 2f, yaw = 23f, pitch = 11f, grounded = false
            };
            scope.Simulation.RestoreState(restored);
            Require(!scope.Body.enabled && scope.Simulation.State.Equals(restored) &&
                    Near(scope.Root.transform.position, restored.position),
                "恢复不能意外启用原本禁用的物理，也不能重算或清空快照。");
            scope.Simulation.SetPhysical(true);
            Require(scope.Body.enabled && scope.Simulation.State.Equals(restored),
                "重新启用物理不能初始化或覆盖已有模拟状态。");
            scope.Simulation.Step(new PlayerInputFrame { tick = 1 });
            Require(Near(scope.Simulation.State.position, restored.position + Vector3.up * (2f * SimTime.StepSeconds)),
                "重新启用后应从恢复的位置和竖直速度正常继续移动。");
        }

        private static void TestAirCompatibility()
        {
            using var scope = new PreviewScope();
            scope.Ground();
            PlayerSimState grounded = scope.Simulation.State;
            var airborne = grounded;
            airborne.position += Vector3.up * 2f;
            airborne.velocity = Vector3.forward * 3f;
            airborne.grounded = false;
            scope.Simulation.RestoreState(airborne);
            scope.Simulation.Step(new PlayerInputFrame { tick = 1, move = Vector2.right });
            Require(Near(Horizontal(scope.Simulation.State.velocity), Vector3.right * 6f, VelocityTolerance),
                "本次只修改地面移动，空中仍应按原规则直接使用输入水平速度。");
            scope.Simulation.RestoreState(grounded);
            PlayerStepResult jump = scope.Simulation.Step(new PlayerInputFrame { tick = 2, move = Vector2.up, jump = true });
            Require(jump.jumped && !scope.Simulation.State.grounded &&
                    Near(scope.Simulation.State.verticalVelocity, 5f) &&
                    Near(Horizontal(scope.Simulation.State.velocity), Vector3.forward * 6f, VelocityTolerance),
                "起跳成功当拍已经离地，必须进入保留的空中分支，不能额外应用地面摩擦和加速。");
        }

        private static void TestReplayView()
        {
            using var scope = new PreviewScope();
            PlayerSimState initial = scope.Simulation.State;
            initial.yaw = 10f;
            initial.pitch = 5f;
            scope.Simulation.RestoreState(initial);
            Quaternion displayed = scope.Sight.View.localRotation;
            scope.Simulation.SetReplayMode(true);
            try
            {
                PlayerSimState corrected = initial;
                corrected.position += Vector3.right;
                corrected.yaw = 30f;
                corrected.pitch = -10f;
                scope.Simulation.RestoreState(corrected);
                Require(Near(scope.Sight.AppliedPitch, 5f) &&
                        Quaternion.Angle(scope.Sight.View.localRotation, displayed) < 0.001f,
                    "回放恢复只能改变模拟和物理现场，不能提前提交校正视角。");
                scope.Simulation.Step(new PlayerInputFrame { tick = 1, move = Vector2.up, look = new Vector2(4f, -6f) });
                scope.Simulation.Step(new PlayerInputFrame { tick = 2, look = new Vector2(2f, -3f) });
                PlayerSimState replayed = scope.Simulation.State;
                Require(Near(replayed.yaw, 36f) && Near(replayed.pitch, -1f) &&
                        Mathf.Abs(Mathf.DeltaAngle(scope.Root.transform.eulerAngles.y, replayed.yaw)) < 0.001f &&
                        Near(scope.Sight.AppliedPitch, 5f) &&
                        Quaternion.Angle(scope.Sight.View.localRotation, displayed) < 0.001f,
                    "回放期间仍应积分视角、更新真实根朝向，但显示俯仰应保持回放前的值。");
            }
            finally { scope.Simulation.SetReplayMode(false); }
            Require(!scope.Simulation.IsReplaying && Near(scope.Sight.AppliedPitch, -1f) &&
                    Quaternion.Angle(scope.Sight.View.localRotation, Quaternion.Euler(-1f, 0f, 0f)) < 0.001f,
                "回放结束必须统一提交最后一拍的最终视角。");
        }

        private static void TestLiveConfiguration()
        {
            using var scope = new PreviewScope();
            scope.Ground();
            PlayerSimState reset = scope.Simulation.State;
            scope.Simulation.Step(new PlayerInputFrame { tick = 1, move = Vector2.up, look = new Vector2(1f, -1f) });
            Require(Near(scope.Simulation.State.yaw, 1f) && Near(scope.Simulation.State.pitch, 1f),
                "首拍必须使用初始视角配置。");
            Set(scope.Data, "_moveSpeed", 12f);
            Set(scope.Data, "_jumpSpeed", 8f);
            Set(scope.Data, "_groundAcceleration", 3f);
            Set(scope.Data, "_groundFriction", 2f);
            Set(scope.Data, "_stopSpeed", 2f);
            Set(scope.Data, "_surfaceFriction", 0.5f);
            Set(scope.Movement, "_gravityScale", 2f);
            Set(scope.Movement, "_groundedForce", -3f);
            scope.Sight.Sensitivity = 2f;
            scope.Sight.SetPitchLimit(-4f, 4f);
            reset.verticalVelocity = -1f;
            scope.Simulation.RestoreState(reset);
            scope.Simulation.Step(new PlayerInputFrame { tick = 2, move = Vector2.up, look = new Vector2(5f, -5f) });
            PlayerSimState changed = scope.Simulation.State;
            Require(Near(Horizontal(changed.velocity).magnitude, 0.3f, VelocityTolerance) &&
                    Near(changed.yaw, 10f) && Near(changed.pitch, 4f) &&
                    Near(changed.verticalVelocity, -3f + GForce.globalGForce * 2f * SimTime.StepSeconds),
                "下一拍必须使用新速度 12、加速度 3、表面摩擦 0.5，并同步更新重力与视角配置。");
            reset.velocity = Vector3.forward;
            scope.Simulation.RestoreState(reset);
            scope.Simulation.Step(new PlayerInputFrame { tick = 3 });
            Require(Near(Horizontal(scope.Simulation.State.velocity), Vector3.forward * 0.9666667f, VelocityTolerance),
                "新的停止速度 2、摩擦 2 和表面摩擦 0.5 必须直接参与下一拍无输入减速。");
            scope.Simulation.RestoreState(reset);
            PlayerStepResult jump = scope.Simulation.Step(new PlayerInputFrame { tick = 4, jump = true });
            PlayerMotorConfig config = scope.Movement.BuildConfig(scope.Sight.Sensitivity, scope.Sight.MinPitch, scope.Sight.MaxPitch);
            Require(jump.jumped && Near(scope.Simulation.State.verticalVelocity, 8f) &&
                    Near(config.groundedForce, -3f),
                "修改后的跳速和接地力也必须在下一拍读取。");
        }

        private static void TestPrefabConfiguration()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player/Player.prefab");
            Require(prefab != null && prefab.GetComponents<PlayerMovementController>().Length == 1 &&
                    prefab.GetComponents<PlayerSimulation>().Length == 1,
                "玩家预制体必须同时包含唯一的移动组件和模拟组件。");
            PlayerMovementDataSO expected = AssetDatabase.LoadAssetAtPath<PlayerMovementDataSO>(
                AssetDatabase.GUIDToAssetPath("ea860f4d7585d8f48a213ed1d68e58f2"));
            var serialized = new SerializedObject(prefab.GetComponent<PlayerMovementController>());
            Require(expected != null &&
                    serialized.FindProperty("_playerMovementDataSO").objectReferenceValue == expected &&
                    Near(serialized.FindProperty("_gravityScale").floatValue, 2f) &&
                    Near(serialized.FindProperty("_groundedForce").floatValue, 0f),
                "拆分后必须保留原移动 SO、重力倍率 2 和接地力 0，不能退回新组件默认值。");
            PlayerMotorConfig config = prefab.GetComponent<PlayerMovementController>().BuildConfig(1f, -89f, 89f);
            Require(Near(config.groundAcceleration, 5.5f) && Near(config.groundFriction, 5.2f) &&
                    Near(config.stopSpeed, 1.92f) && Near(config.surfaceFriction, 1f),
                "玩家预设必须采用已约定的地面加速、摩擦、停止速度和表面摩擦默认值。");
        }

        private static Vector3 Horizontal(Vector3 value) => new(value.x, 0f, value.z);

        private static PlayerSimState RestoreGroundVelocity(PreviewScope scope, Vector3 velocity)
        {
            PlayerSimState state = scope.Simulation.State;
            state.velocity = Horizontal(velocity);
            state.verticalVelocity = -2f;
            state.grounded = true;
            state.yaw = 0f;
            state.pitch = 0f;
            scope.Simulation.RestoreState(state);
            return state;
        }

        private static bool Near(float actual, float expected, float tolerance = PositionTolerance) =>
            Mathf.Abs(actual - expected) <= tolerance;

        private static bool Near(Vector3 actual, Vector3 expected, float tolerance = PositionTolerance) =>
            Vector3.Distance(actual, expected) <= tolerance;

        private static void Set(object target, string name, object value) => target.GetType()
            .GetField(name, PrivateInstance).SetValue(target, value);

        private static void Require(bool value, string message)
        {
            if (!value) throw new InvalidOperationException(message);
        }

        private static void RunCase(string name, Action test, StringBuilder report, ref int passed)
        {
            try { test(); }
            catch (Exception exception) { throw new InvalidOperationException("玩家移动验证失败：" + name, exception); }
            passed++;
            report.AppendLine("PASS " + name);
        }

        private sealed class PreviewScope : IDisposable
        {
            public static readonly Vector3 Origin = new(100f, 2f, 100f);
            private readonly Scene _scene = EditorSceneManager.NewPreviewScene();
            public GameObject Root { get; }
            public CharacterController Body { get; }
            public PlayerMovementDataSO Data { get; }
            public PlayerMovementController Movement { get; }
            public PlayerSightController Sight { get; }
            public PlayerSimulation Simulation { get; }

            public PreviewScope(bool withSimulation = true)
            {
                Root = CreateRoot("PlayerMovementValidation");
                Root.transform.position = Origin;
                Movement = Root.AddComponent<PlayerMovementController>();
                Body = Root.GetComponent<CharacterController>();
                Body.height = 2f;
                Body.radius = 0.5f;
                Body.center = Vector3.zero;
                Body.skinWidth = 0.03f;
                Body.minMoveDistance = 0f;
                Data = ScriptableObject.CreateInstance<PlayerMovementDataSO>();
                Data.hideFlags = HideFlags.HideAndDontSave;
                Set(Data, "_moveSpeed", 6f);
                Set(Data, "_jumpSpeed", 5f);
                Set(Movement, "_playerMovementDataSO", Data);
                Set(Movement, "_gravityScale", 0f);
                if (!withSimulation) return;
                Sight = Root.AddComponent<PlayerSightController>();
                Transform view = CreateRoot("MovementValidationView").transform;
                view.SetParent(Root.transform, false);
                Set(Sight, "_view", view);
                Sight.Sensitivity = 1f;
                Simulation = Root.AddComponent<PlayerSimulation>();
                typeof(PlayerSimulation).GetMethod("Awake", PrivateInstance).Invoke(Simulation, null);
                Simulation.CaptureFromBody();
            }

            public void Ground()
            {
                CreateBox("MovementValidationGround", new Vector3(100f, -0.5f, 100f), new Vector3(80f, 1f, 80f));
                Set(Movement, "_gravityScale", 1f);
                Set(Movement, "_groundedForce", -2f);
                Body.enabled = false;
                Root.transform.position = new Vector3(100f, 1.1f, 100f);
                Body.enabled = true;
                Physics.SyncTransforms();
                Body.Move(Vector3.down * 0.5f);
                Require(Body.isGrounded, "隔离地面应与真实 CharacterController 建立接地接触。");
                Simulation.CaptureFromBody();
            }

            public BoxCollider CreateBox(string name, Vector3 position, Vector3 size)
            {
                GameObject root = CreateRoot(name);
                root.transform.position = position;
                BoxCollider collider = root.AddComponent<BoxCollider>();
                collider.size = size;
                return collider;
            }

            private GameObject CreateRoot(string name)
            {
                GameObject root = EditorUtility.CreateGameObjectWithHideFlags(name, HideFlags.HideAndDontSave);
                SceneManager.MoveGameObjectToScene(root, _scene);
                return root;
            }

            public void Dispose()
            {
                if (_scene.IsValid()) EditorSceneManager.ClosePreviewScene(_scene);
                if (Data != null) UnityEngine.Object.DestroyImmediate(Data);
            }
        }
    }
}
