using System;
using System.Linq;
using System.Reflection;
using System.Text;
using Core.Event;
using Core.Input;
using GamePlay.Events;
using GamePlay.HoldSystem;
using GamePlay.Player;
using GamePlay.Player.Simulation;
using GamePlay.Player.Simulation.Reconciliation;
using Network.Server;
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
                RunCase("预测与权威开火从相机挂点前方开始，并穿过相机画面中心", TestCameraViewRay, report, ref passed);
                RunCase("服务器玩法每次成功扣弹生成一个事实，准备、冷却、松手和空弹结果正确", TestGameLogicCadence, report, ref passed);
                RunCase("服务器玩法按输入顺序完成多玩家，并保留独立的同拍射击事实", TestGameLogicBatchResults, report, ref passed);
                RunCase("服务器玩法隔离扣弹后发生的玩家异常，并继续处理后续玩家", TestGameLogicFailureIsolation, report, ref passed);
                RunCase("服务器玩法检查输入集合，并记录无效玩家与错误身份", TestGameLogicGuards, report, ref passed);
                RunCase("预测和权威切枪结果记录有效栏位变化，并保留弹药与准备时间", TestWeaponSwitchResults, report, ref passed);
                RunCase("服务器独立发布切枪事实，准备完成后再生成开火事实", TestGameLogicWeaponSwitchEvents, report, ref passed);
                RunCase("同拍多键按实际顺序处理并记录每次有效切换", TestMultiKeyWeaponSwitch, report, ref passed);
                RunCase("R 输入锁存到下一拍且消费一次，重置清理积压请求", TestReloadInput, report, ref passed);
                RunCase("刀的左右键按下沿独立消费，漏拍、重传与长按不会补刀", TestKnifeAttackInput, report, ref passed);
                RunCase("权威刀具按截止拍接收新攻击，仅产生轻重击动作事实", TestKnifeAttackFacts, report, ref passed);
                RunCase("输入开关清空设备与驱动积压，禁用期间继续产生无操作输入帧", TestInputAvailability, report, ref passed);
                RunCase("本机 Controller 根据权威生命状态广播输入开关，停止控制后取消订阅", TestLocalLifeInputGate, report, ref passed);
                RunCase("预测与权威换弹按拍装填，同拍优先开火且射击期间可立即换弹", TestReloadLiveSteps, report, ref passed);
                RunCase("服务器捕获权威 C# 主动取消和重新准备的有序事实", TestAuthoritativeActionFacts, report, ref passed);
                RunCase("包含 R 的运动重放保持换弹进度、弹药和生命周期次数", TestReloadMovementReplay, report, ref passed);
                RunCase("晚装备与长期收起后立即准备使用当前武器时钟，保持完整准备时长", TestReequippedGunClock, report, ref passed);
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
                Require(!knife.Fired && knife.SwitchedWeaponSlot == WeaponSlot.Knife &&
                        scope.Holder.CurrentSlot == WeaponSlot.Knife &&
                        gun.GunState == GunState.None && gun.Magazine.Rounds == rounds,
                    "同拍切刀必须先取消旧枪动作，Fire 不得继续驱动旧枪。");
                PlayerLiveStepResult rifle = scope.Step(new PlayerInputFrame { tick = next++, key1 = true, fire = true });
                Require(!rifle.Fired && rifle.SwitchedWeaponSlot == WeaponSlot.Rifle &&
                        ReferenceEquals(scope.Holder.CurrentWeapon, gun) &&
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
                    jump = true, fire = true, reload = true, key3 = true
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
                        tick = 1, move = Vector2.one, look = Vector2.one, key3 = true, jump = true, fire = true, reload = true
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
            Require(result.Fired && result.FireWeaponSlot == WeaponSlot.Rifle &&
                    result.State.yaw == 30f && result.State.pitch == 15f &&
                    Vector3.Distance(result.FireOrigin,
                        scope.Sight.View.position + expected * PlayerShootConfig.ShootIgnoreDistance) < 0.0001f &&
                    Vector3.Distance(result.FireDirection, expected) < 0.0001f &&
                    result.State.Equals(scope.Simulation.State),
                "射线必须来自该拍完成后的服务器状态，并从 Sight 挂点沿射击方向前移配置距离。");
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
            Require(fallback.Fired && Vector3.Distance(fallback.FireOrigin,
                    fallback.State.position + fallback.FireDirection * PlayerShootConfig.ShootIgnoreDistance) < 0.0001f,
                "没有 Sight 挂点时应从模拟位置沿射击方向前移配置距离。");
        }

        private static void TestGameLogicCadence()
        {
            using var scope = new PreviewScope(PawnRole.Authoritative);
            AK47 gun = scope.EquipRifle();
            var logic = new ServerGameLogic();
            uint inputTick = 0;
            int initialRounds = gun.Magazine.Rounds;
            int initialReserve = gun.Magazine.ReserveRounds;
            int totalEvents = 0;

            GameTickResult Step(bool fire)
            {
                var frame = new PlayerInputFrame { tick = ++inputTick, fire = fire };
                int roundsBefore = gun.Magazine.Rounds;
                GameTickResult result = logic.Step(1000 + inputTick,
                    new[] { new ServerPlayerInput(73, 9, scope.Brain, frame) });
                Require(result.ProcessedInputs.Count == 1 && result.Failures.Count == 0 &&
                        result.ProcessedInputs[0].PlayerId == 73 &&
                        result.ProcessedInputs[0].PredictionEpoch == 9 &&
                        result.ProcessedInputs[0].InputTick == inputTick,
                    "正常玩家的每拍结果必须携带可确认的原始玩家、轮次和输入拍。");
                Require(FireCount(result) == roundsBefore - gun.Magazine.Rounds,
                    "玩法开火事实数量必须逐拍等于实际弹匣消耗量。");
                totalEvents += FireCount(result);
                CheckTick(scope, inputTick);
                return result;
            }

            for (uint tick = 1; tick <= gun.Config.PrepareTicks; tick++)
                Require(FireCount(Step(true)) == 0, "准备过程应完成输入并返回空开火集合。");
            Require(FireCount(Step(true)) == 1, "准备结束当拍应生成一个开火事实。");
            for (uint tick = 1; tick < gun.Config.ShotIntervalTicks; tick++)
                Require(FireCount(Step(true)) == 0, "冷却过程应完成输入并返回空开火集合。");
            Require(FireCount(Step(true)) == 1, "冷却到期应生成第二个开火事实。");
            for (uint tick = 0; tick < gun.Config.ShotIntervalTicks * 2; tick++)
                Require(FireCount(Step(false)) == 0, "松手时推进状态并返回空开火集合。");
            for (uint tick = 0; tick <= initialRounds * gun.Config.ShotIntervalTicks; tick++)
                Step(true);
            Require(gun.Magazine.Empty && totalEvents == initialRounds &&
                    gun.Magazine.ReserveRounds == initialReserve,
                "持续开火至空弹后，玩法事实总数必须等于弹匣原始弹量，备弹保留原值。");
            Require(FireCount(Step(true)) == 0, "空弹拍应完成输入并返回空开火集合。");
        }

        private static void TestGameLogicBatchResults()
        {
            using var first = new PreviewScope(PawnRole.Authoritative);
            using var second = new PreviewScope(PawnRole.Authoritative);
            AK47 firstGun = first.EquipRifle();
            second.EquipRifle();
            second.Root.transform.position += Vector3.right * 20f;
            second.Simulation.CaptureFromBody();
            var logic = new ServerGameLogic();
            for (uint tick = 1; tick <= firstGun.Config.PrepareTicks; tick++)
                logic.Step(tick, new[]
                {
                    new ServerPlayerInput(73, 9, first.Brain, new PlayerInputFrame { tick = tick }),
                    new ServerPlayerInput(81, 12, second.Brain, new PlayerInputFrame { tick = tick + 400 })
                });

            uint firstTick = firstGun.Config.PrepareTicks + 1;
            var inputs = new[]
            {
                new ServerPlayerInput(73, 9, first.Brain, new PlayerInputFrame
                {
                    tick = firstTick, fire = true, move = Vector2.up, look = new Vector2(30f, -15f)
                }),
                new ServerPlayerInput(81, 12, second.Brain, new PlayerInputFrame
                {
                    tick = firstTick + 400, fire = true, look = new Vector2(-20f, 10f)
                })
            };
            GameTickResult result = logic.Step(900, inputs);
            Require(result.ProcessedInputs.Count == 2 && result.Events.Count == 2 && result.Failures.Count == 0,
                "同一服务器拍应汇集两个成功玩家的输入与开火事实。");
            var firstFire = result.Events[0] as ShotFiredEvent;
            var secondFire = result.Events[1] as ShotFiredEvent;
            Require(firstFire != null && secondFire != null &&
                    firstFire.ServerTick == 900 && firstFire.PlayerId == 73 &&
                    firstFire.PredictionEpoch == 9 && firstFire.InputTick == firstTick &&
                    secondFire.ServerTick == 900 && secondFire.PlayerId == 81 &&
                    secondFire.PredictionEpoch == 12 && secondFire.InputTick == firstTick + 400 &&
                    result.ProcessedInputs[0].PlayerId == 73 && result.ProcessedInputs[1].PlayerId == 81,
                "同拍事实必须按输入顺序保留各自的玩家、预测轮次与输入拍。");
            Vector3 expectedFirst = Quaternion.Euler(first.Brain.State.pitch, first.Brain.State.yaw, 0f) * Vector3.forward;
            Vector3 expectedSecond = Quaternion.Euler(second.Brain.State.pitch, second.Brain.State.yaw, 0f) * Vector3.forward;
            Require(firstFire.Position == first.Brain.State.position && secondFire.Position == second.Brain.State.position &&
                    Vector3.Distance(firstFire.Origin,
                        first.Sight.View.position + expectedFirst * PlayerShootConfig.ShootIgnoreDistance) < 0.0001f &&
                    Vector3.Distance(secondFire.Origin,
                        second.Sight.View.position + expectedSecond * PlayerShootConfig.ShootIgnoreDistance) < 0.0001f &&
                    Vector3.Distance(firstFire.Direction, expectedFirst) < 0.0001f &&
                    Vector3.Distance(secondFire.Direction, expectedSecond) < 0.0001f,
                "每个开火事实必须取自对应玩家的同拍状态，并仅前移一次配置距离。");

            Vector3 firstPosition = firstFire.Position;
            Vector3 firstOrigin = firstFire.Origin;
            Vector3 firstDirection = firstFire.Direction;
            first.Root.transform.position += Vector3.right * 10f;
            first.Simulation.CaptureFromBody();
            inputs[0] = new ServerPlayerInput(73, 15, first.Brain, new PlayerInputFrame
            {
                tick = firstTick + 1, look = new Vector2(45f, 10f)
            });
            inputs[1] = new ServerPlayerInput(81, 18, second.Brain,
                new PlayerInputFrame { tick = firstTick + 401 });
            GameTickResult next = logic.Step(901, inputs);
            Require(next.ProcessedInputs.Count == 2 && next.Events.Count == 0 && next.Failures.Count == 0 &&
                    result.Events.Count == 2 && result.ProcessedInputs.Count == 2 &&
                    firstFire.ServerTick == 900 && firstFire.PredictionEpoch == 9 &&
                    firstFire.InputTick == firstTick && firstFire.Position == firstPosition &&
                    firstFire.Origin == firstOrigin && firstFire.Direction == firstDirection &&
                    result.ProcessedInputs[0].PredictionEpoch == 9 && result.ProcessedInputs[0].InputTick == firstTick,
                "更新输入数组、角色现场和后续拍时，已返回的集合及射线数据必须保持独立。");
        }

        private static void TestGameLogicFailureIsolation()
        {
            using var failing = new PreviewScope(PawnRole.Authoritative);
            using var healthy = new PreviewScope(PawnRole.Authoritative);
            AK47 failingGun = failing.EquipRifle();
            AK47 healthyGun = healthy.EquipRifle();
            var logic = new ServerGameLogic();
            for (uint tick = 1; tick <= failingGun.Config.PrepareTicks; tick++)
                logic.Step(tick, new[]
                {
                    new ServerPlayerInput(73, 9, failing.Brain, new PlayerInputFrame { tick = tick }),
                    new ServerPlayerInput(81, 12, healthy.Brain, new PlayerInputFrame { tick = tick })
                });

            var expectedFailure = new InvalidOperationException("Expected server player failure after ammo consumption.");
            void FailAfterShot(Gun gun) => throw expectedFailure;
            failing.Weapon.OnShot += FailAfterShot;
            int failingRounds = failingGun.Magazine.Rounds;
            int healthyRounds = healthyGun.Magazine.Rounds;
            uint inputTick = failingGun.Config.PrepareTicks + 1;
            GameTickResult result;
            try
            {
                result = logic.Step(900, new[]
                {
                    new ServerPlayerInput(73, 9, failing.Brain, new PlayerInputFrame { tick = inputTick, fire = true }),
                    new ServerPlayerInput(81, 12, healthy.Brain, new PlayerInputFrame { tick = inputTick, fire = true })
                });
            }
            finally { failing.Weapon.OnShot -= FailAfterShot; }

            Require(result.Failures.Count == 1 && result.Failures[0].PlayerId == 73 &&
                    result.Failures[0].PredictionEpoch == 9 && result.Failures[0].InputTick == inputTick &&
                    ReferenceEquals(result.Failures[0].Exception, expectedFailure) &&
                    failingGun.Magazine.Rounds == failingRounds - 1,
                "扣弹后发生异常的玩家必须返回原始异常及对应输入标识，供管理器结束异常会话。");
            Require(result.ProcessedInputs.Count == 1 && result.ProcessedInputs[0].PlayerId == 81 &&
                    result.Events.Count == 1 && result.Events[0].PlayerId == 81 &&
                    healthyGun.Magazine.Rounds == healthyRounds - 1,
                "前一个玩家执行失败后，后续正常玩家仍应完成输入、扣弹并产生开火事实。");
            GameTickResult next = logic.Step(901, new[]
            {
                new ServerPlayerInput(81, 12, healthy.Brain, new PlayerInputFrame { tick = inputTick + 1 })
            });
            Require(next.Failures.Count == 0 && next.ProcessedInputs.Count == 1 && next.Events.Count == 0 &&
                    result.Failures.Count == 1 && failingGun.Magazine.Rounds == failingRounds - 1,
                "后续拍处理存活玩家时，先前失败记录保持独立，失败玩家的弹药维持已执行结果。");
            Require(failingGun.TryReload(), "失败输入退出后，权威 C# 应能主动发起换弹。");
            var pendingActions = failing.Weapon.ConsumeActions();
            Require(pendingActions.Count == 3 && pendingActions[0].Kind == PlayerWeaponActionKind.Shot &&
                    !pendingActions[0].ServerInitiated && pendingActions[1].Kind == PlayerWeaponActionKind.Canceled &&
                    pendingActions[1].ServerInitiated && pendingActions[2].Kind == PlayerWeaponActionKind.ReloadStarted &&
                    pendingActions[2].ServerInitiated,
                "实时输入异常也必须在 finally 中清理来源上下文，后续 C# 操作应标为服务器主动事实。");
        }

        private static void TestGameLogicGuards()
        {
            var logic = new ServerGameLogic();
            bool rejectedNull = false;
            try { logic.Step(1, null); }
            catch (ArgumentNullException) { rejectedNull = true; }
            Require(rejectedNull, "空输入集合必须报告参数异常。");
            GameTickResult empty = logic.Step(1, Array.Empty<ServerPlayerInput>());
            Require(empty.ProcessedInputs.Count == 0 && empty.Events.Count == 0 && empty.Failures.Count == 0,
                "没有待处理玩家的服务器拍应返回三组空结果。");

            using var predicted = new PreviewScope(PawnRole.Predicted);
            AK47 gun = predicted.EquipRifle();
            PlayerSimState before = predicted.Brain.State;
            int rounds = gun.Magazine.Rounds;
            var frame = new PlayerInputFrame { tick = 7, fire = true, look = Vector2.one, key3 = true };
            GameTickResult result = logic.Step(2, new[]
            {
                new ServerPlayerInput(73, 9, null, frame),
                new ServerPlayerInput(81, 12, predicted.Brain, frame)
            });
            Require(result.ProcessedInputs.Count == 0 && result.Events.Count == 0 && result.Failures.Count == 2 &&
                    result.Failures[0].PlayerId == 73 && result.Failures[0].Exception != null &&
                    result.Failures[1].PlayerId == 81 && result.Failures[1].Exception is InvalidOperationException &&
                    predicted.Brain.State.Equals(before) && ReferenceEquals(predicted.Holder.CurrentWeapon, gun) &&
                    gun.Magazine.Rounds == rounds && predicted.Weapon.LocalTick == 0 && predicted.Weapon.ServerTick == 0,
                "缺失玩家与错误身份应分别记录失败，身份检查应在运动、切栏和枪械副作用前完成。");
        }

        private static void TestCameraViewRay()
        {
            foreach (PawnRole role in new[] { PawnRole.Predicted, PawnRole.Authoritative })
            {
                using var scope = new PreviewScope(role);
                AK47 gun = scope.EquipRifle();
                GameObject pointObject = EditorUtility.CreateGameObjectWithHideFlags(
                    "CameraViewRayPoint", HideFlags.HideAndDontSave);
                SceneManager.MoveGameObjectToScene(pointObject, scope.Root.scene);
                Transform point = pointObject.transform;
                point.SetParent(scope.Sight.View, false);
                point.localPosition = new Vector3(-0.08f, 0.14f, -0.11431779f);
                point.localRotation = Quaternion.Euler(2f, 7f, 0f);
                var cameraController = scope.Root.AddComponent<PlayerCameraController>();
                Set(cameraController, "TfCameraPoint", point);
                Invoke(cameraController, "Awake");
                Bind(scope.Brain, "Camera", cameraController);

                GameObject cameraObject = EditorUtility.CreateGameObjectWithHideFlags(
                    "CameraViewRayCamera", HideFlags.HideAndDontSave, typeof(UnityEngine.Camera));
                SceneManager.MoveGameObjectToScene(cameraObject, scope.Root.scene);
                cameraObject.transform.SetParent(point, false);
                var camera = cameraObject.GetComponent<UnityEngine.Camera>();
                camera.enabled = false;
                for (uint tick = 1; tick <= gun.Config.PrepareTicks; tick++)
                    scope.Step(new PlayerInputFrame { tick = tick });
                PlayerLiveStepResult result = scope.Step(new PlayerInputFrame
                {
                    tick = gun.Config.PrepareTicks + 1,
                    fire = true,
                    move = Vector2.up,
                    look = new Vector2(30f, -15f)
                });
                Require(result.Fired && cameraController.TryGetViewRay(out Ray ray) &&
                        Vector3.Distance(result.FireOrigin,
                            point.position + point.forward * PlayerShootConfig.ShootIgnoreDistance) < 0.0001f &&
                        Vector3.Distance(result.FireDirection, point.forward) < 0.0001f &&
                        Vector3.Distance(camera.transform.position, point.position) < 0.0001f &&
                        Vector3.Distance(result.FireDirection, camera.transform.forward) < 0.0001f,
                    role + " 开火必须沿相机挂点的世界视线前移配置距离，不能移动相机或改变射击方向。");
                Vector3 viewport = camera.WorldToViewportPoint(result.FireOrigin + result.FireDirection * 10f);
                Require(viewport.z > 0f && Mathf.Abs(viewport.x - 0.5f) < 0.0001f &&
                        Mathf.Abs(viewport.y - 0.5f) < 0.0001f,
                    role + " 的开火射线应穿过第一人称画面中心。");
                Require(cameraController.TryGetViewRay(out Ray viewRay) &&
                        Vector3.Distance(viewRay.origin, point.position) < 0.0001f &&
                        Vector3.Distance(viewRay.GetPoint(PlayerShootConfig.ShootIgnoreDistance), result.FireOrigin) < 0.0001f &&
                        Vector3.Distance(viewRay.direction, result.FireDirection) < 0.0001f,
                    "原始相机视线应保持不变，开火原点只沿该视线前移一次。");
                Set(cameraController, "TfCameraPoint", null);
                Require(!cameraController.TryGetViewRay(out _), "缺失相机挂点时应交由调用方使用兼容路径。");
            }
        }

        private static void TestWeaponSwitchResults()
        {
            foreach (PawnRole role in new[] { PawnRole.Predicted, PawnRole.Authoritative })
            {
                using var scope = new PreviewScope(role);
                AK47 gun = scope.EquipRifle();
                uint inputTick = 0;
                for (uint tick = 0; tick <= gun.Config.PrepareTicks; tick++)
                    scope.Step(new PlayerInputFrame { tick = ++inputTick, fire = true });
                int rounds = gun.Magazine.Rounds;
                int reserve = gun.Magazine.ReserveRounds;

                PlayerLiveStepResult knife = scope.Step(new PlayerInputFrame { tick = ++inputTick, key3 = true });
                PlayerLiveStepResult rifle = scope.Step(new PlayerInputFrame { tick = ++inputTick, key1 = true });
                Require(knife.SwitchedWeaponSlot == WeaponSlot.Knife && rifle.SwitchedWeaponSlot == WeaponSlot.Rifle &&
                        !knife.Fired && !rifle.Fired && ReferenceEquals(scope.Holder.CurrentWeapon, gun) &&
                        gun.GunState == GunState.Preparing && gun.Magazine.Rounds == rounds &&
                        gun.Magazine.ReserveRounds == reserve,
                    role + " 切刀再切枪应记录两次有效栏位变化，保留原武器及弹药，并重新开始准备。");

                PlayerLiveStepResult repeated = scope.Step(new PlayerInputFrame { tick = ++inputTick, key1 = true });
                PlayerLiveStepResult missing = scope.Step(new PlayerInputFrame { tick = ++inputTick, key2 = true });
                Require(!repeated.SwitchedWeaponSlot.HasValue && !missing.SwitchedWeaponSlot.HasValue &&
                        scope.Holder.CurrentSlot == WeaponSlot.Rifle && gun.Magazine.Rounds == rounds,
                    role + " 重复选择当前栏位和选择空手枪栏应保留当前装备，并返回空切枪结果。");
                CheckTick(scope, inputTick);
            }
        }

        private static void TestGameLogicWeaponSwitchEvents()
        {
            using var scope = new PreviewScope(PawnRole.Authoritative);
            AK47 gun = scope.EquipRifle();
            Require(scope.Holder.SelectSlot(WeaponSlot.Knife), "测试应从持刀状态开始。");
            var logic = new ServerGameLogic();
            int rounds = gun.Magazine.Rounds;
            scope.Weapon.ConsumeActions();

            GameTickResult rifle = logic.Step(100, new[]
            {
                new ServerPlayerInput(73, 9, scope.Brain, new PlayerInputFrame { tick = 1, key1 = true, fire = true })
            });
            Require(rifle.ProcessedInputs.Count == 1 && rifle.Failures.Count == 0 && rifle.Events.Count == 2 &&
                    rifle.Events[0] is WeaponSwitchedEvent selected && selected.ServerTick == 100 &&
                    selected.PlayerId == 73 && selected.PredictionEpoch == 9 && selected.InputTick == 1 &&
                    selected.WeaponSlot == WeaponSlot.Rifle && !selected.ServerInitiated && gun.GunState == GunState.Preparing &&
                    gun.Magazine.Rounds == rounds && rifle.Events[1] is PrepareStartedEvent preparingFact &&
                    !preparingFact.ServerInitiated,
                "切枪与开火同时请求时，应先完成切枪并发布对应事实，枪械按实际准备时间处理发射。");

            GameTickResult preparing = logic.Step(101, new[]
            {
                new ServerPlayerInput(73, 9, scope.Brain, new PlayerInputFrame { tick = 2, key1 = true, fire = true })
            });
            GameTickResult fired = logic.Step(102, new[]
            {
                new ServerPlayerInput(73, 9, scope.Brain, new PlayerInputFrame { tick = 3, fire = true })
            });
            Require(preparing.Events.Count == 0 && fired.Events.Count == 1 &&
                    fired.Events[0] is ShotFiredEvent shot && shot.WeaponSlot == WeaponSlot.Rifle &&
                    shot.InputTick == 3 && gun.Magazine.Rounds == rounds - 1,
                "准备期间重复选枪应生成空事件集合，准备完成后发射应生成一次开火事实。");

            GameTickResult knife = logic.Step(103, new[]
            {
                new ServerPlayerInput(73, 9, scope.Brain, new PlayerInputFrame { tick = 4, key3 = true, fire = true })
            });
            Require(knife.Events.Count == 3 && knife.Events[0] is WeaponActionCanceledEvent canceled &&
                    canceled.CanceledState == GunState.Shooting &&
                    knife.Events[1] is WeaponSwitchedEvent holstered &&
                    holstered.WeaponSlot == WeaponSlot.Knife && holstered.InputTick == 4 &&
                    knife.Events[2] is PrepareStartedEvent { WeaponSlot: WeaponSlot.Knife } &&
                    scope.Holder.CurrentSlot == WeaponSlot.Knife && gun.Magazine.Rounds == rounds - 1 &&
                    gun.GunState == GunState.None,
                "同拍切刀与开火应提交切刀事实，原枪以已消耗弹量结束当前动作。");
            Require(rifle.Events.Count == 2 && rifle.Events[0] is WeaponSwitchedEvent original &&
                    original.WeaponSlot == WeaponSlot.Rifle && original.ServerTick == 100 && original.InputTick == 1,
                "后续切枪和开火处理完成后，前一拍返回的切枪事实及其来源信息应保持独立。");
        }

        private static void TestMultiKeyWeaponSwitch()
        {
            using var scope = new PreviewScope(PawnRole.Authoritative);
            AK47 gun = scope.EquipRifle();
            Require(scope.Holder.SelectSlot(WeaponSlot.Knife), "测试应从持刀状态开始。");
            var logic = new ServerGameLogic();
            int rounds = gun.Magazine.Rounds;
            scope.Weapon.ConsumeActions();

            GameTickResult unchanged = logic.Step(200, new[]
            {
                new ServerPlayerInput(73, 9, scope.Brain, new PlayerInputFrame
                {
                    tick = 1, key1 = true, key2 = true, key3 = true, fire = true
                })
            });
            Require(unchanged.Events.Count == 3 && unchanged.Events[0] is WeaponSwitchedEvent first &&
                    first.WeaponSlot == WeaponSlot.Rifle && unchanged.Events[1] is WeaponSwitchedEvent second &&
                    second.WeaponSlot == WeaponSlot.Knife && unchanged.ProcessedInputs.Count == 1 &&
                    unchanged.Events[2] is PrepareStartedEvent { WeaponSlot: WeaponSlot.Knife } &&
                    unchanged.Failures.Count == 0 && scope.Holder.CurrentSlot == WeaponSlot.Knife &&
                    gun.Magazine.Rounds == rounds,
                "同拍按 Key1、Key2、Key3 顺序处理后，应依次记录实际切到长枪与刀的事实。");

            GameTickResult rifle = logic.Step(201, new[]
            {
                new ServerPlayerInput(73, 9, scope.Brain, new PlayerInputFrame { tick = 2, key1 = true, key2 = true })
            });
            Require(rifle.Events.Count == 2 && rifle.Events[0] is WeaponSwitchedEvent switched &&
                    switched.WeaponSlot == WeaponSlot.Rifle && switched.InputTick == 2 &&
                    rifle.Events[1] is PrepareStartedEvent && scope.Holder.CurrentSlot == WeaponSlot.Rifle,
                "Key1 选中长枪后，空手枪栏的 Key2 请求应保留该次有效的最终长枪选择。");

            GameTickResult knife = logic.Step(202, new[]
            {
                new ServerPlayerInput(73, 9, scope.Brain, new PlayerInputFrame { tick = 3, key1 = true, key3 = true })
            });
            Require(knife.Events.Count == 3 && knife.Events[0] is WeaponActionCanceledEvent canceled &&
                    canceled.CanceledState == GunState.Preparing && knife.Events[1] is WeaponSwitchedEvent final &&
                    final.WeaponSlot == WeaponSlot.Knife && scope.Holder.CurrentSlot == WeaponSlot.Knife &&
                    knife.Events[2] is PrepareStartedEvent { WeaponSlot: WeaponSlot.Knife } &&
                    gun.Magazine.Rounds == rounds,
                "Key3 应成为该拍最终有效选择，玩法层应发布一次对应切刀事实并保留弹药。");
        }

        private static int FireCount(GameTickResult result) => result.Events.OfType<ShotFiredEvent>().Count();

        private static void TestKnifeAttackInput()
        {
            using var scope = new PreviewScope(PawnRole.Predicted);
            var driver = scope.Root.AddComponent<PlayerInputDriver>();
            driver.Consume(0);
            Accumulate(driver, new PlayerInput { lightAttackPressed = true, lMouse = true });
            Accumulate(driver, new PlayerInput { heavyAttackPressed = true, lMouse = true });
            PlayerInputFrame first = driver.Consume(1);
            PlayerInputFrame held = driver.Consume(2);
            Require(first.lightAttackPressed && first.heavyAttackPressed && held.fire &&
                    !held.lightAttackPressed && !held.heavyAttackPressed, "刀只消耗按下沿，枪的持续开火保留。");
            Accumulate(driver, new PlayerInput { lightAttackPressed = true, heavyAttackPressed = true });
            driver.ResetPendingInput();
            PlayerInputFrame reset = driver.Consume(3);
            Require(!reset.lightAttackPressed && !reset.heavyAttackPressed, "重置时清除未消费的刀输入。");

            var queue = new PlayerInputQueue();
            queue.Push(first);
            queue.Push(new PlayerInputFrame { tick = first.tick });
            PlayerInputFrame accepted = queue.ResolveNext();
            Require(accepted.lightAttackPressed && accepted.heavyAttackPressed, "重复输入不能覆盖首次接收的按下沿。");
            queue.Commit(first.tick);
            queue.Push(first);
            PlayerInputFrame synthetic = queue.ResolveNext();
            Require(!synthetic.lightAttackPressed && !synthetic.heavyAttackPressed, "旧包和合成输入不能重放刀的按下沿。");
        }

        private static void TestKnifeAttackFacts()
        {
            using var scope = new PreviewScope(PawnRole.Authoritative);
            scope.EquipRifle();
            scope.Weapon.ConsumeActions();
            var logic = new ServerGameLogic();
            uint tick = 0;
            GameTickResult Step(bool select = false, bool light = false, bool heavy = false)
            {
                var frame = new PlayerInputFrame
                {
                    tick = ++tick, key3 = select, fire = true, lightAttackPressed = light, heavyAttackPressed = heavy
                };
                GameTickResult result = logic.Step(tick, new[] { new ServerPlayerInput(73, 9, scope.Brain, frame) });
                Require(result.ProcessedInputs.Count == 1 && result.Failures.Count == 0 && result.Shots.Count == 0,
                    "每拍刀具输入应完成确认，射击与伤害队列保持空。");
                return result;
            }
            GameTickResult draw = Step(select: true, light: true);
            Require(draw.Events.Count == 2 && draw.Events[0] is WeaponSwitchedEvent &&
                    draw.Events[1] is PrepareStartedEvent, "切刀同拍应只切栏并开始准备。");
            var knife = (Knife)scope.Holder.CurrentWeapon;
            for (uint elapsed = 1; elapsed < knife.Config.PrepareTicks; elapsed++)
                Require(Step(heavy: true).Events.Count == 0, "准备期间点击应丢弃。");
            Require(Step().Events.Count == 0 && knife.State == KnifeState.Ready, "到期时持续按住左键不能触发轻击。");
            GameTickResult lightAttack = Step(light: true);
            Require(lightAttack.Events.Count == 1 && lightAttack.Events[0] is KnifeAttackStartedEvent
                    { AttackKind: KnifeAttackKind.Light, WeaponSlot: WeaponSlot.Knife }, "轻击应产生独立动作事实。");
            for (uint elapsed = 1; elapsed < knife.Config.LightAttackTicks; elapsed++)
                Require(Step(heavy: true).Events.Count == 0, "轻击收尾前不能进入重击。");
            GameTickResult heavyAttack = Step(heavy: true);
            Require(heavyAttack.Events.Count == 1 && heavyAttack.Events[0] is KnifeAttackStartedEvent heavy &&
                    heavy.AttackKind == KnifeAttackKind.Heavy && heavy.InputTick == tick && heavy.PlayerId == 73 &&
                    heavy.PredictionEpoch == 9, "轻击结束拍可以接受新的重击按下沿。");
            for (uint elapsed = 0; elapsed < knife.Config.HeavyAttackTicks; elapsed++)
                Require(Step().Events.Count == 0, "重击与先前被拒绝的输入不能重复发布。");
            Require(knife.State == KnifeState.Ready, "重击结束回到就绪。");
        }

        private static void TestReloadInput()
        {
            using var scope = new PreviewScope(PawnRole.Predicted);
            PlayerInputDriver driver = scope.Root.AddComponent<PlayerInputDriver>();
            MethodInfo accumulate = typeof(PlayerInputDriver).GetMethod("Accumulate", PrivateInstance);
            Require(accumulate != null, "输入积累入口必须存在。");
            driver.Consume(0);
            accumulate.Invoke(driver, new object[] { new Core.Input.PlayerInput { r = true } });
            accumulate.Invoke(driver, new object[] { new Core.Input.PlayerInput() });
            Require(driver.Consume(1).reload && !driver.Consume(2).reload,
                "R 的短按应保留至下一拍，并且同一渲染帧内补算的后续拍保持清除。");
            accumulate.Invoke(driver, new object[] { new Core.Input.PlayerInput { r = true } });
            driver.ResetPendingInput();
            Require(!driver.Consume(3).reload && !driver.Consume(4).reload,
                "输入重置必须清掉尚未消费的换弹请求。");

            var queue = new PlayerInputQueue();
            queue.Push(new PlayerInputFrame { tick = 1, reload = true });
            queue.Push(new PlayerInputFrame { tick = 1, reload = false });
            Require(queue.ResolveNext().reload, "同一拍的冗余输入应仅保留首次接受的换弹请求。");
            queue.Commit(1);
            Require(!queue.ResolveNext().reload, "缺失输入的合成拍必须清除前一拍的换弹请求。");
            queue.Commit(2);
            queue.Push(new PlayerInputFrame { tick = 1, reload = true });
            Require(!queue.ResolveNext().reload, "已经确认的旧 R 输入不能再次执行。");
        }

        private static void TestInputAvailability()
        {
            using var scope = new PreviewScope(PawnRole.Predicted);
            InputCollector collector = scope.Root.AddComponent<InputCollector>();
            PlayerInputDriver driver = scope.Root.AddComponent<PlayerInputDriver>();
            Set(driver, "_collector", collector);
            driver.Consume(0);
            var sample = new PlayerInput
            {
                move = Vector2.one, look = Vector2.one, space = true, lMouse = true,
                r = true, key1 = true, key2 = true, key3 = true, lightAttackPressed = true, heavyAttackPressed = true
            };
            Accumulate(driver, sample);
            Set(collector, "_key1", true);
            driver.DisableInput();
            Require(!(bool)typeof(InputCollector).GetField("_key1", PrivateInstance).GetValue(collector),
                "禁用时必须丢弃采集器已经锁存的切枪请求。");
            RequireNeutralInput(driver.Consume(1), 1);
            Accumulate(driver, sample);
            driver.ResetPendingInput();
            RequireNeutralInput(driver.Consume(2), 2);

            Set(collector, "_reload", true);
            driver.EnableInput();
            Require(!(bool)typeof(InputCollector).GetField("_reload", PrivateInstance).GetValue(collector),
                "恢复时必须丢弃禁用期间锁存的换弹请求。");
            PlayerInputFrame empty = driver.Consume(3);
            Require(empty.move == Vector2.zero && empty.look == Vector2.zero && !empty.jump && !empty.fire &&
                    !empty.reload && !empty.key1 && !empty.key2 && !empty.key3 && !empty.lightAttackPressed && !empty.heavyAttackPressed,
                "恢复后不能重放禁用前及禁用期间的积压输入。");
            Accumulate(driver, sample);
            PlayerInputFrame fresh = driver.Consume(4);
            Require(fresh.tick == 4 && fresh.move == sample.move && fresh.look == sample.look && fresh.jump &&
                    fresh.fire && fresh.reload && fresh.key1 && fresh.key2 && fresh.key3 && fresh.lightAttackPressed && fresh.heavyAttackPressed,
                "恢复后新采集的动作应进入下一拍。");
        }

        private static void TestLocalLifeInputGate()
        {
            Require(EventCenter.Reset() && GameEventBootstrap.RegisterAll(), "隔离用例必须初始化玩法事件。");
            using var scope = new PreviewScope(PawnRole.Predicted);
            using var remoteScope = new PreviewScope(PawnRole.Interpolated);
            PlayerStatsController local = scope.Root.AddComponent<PlayerStatsController>();
            PlayerStatsController remote = remoteScope.Root.AddComponent<PlayerStatsController>();
            Bind(scope.Brain, "Stats", local);
            Bind(remoteScope.Brain, "Stats", remote);
            local.InitializeClient(true);
            remote.InitializeClient(false);
            ApplyLife(local, PlayerLifeState.Dead, 1);

            PlayerCameraController camera = scope.Root.AddComponent<PlayerCameraController>();
            Set(camera, "TfCameraPoint", scope.Sight.View);
            Bind(scope.Brain, "Camera", camera);
            scope.Root.AddComponent<Mirror.NetworkIdentity>();
            PlayerController controller = scope.Root.AddComponent<PlayerController>();
            Invoke(controller, "Awake");
            PlayerInputDriver driver = scope.Root.AddComponent<PlayerInputDriver>();
            Invoke(driver, "Awake");
            try
            {
                controller.OnStartLocalPlayer();
                RequireNeutralInput(driver.Consume(1), 1);
                ApplyLife(local, PlayerLifeState.Alive, 2);
                ApplyLife(remote, PlayerLifeState.Alive, 1);
                ApplyLife(remote, PlayerLifeState.Dead, 2);
                Accumulate(driver, new PlayerInput { lMouse = true });
                Require(driver.Consume(2).fire, "远端死亡不能锁定本机输入。");

                // 调试注入可以暂停设备采样，仍应收到本机死亡事件。
                driver.enabled = false;
                ApplyLife(local, PlayerLifeState.Dead, 3);
                RequireNeutralInput(driver.Consume(3), 3);
                ApplyLife(local, PlayerLifeState.Alive, 4);
                Accumulate(driver, new PlayerInput { r = true });
                Require(driver.Consume(4).reload, "收到本机权威存活状态后应恢复输入。");
            }
            finally
            {
                controller.OnStopLocalPlayer();
                ApplyLife(local, PlayerLifeState.Dead, 5);
                ApplyLife(local, PlayerLifeState.Alive, 6);
                try { RequireNeutralInput(driver.Consume(5), 5); }
                finally { Invoke(driver, "OnDestroy"); }
            }
        }

        private static void Accumulate(PlayerInputDriver driver, PlayerInput input) =>
            typeof(PlayerInputDriver).GetMethod("Accumulate", PrivateInstance).Invoke(driver, new object[] { input });

        private static void ApplyLife(PlayerStatsController stats, PlayerLifeState state, ulong revision) =>
            Require((bool)typeof(PlayerStatsController).GetMethod("ApplyLifeState", PrivateInstance)
                .Invoke(stats, new object[] { state, revision }), "权威生命状态消息应成功应用。");

        private static void RequireNeutralInput(PlayerInputFrame frame, uint tick) =>
            Require(frame.tick == tick && double.IsNaN(frame.viewServerTick) && frame.viewFromTick == 0 &&
                frame.viewToTick == 0 && frame.move == Vector2.zero && frame.look == Vector2.zero && !frame.jump &&
                !frame.fire && !frame.reload && !frame.key1 && !frame.key2 && !frame.key3 &&
                !frame.lightAttackPressed && !frame.heavyAttackPressed,
                "禁用期间应保留模拟拍号，清除全部控制输入与开火时间样本。");

        private static void TestReloadLiveSteps()
        {
            foreach (PawnRole role in new[] { PawnRole.Predicted, PawnRole.Authoritative })
            {
                using var scope = new PreviewScope(role);
                AK47 gun = scope.EquipRifle();
                uint tick = 0;
                int starts = 0;
                scope.Weapon.OnReload += _ => starts++;
                for (uint i = 0; i <= gun.Config.PrepareTicks; i++)
                    scope.Step(new PlayerInputFrame { tick = ++tick });
                int initialRounds = gun.Magazine.Rounds;
                int reserve = gun.Magazine.ReserveRounds;
                PlayerLiveStepResult shot = scope.Step(new PlayerInputFrame { tick = ++tick, fire = true, reload = true });
                Require(shot.Fired && gun.GunState == GunState.Shooting && gun.Magazine.Rounds == initialRounds - 1 &&
                        !shot.WeaponActions.Any(action => action.Kind == PlayerWeaponActionKind.ReloadStarted) &&
                        shot.WeaponActions.All(action => !action.ServerInitiated),
                    role + " 同拍有效开火优先于 R，实际发射一发后维持射击状态。");
                PlayerLiveStepResult reload = scope.Step(new PlayerInputFrame { tick = ++tick, reload = true });
                Require(!reload.Fired && starts == 1 && gun.GunState == GunState.Reloading &&
                        gun.Magazine.Rounds == initialRounds - 1 &&
                        reload.WeaponActions.All(action => !action.ServerInitiated) &&
                        (role == PawnRole.Predicted || reload.WeaponActions.Any(action => action.Kind == PlayerWeaponActionKind.ReloadStarted)),
                    role + " 射击间隔内的 R 应立即成功开始换弹，补弹在完成时提交。");
                for (uint i = 1; i < gun.Config.ReloadTicks; i++)
                {
                    scope.Step(new PlayerInputFrame { tick = ++tick, reload = i == 1 });
                    Require(gun.GunState == GunState.Reloading && gun.Magazine.Rounds == initialRounds - 1 &&
                            gun.Magazine.ReserveRounds == reserve,
                        "换弹截止拍之前应维持原有弹药，重复 R 不重启计时。");
                }
                scope.Step(new PlayerInputFrame { tick = ++tick });
                Require(starts == 1 && gun.GunState == GunState.Normal && gun.Magazine.Rounds == initialRounds &&
                        gun.Magazine.ReserveRounds == reserve - 1,
                    role + " 原截止拍应完成一次装填，弹匣和备弹转移量正确。");
                PlayerLiveStepResult full = scope.Step(new PlayerInputFrame { tick = ++tick, reload = true });
                Require(!full.WeaponActions.Any(action => action.Kind == PlayerWeaponActionKind.ReloadStarted),
                    "满弹匣的 R 请求应保持就绪且没有换弹开始事实。");
                scope.Step(new PlayerInputFrame { tick = ++tick, key3 = true, reload = true });
                Require(gun.GunState == GunState.None && scope.Holder.CurrentSlot == WeaponSlot.Knife,
                    "切刀优先于同拍 R，旧枪应进入未操控状态。");
            }
        }

        private static void TestAuthoritativeActionFacts()
        {
            using var scope = new PreviewScope(PawnRole.Authoritative);
            AK47 gun = scope.EquipRifle();
            uint tick = 0;
            for (uint i = 0; i <= gun.Config.PrepareTicks; i++)
                scope.Step(new PlayerInputFrame { tick = ++tick });
            scope.Step(new PlayerInputFrame { tick = ++tick, fire = true });
            int rounds = gun.Magazine.Rounds;
            int reserve = gun.Magazine.ReserveRounds;
            var logic = new ServerGameLogic();

            GameTickResult Step() => logic.Step(1000 + tick,
                new[] { new ServerPlayerInput(73, 9, scope.Brain, new PlayerInputFrame { tick = ++tick }) });

            Require(gun.TryReload(), "权威 C# 应能在射击期间主动开始换弹。");
            GameTickResult reload = Step();
            Require(reload.Events.Count == 2 && reload.Events[0] is WeaponActionCanceledEvent shooting &&
                    shooting.CanceledState == GunState.Shooting && shooting.ServerInitiated &&
                    reload.Events[1] is ReloadStartedEvent reloadStart && reloadStart.ServerInitiated,
                "拍外权威换弹应依次产生射击动作退出和换弹开始事实。");
            Require(gun.TryReturnToNormal(), "权威 C# 应能主动取消换弹回到 Normal。");
            GameTickResult canceled = Step();
            Require(canceled.Events.Count == 1 && canceled.Events[0] is WeaponActionCanceledEvent reloadCanceled &&
                    reloadCanceled.CanceledState == GunState.Reloading && reloadCanceled.ServerInitiated &&
                    gun.Magazine.Rounds == rounds &&
                    gun.Magazine.ReserveRounds == reserve,
                "主动取消应在下一拍发布一次换弹取消事实并维持弹药。");
            Require(gun.TryPrepare(), "Normal 应能主动重新进入准备。");
            GameTickResult prepare = Step();
            Require(prepare.Events.Count == 1 && prepare.Events[0] is PrepareStartedEvent prepareStart &&
                    prepareStart.ServerInitiated &&
                    gun.GunState == GunState.Preparing && !gun.TryReturnToNormal() && !gun.TryReload() && !gun.TryFire(),
                "同枪重新准备应广播开始事实，并保持准备期间的动作门禁。");
            Require(scope.Holder.SelectSlot(WeaponSlot.Knife), "权威 C# 应能在准备期间主动切刀。");
            GameTickResult switched = Step();
            Require(switched.Events.Count == 3 && switched.Events[0] is WeaponActionCanceledEvent preparingCanceled &&
                    preparingCanceled.CanceledState == GunState.Preparing && preparingCanceled.ServerInitiated &&
                    switched.Events[1] is WeaponSwitchedEvent selected && selected.WeaponSlot == WeaponSlot.Knife &&
                    selected.ServerInitiated && switched.Events[2] is PrepareStartedEvent { WeaponSlot: WeaponSlot.Knife },
                "权威 C# 切枪产生的取消和栏位变化应都标记为服务器主动事实。");
        }

        private static void TestReloadMovementReplay()
        {
            using var scope = new PreviewScope(PawnRole.Predicted);
            AK47 gun = scope.EquipRifle();
            uint tick = 0;
            for (uint i = 0; i <= gun.Config.PrepareTicks; i++)
                scope.Step(new PlayerInputFrame { tick = ++tick });
            scope.Step(new PlayerInputFrame { tick = ++tick, fire = true });
            PlayerSimState baseline = scope.Brain.State;
            var history = new PredictionHistory(8);
            history.Seed(100, baseline);
            int starts = 0;
            scope.Weapon.OnReload += _ => starts++;
            foreach (uint inputTick in new[] { 101u, 102u })
            {
                var frame = new PlayerInputFrame { tick = inputTick, reload = true };
                scope.Step(frame);
                history.Record(frame, scope.Brain.State);
            }
            uint weaponTick = scope.Weapon.LocalTick;
            float progress = gun.ActionProgress;
            int rounds = gun.Magazine.Rounds;
            int reserve = gun.Magazine.ReserveRounds;
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
            Require(result.Status == ReconcileStatus.Replayed && result.ReplayedTicks == 2 && starts == 1 &&
                    scope.Weapon.LocalTick == weaponTick && gun.GunState == GunState.Reloading &&
                    gun.ActionProgress == progress && gun.Magazine.Rounds == rounds && gun.Magazine.ReserveRounds == reserve,
                "运动回放包含 R 的历史输入时，应保持武器时钟、换弹进度、弹药和开始次数。");
        }

        private static void TestReequippedGunClock()
        {
            foreach (PawnRole role in new[] { PawnRole.Predicted, PawnRole.Authoritative })
            {
                using var scope = new PreviewScope(role);
                Require(scope.Holder.InitializeLoadout(), "晚装备验证应先初始化默认刀。");
                uint tick = 0;
                for (uint i = 0; i < 64; i++)
                    scope.Step(new PlayerInputFrame { tick = ++tick });
                AK47 gun = scope.EquipRifle();
                int cooldownEnds = 0;
                int prepareEnds = 0;
                scope.Weapon.OnShotCooldownEnd += _ => cooldownEnds++;
                scope.Weapon.OnPrepareEnd += _ => prepareEnds++;

                void PrepareFromCurrentClock()
                {
                    Require(gun.TryPrepare() && gun.ActionProgress == 0f,
                        role + " 拍间 C# 准备应从当前武器时钟的零进度开始。");
                    for (uint elapsed = 1; elapsed < gun.Config.PrepareTicks; elapsed++)
                    {
                        PlayerLiveStepResult preparing = scope.Step(new PlayerInputFrame { tick = ++tick, fire = true });
                        float expectedProgress = elapsed / (float)gun.Config.PrepareTicks;
                        Require(!preparing.Fired && gun.GunState == GunState.Preparing &&
                                Mathf.Abs(gun.ActionProgress - expectedProgress) < 0.0001f,
                            role + " 下一拍及准备截止拍前应保持准备，进度按本次准备实际经过拍数增长。");
                    }
                    scope.Step(new PlayerInputFrame { tick = ++tick });
                    Require(gun.GunState == GunState.Normal,
                        role + " 拍间开始的准备应在完整 PrepareTicks 后完成。");
                }

                PrepareFromCurrentClock();
                Require(prepareEnds == 1 && scope.Step(new PlayerInputFrame { tick = ++tick, fire = true }).Fired,
                    "晚装备的枪应完成一次准备后正常发射。");
                int rounds = gun.Magazine.Rounds;
                Require(scope.Holder.SelectSlot(WeaponSlot.Knife) && gun.GunState == GunState.None,
                    "射击期间切刀应让旧枪进入未操控状态。");
                for (uint i = 0; i < 64; i++)
                    scope.Step(new PlayerInputFrame { tick = ++tick });
                Require(gun.Magazine.Rounds == rounds && cooldownEnds == 0,
                    "长期收枪应维持弹药，已取消射击周期保持单次取消结果。");
                Require(scope.Holder.SelectSlot(WeaponSlot.Rifle) && ReferenceEquals(scope.Holder.CurrentWeapon, gun),
                    "切回时应选中已有枪械实例。");
                PrepareFromCurrentClock();
                Require(prepareEnds == 2 && cooldownEnds == 0 &&
                        scope.Step(new PlayerInputFrame { tick = ++tick, fire = true }).Fired &&
                        gun.Magazine.Rounds == rounds - 1,
                    role + " 长期收起后切回仍需完整准备，原射击冷却到期后可正常发射。");
            }
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
                PlayerMovementController movementController = Root.GetComponent<PlayerMovementController>();
                Require(movementController != null, "运动模拟必须自动补齐独立的移动控制器。");
                _movement = ScriptableObject.CreateInstance<PlayerMovementDataSO>();
                _movement.hideFlags = HideFlags.HideAndDontSave;
                Set(_movement, "_moveSpeed", 6f);
                Set(_movement, "_jumpSpeed", 5f);
                Set(movementController, "_playerMovementDataSO", _movement);
                Set(movementController, "_gravityScale", 0f);
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
                AK47SO source = AssetDatabase.LoadAssetAtPath<AK47SO>("Assets/SO/GunData/Definitions/AK47SO.asset");
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
