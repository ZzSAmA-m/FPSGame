using System;
using System.Collections.Generic;
using System.Text;
using GamePlay.Player.Simulation;
using GamePlay.Player.Simulation.Reconciliation;
using Network.Server;
using UnityEditor;
using UnityEngine;

namespace FPSGame.Editor
{
    /// <summary>不用测试框架也能执行的回滚回归检查。</summary>
    public static class RollbackValidation
    {
        [MenuItem("Tools/FPSGame/Validate Rollback")]
        static void RunFromMenu()
        {
            Debug.Log(RunAll());
        }

        public static string RunAll()
        {
            var report = new StringBuilder();
            int passed = 0;
            Run("Seed 不进入输入发送窗口", TestSeedIsNotInput, report, ref passed);
            Run("环形覆盖不会读到旧槽位", TestRingOverwrite, report, ref passed);
            Run("历史清理保留确认锚点", TestHistoryPruning, report, ref passed);
            Run("历史支持 Tick 回绕", TestHistoryWrap, report, ref passed);
            Run("重放支持 Tick 回绕", TestReplayWrap, report, ref passed);
            Run("同拍匹配不重新模拟", TestMatchedDoesNotStep, report, ref passed);
            Run("纠偏从下一拍开始并改写历史", TestReplayUpdatesHistory, report, ref passed);
            Run("确认当前拍只恢复不多算", TestCurrentTickCorrection, report, ref passed);
            Run("接地状态不同也触发纠偏", TestGroundedMismatch, report, ref passed);
            Run("竖直速度不同也触发纠偏", TestVerticalVelocityMismatch, report, ref passed);
            Run("角度比较处理 360 度环绕", TestEquivalentAngles, report, ref passed);
            Run("容差内继续预测", TestTolerance, report, ref passed);
            Run("缺输入在修改现场前拒绝重放", TestMissingInput, report, ref passed);
            Run("未来确认在修改现场前要求重同步", TestFutureAcknowledgement, report, ref passed);
            Run("重放超预算不执行部分重放", TestReplayBudget, report, ref passed);
            Run("缺确认锚点要求重同步", TestMissingAnchor, report, ref passed);
            Run("无效权威数值不进入模拟", TestInvalidAuthority, report, ref passed);
            Run("队列只在 Commit 后确认", TestQueueCommitBoundary, report, ref passed);
            Run("缺包使用新 Tick 并清除边沿", TestQueueMissingInput, report, ref passed);
            Run("迟到输入不能覆盖已完成拍", TestQueueRejectsLateInput, report, ref passed);
            Run("未来输入窗口有界且不跳确认", TestQueueFutureWindow, report, ref passed);
            Run("队列积压不会凭空推进确认", TestQueueBacklog, report, ref passed);
            Run("真实输入恢复后重置缺包计数", TestQueueRealInputAfterGap, report, ref passed);
            Run("错误 Commit 不改变确认进度", TestQueueInvalidCommit, report, ref passed);
            Run("队列支持 Tick 回绕", TestQueueWrap, report, ref passed);
            Run("输入积压独立于状态误差检查", TestQueueLeadLimit, report, ref passed);
            Run("补算时间不会误判为输入积压", TestQueueLeadDuringCatchUp, report, ref passed);
            Run("超出队列容量也能触发重同步", TestQueueLeadBeyondCapacity, report, ref passed);
            Run("旧输入和无效输入不触发重同步", TestQueueLeadRejectsInvalid, report, ref passed);
            Run("输入积压判断支持 Tick 回绕", TestQueueLeadWrap, report, ref passed);
            return "PASS " + passed + " checks\n" + report;
        }

        static void Run(string name, Action test, StringBuilder report, ref int passed)
        {
            try
            {
                test();
                passed++;
                report.Append("PASS ").Append(name).Append('\n');
            }
            catch (Exception exception)
            {
                throw new InvalidOperationException("回滚验证失败：" + name, exception);
            }
        }

        static void TestSeedIsNotInput()
        {
            var history = new PredictionHistory(8);
            history.Seed(0, InitialState());
            Assert(history.Count == 1, "基准应作为历史锚点保存。");
            Assert(history.CopyRecentInputs(12).Length == 0, "基准不能作为真实输入发送。");
            var simulation = new FakeSimulation(InitialState());
            RecordStep(history, simulation, 1);
            RecordStep(history, simulation, 2);
            PlayerInputFrame[] inputs = history.CopyRecentInputs(12);
            Assert(inputs.Length == 2 && inputs[0].tick == 1 && inputs[1].tick == 2,
                "发送窗口只能包含按序的真实输入。");
        }

        static void TestRingOverwrite()
        {
            var history = new PredictionHistory(8);
            var simulation = new FakeSimulation(InitialState());
            history.Seed(0, simulation.State);
            uint latest = (uint)history.Capacity + 2;
            for (uint tick = 1; tick <= latest; tick++)
                RecordStep(history, simulation, tick);
            Assert(!history.TryGet(0, out _), "被覆盖的基准不能继续读取。");
            Assert(history.TryGet(latest, out PredictionFrame frame) && frame.Input.tick == latest,
                "最新槽位必须对应完整 Tick。");
            Assert(history.Count <= history.Capacity, "历史长度不能超过容量。");
            PlayerInputFrame[] inputs = history.CopyRecentInputs(3);
            Assert(inputs.Length == 3 && inputs[0].tick == latest - 2 && inputs[2].tick == latest,
                "覆盖后发送窗口仍需保持顺序。");
        }

        static void TestHistoryPruning()
        {
            var fixture = new Fixture(3);
            fixture.History.DiscardBefore(2);
            Assert(!fixture.History.TryGet(1, out _), "早于确认拍的历史应回收。");
            Assert(fixture.History.TryGet(2, out _) && fixture.History.TryGet(3, out _),
                "确认锚点和之后的输入必须保留。");
        }

        static void TestHistoryWrap()
        {
            var history = new PredictionHistory(5);
            var simulation = new FakeSimulation(InitialState());
            history.Seed(uint.MaxValue - 2, simulation.State);
            RecordStep(history, simulation, uint.MaxValue - 1);
            RecordStep(history, simulation, uint.MaxValue);
            RecordStep(history, simulation, 0);
            RecordStep(history, simulation, 1);
            RecordStep(history, simulation, 2);
            Assert(!history.TryGet(uint.MaxValue - 2, out _) && history.HasInputRange(uint.MaxValue - 1, 2),
                "非 2 次幂容量在回绕后也应正确覆盖旧槽位。");
            PlayerInputFrame[] inputs = history.CopyRecentInputs(3);
            Assert(inputs.Length == 3 && inputs[0].tick == 0 && inputs[1].tick == 1 && inputs[2].tick == 2,
                "回绕后的发送窗口顺序不正确。");
            history.DiscardBefore(0);
            Assert(!history.TryGet(uint.MaxValue, out _) && history.TryGet(0, out _), "回绕后的清理边界不正确。");
            Assert(TickMath.IsNewer(0, uint.MaxValue) && TickMath.IsOlder(uint.MaxValue, 0),
                "序号比较必须处理 uint 回绕。");
        }

        static void TestReplayWrap()
        {
            var history = new PredictionHistory(8);
            var simulation = new FakeSimulation(InitialState());
            history.Seed(uint.MaxValue - 1, simulation.State);
            RecordStep(history, simulation, uint.MaxValue);
            history.TryGet(uint.MaxValue, out PredictionFrame anchor);
            RecordStep(history, simulation, 0);
            RecordStep(history, simulation, 1);
            simulation.ClearObservations();
            var authority = new PlayerAuthoritativeSnapshot
            {
                Epoch = 1,
                ServerTick = 100,
                StateInputTick = uint.MaxValue,
                State = anchor.PostState
            };
            authority.State.position.x += 10f;
            var reconciler = new PlayerReconciler(simulation, history, new ReconciliationSettings());
            ReconcileResult result = reconciler.Reconcile(authority, 1);
            Assert(result.Status == ReconcileStatus.Replayed && result.ReplayedTicks == 2,
                "跨回绕纠偏只应重放第 0、1 拍。");
            Assert(simulation.SteppedTicks[0] == 0 && simulation.SteppedTicks[1] == 1,
                "跨回绕重放的顺序不正确。");
            Near(simulation.State.position.x, 13f, "跨回绕的最终状态不正确。");
        }

        static void TestMatchedDoesNotStep()
        {
            var fixture = new Fixture(3);
            ReconcileResult result = fixture.Reconcile(fixture.Snapshot(1), 3);
            Assert(result.Status == ReconcileStatus.Matched, "应比较历史第 1 拍，而不是当前第 3 拍。");
            Assert(fixture.Simulation.Steps == 0 && fixture.Simulation.Restores == 0,
                "匹配快照不能触发额外模拟或恢复。");
            Assert(result.AcknowledgedTick == 1, "匹配也应返回确认进度。");
            Near(fixture.Simulation.State.position.x, 3f, "匹配时不应改变当前状态。");
        }

        static void TestReplayUpdatesHistory()
        {
            var fixture = new Fixture(3);
            PlayerAuthoritativeSnapshot snapshot = fixture.Snapshot(1);
            snapshot.State.position.x += 10f;
            ReconcileResult result = fixture.Reconcile(snapshot, 3);
            Assert(result.Status == ReconcileStatus.Replayed && result.ReplayedTicks == 2,
                "只应重放第 2、3 拍。");
            Assert(fixture.Simulation.Restores == 1 && fixture.Simulation.Steps == 2,
                "应先恢复一次，再重放两次。");
            Assert(fixture.Simulation.SteppedTicks[0] == 2 && fixture.Simulation.SteppedTicks[1] == 3,
                "确认拍不能再次执行。");
            Near(fixture.Simulation.State.position.x, 13f, "最终模拟状态应包含权威纠正和后续输入。");
            CheckHistoryPosition(fixture.History, 1, 11f);
            CheckHistoryPosition(fixture.History, 2, 12f);
            CheckHistoryPosition(fixture.History, 3, 13f);
        }

        static void TestCurrentTickCorrection()
        {
            var fixture = new Fixture(3);
            PlayerAuthoritativeSnapshot snapshot = fixture.Snapshot(3);
            snapshot.State.position.x += 1f;
            ReconcileResult result = fixture.Reconcile(snapshot, 3);
            Assert(result.Status == ReconcileStatus.Replayed && result.ReplayedTicks == 0,
                "确认当前拍时应直接恢复权威状态。");
            Assert(fixture.Simulation.Restores == 1 && fixture.Simulation.Steps == 0,
                "不能在当前拍纠偏后额外执行已确认输入。");
            Near(fixture.Simulation.State.position.x, 4f, "当前拍纠偏位置不正确。");
        }

        static void TestGroundedMismatch()
        {
            var fixture = new Fixture(1);
            PlayerAuthoritativeSnapshot snapshot = fixture.Snapshot(1);
            snapshot.State.grounded = false;
            Assert(fixture.Reconcile(snapshot, 1).Status == ReconcileStatus.Replayed,
                "位置相同但接地状态不同也需要纠偏。");
            Assert(!fixture.Simulation.State.grounded, "接地状态必须完整恢复。");
        }

        static void TestVerticalVelocityMismatch()
        {
            var fixture = new Fixture(1);
            PlayerAuthoritativeSnapshot snapshot = fixture.Snapshot(1);
            snapshot.State.verticalVelocity = 2f;
            Assert(fixture.Reconcile(snapshot, 1).Status == ReconcileStatus.Replayed,
                "独立竖直速度不能漏掉比较。");
            Near(fixture.Simulation.State.verticalVelocity, 2f, "竖直速度必须恢复。");
        }

        static void TestEquivalentAngles()
        {
            var fixture = new Fixture(1);
            PlayerAuthoritativeSnapshot snapshot = fixture.Snapshot(1);
            snapshot.State.yaw += 360f;
            Assert(fixture.Reconcile(snapshot, 1).Status == ReconcileStatus.Matched,
                "相差 360 度的同向朝向不能触发回滚。");
            Assert(fixture.Simulation.Restores == 0, "等价角度无需恢复。");
        }

        static void TestTolerance()
        {
            var fixture = new Fixture(1);
            PlayerAuthoritativeSnapshot snapshot = fixture.Snapshot(1);
            snapshot.State.position.x += fixture.Settings.positionTolerance * 0.5f;
            snapshot.State.velocity.y += fixture.Settings.velocityTolerance * 0.5f;
            snapshot.State.verticalVelocity += fixture.Settings.velocityTolerance * 0.5f;
            snapshot.State.pitch += fixture.Settings.angleTolerance * 0.5f;
            Assert(fixture.Reconcile(snapshot, 1).Status == ReconcileStatus.Matched,
                "各项误差都在容差内时应继续预测。");
            Assert(fixture.Simulation.Restores == 0 && fixture.Simulation.Steps == 0,
                "小误差不能产生重放开销。");
        }

        static void TestMissingInput()
        {
            var fixture = new Fixture(0);
            RecordStep(fixture.History, fixture.Simulation, 1);
            RecordStep(fixture.History, fixture.Simulation, 3);
            fixture.Simulation.ClearObservations();
            PlayerAuthoritativeSnapshot snapshot = fixture.Snapshot(0);
            snapshot.State.position.x = 10f;
            AssertPreflightFailure(fixture, snapshot, 3);
        }

        static void TestFutureAcknowledgement()
        {
            var fixture = new Fixture(2);
            PlayerAuthoritativeSnapshot snapshot = fixture.Snapshot(2);
            snapshot.StateInputTick = 3;
            AssertPreflightFailure(fixture, snapshot, 2);
        }

        static void TestReplayBudget()
        {
            var fixture = new Fixture(3);
            fixture.Settings.maxReplayTicks = 2;
            PlayerAuthoritativeSnapshot snapshot = fixture.Snapshot(0);
            snapshot.State.position.x = 10f;
            AssertPreflightFailure(fixture, snapshot, 3);
        }

        static void TestMissingAnchor()
        {
            var fixture = new Fixture(3);
            PlayerAuthoritativeSnapshot snapshot = fixture.Snapshot(1);
            fixture.History.DiscardBefore(2);
            AssertPreflightFailure(fixture, snapshot, 3);
        }

        static void TestInvalidAuthority()
        {
            var fixture = new Fixture(1);
            PlayerAuthoritativeSnapshot snapshot = fixture.Snapshot(1);
            snapshot.State.position.x = float.NaN;
            Assert(fixture.Reconcile(snapshot, 1).Status == ReconcileStatus.Ignored,
                "非有限权威状态必须被拒绝。");
            Assert(fixture.Simulation.Restores == 0 && fixture.Simulation.Steps == 0,
                "无效状态不能触发物理操作。");
        }

        static void TestQueueCommitBoundary()
        {
            var queue = new PlayerInputQueue();
            queue.Reset();
            PlayerInputFrame input = ActiveInput(1);
            queue.Push(input);
            Assert(queue.HasPending && queue.LastProcessedTick == 0, "接收输入不能直接确认。");
            PlayerInputFrame resolved = queue.ResolveNext();
            Assert(resolved.tick == 1 && queue.LastProcessedTick == 0, "解析输入不能提前确认。");
            Assert(queue.ResolveNext().tick == 1, "同一待提交拍应稳定返回。");
            queue.Commit(resolved.tick);
            Assert(queue.LastProcessedTick == 1, "模拟完成后 Commit 才推进确认。");
        }

        static void TestQueueMissingInput()
        {
            var queue = new PlayerInputQueue();
            queue.Reset();
            queue.Push(ActiveInput(1));
            queue.Commit(queue.ResolveNext().tick);
            for (int missing = 1; missing <= PlayerInputQueue.MissingMoveHoldTicks + 1; missing++)
            {
                PlayerInputFrame input = queue.ResolveNext();
                Assert(input.tick == (uint)missing + 1, "缺包也必须占用新的输入 Tick。");
                Assert(input.look == Vector2.zero && !input.jump && !input.fire && !input.key1 && !input.key2 && !input.key3,
                    "缺包替代输入不能重复鼠标和按键边沿。");
                Vector2 expected = missing <= PlayerInputQueue.MissingMoveHoldTicks ? Vector2.right : Vector2.zero;
                Assert(input.move == expected, "连续缺包时移动应在短暂保持后停止。");
                Assert(queue.LastProcessedTick == input.tick - 1, "替代输入也要提交后才确认。");
                queue.Commit(input.tick);
            }
        }

        static void TestQueueRejectsLateInput()
        {
            var queue = new PlayerInputQueue();
            queue.Reset();
            queue.Push(ActiveInput(1));
            queue.Commit(queue.ResolveNext().tick);
            PlayerInputFrame late = ActiveInput(1);
            late.move = Vector2.left;
            queue.Push(late);
            Assert(!queue.HasPending, "已经完成的拍不能重新入队。");
            PlayerInputFrame next = queue.ResolveNext();
            Assert(next.tick == 2 && next.move == Vector2.right && !next.fire,
                "迟到包不能改变已完成输入的替代状态。");
        }

        static void TestQueueFutureWindow()
        {
            var queue = new PlayerInputQueue();
            queue.Reset();
            queue.Push(ActiveInput((uint)PlayerInputQueue.MaxFutureTicks + 1));
            queue.Push(ActiveInput(uint.MaxValue));
            Assert(!queue.HasPending && queue.LastProcessedTick == 0,
                "超远未来包和旧包不能填充队列或推进确认。");
            queue.Push(ActiveInput((uint)PlayerInputQueue.MaxFutureTicks));
            Assert(queue.HasPending && queue.LastProcessedTick == 0, "窗口边界内的未来输入应保存但不确认。");
            Assert(queue.ResolveNext().tick == 1, "队列不能跳到未来已到达的输入。");
        }

        static void TestQueueBacklog()
        {
            var queue = new PlayerInputQueue();
            queue.Reset();
            for (uint tick = 1; tick <= PlayerInputQueue.MaxFutureTicks; tick++)
                queue.Push(ActiveInput(tick));
            Assert(queue.LastProcessedTick == 0, "积压或清理不能凭空确认输入。");
            Assert(queue.ResolveNext().tick == 1, "积压时仍应从下一拍依序消费。");
        }

        static void TestQueueRealInputAfterGap()
        {
            var queue = new PlayerInputQueue();
            queue.Reset();
            queue.Push(ActiveInput(1));
            queue.Commit(queue.ResolveNext().tick);
            for (int i = 0; i <= PlayerInputQueue.MissingMoveHoldTicks; i++)
                queue.Commit(queue.ResolveNext().tick);
            PlayerInputFrame real = ActiveInput(queue.LastProcessedTick + 1);
            real.move = Vector2.up;
            queue.Push(real);
            PlayerInputFrame resolved = queue.ResolveNext();
            Assert(resolved.move == Vector2.up && resolved.fire && resolved.look != Vector2.zero,
                "恢复的真实输入必须完整保留。");
            queue.Commit(resolved.tick);
            Assert(queue.ResolveNext().move == Vector2.up, "真实输入应重新开始短暂缺包保持。");
        }

        static void TestQueueInvalidCommit()
        {
            var queue = new PlayerInputQueue();
            queue.Reset();
            ExpectInvalidOperation(() => queue.Commit(1));
            Assert(queue.LastProcessedTick == 0, "未解析就提交不能改变确认。");
            queue.ResolveNext();
            ExpectInvalidOperation(() => queue.Commit(2));
            Assert(queue.LastProcessedTick == 0, "跳拍提交不能改变确认。");
            queue.Commit(1);
            ExpectInvalidOperation(() => queue.Commit(1));
            Assert(queue.LastProcessedTick == 1, "重复提交不能多推进一拍。");
        }

        static void TestQueueWrap()
        {
            var queue = new PlayerInputQueue();
            queue.Reset(uint.MaxValue - 1);
            queue.Push(ActiveInput(uint.MaxValue));
            queue.Push(ActiveInput(0));
            Assert(queue.ResolveNext().tick == uint.MaxValue, "回绕前的下一拍不正确。");
            queue.Commit(uint.MaxValue);
            Assert(queue.ResolveNext().tick == 0, "Tick 0 在回绕后必须是合法输入。");
            queue.Commit(0);
            queue.Push(ActiveInput(uint.MaxValue));
            Assert(!queue.HasPending && queue.ResolveNext().tick == 1,
                "回绕后应拒绝旧 Tick 并继续向前。");
        }

        static void TestQueueLeadLimit()
        {
            var queue = new PlayerInputQueue();
            queue.Reset(100);
            var idle = new PlayerInputFrame { tick = 100 + PlayerInputQueue.MaxBufferedInputTicks };
            Assert(!queue.NeedsResync(idle, 0), "允许正常的短暂输入缓冲。");
            idle.tick++;
            Assert(queue.NeedsResync(idle, 0), "静止输入排队过长也应恢复，不能等待位置出现误差。");
            Assert(queue.LastProcessedTick == 100, "检测积压不能跳过未执行输入。");
        }

        static void TestQueueLeadDuringCatchUp()
        {
            var queue = new PlayerInputQueue();
            queue.Reset(100);
            var frame = new PlayerInputFrame { tick = 100 + 60 + PlayerInputQueue.MaxBufferedInputTicks };
            Assert(!queue.NeedsResync(frame, 60), "应先允许服务器补算尚未处理的一秒。");
            Assert(queue.NeedsResync(frame, 0), "没有待补算时间时不能持续保留同样的输入积压。");
            Assert(!queue.NeedsResync(frame, uint.MaxValue), "补算预算加缓冲不能溢出。");
        }

        static void TestQueueLeadBeyondCapacity()
        {
            var queue = new PlayerInputQueue();
            queue.Reset();
            var frame = new PlayerInputFrame { tick = PlayerInputQueue.MaxFutureTicks + 100 };
            queue.Push(frame);
            Assert(!queue.HasPending && queue.NeedsResync(frame, 0),
                "被队列拒绝的远期有效输入仍应触发重建基准。");
        }

        static void TestQueueLeadRejectsInvalid()
        {
            var queue = new PlayerInputQueue();
            queue.Reset(100);
            Assert(!queue.NeedsResync(ActiveInput(99), 0), "迟到输入应直接丢弃。");
            var invalid = ActiveInput(200);
            invalid.look.x = float.NaN;
            Assert(!queue.NeedsResync(invalid, 0), "无效输入不能改变预测轮次。");
        }

        static void TestQueueLeadWrap()
        {
            var queue = new PlayerInputQueue();
            queue.Reset(uint.MaxValue - 5);
            Assert(!queue.NeedsResync(ActiveInput(0), 0), "回绕后的第 0 拍只领先 6 拍。");
            Assert(queue.NeedsResync(ActiveInput(20), 0), "回绕后仍应识别过长积压。");
        }

        static void AssertPreflightFailure(Fixture fixture, PlayerAuthoritativeSnapshot snapshot, uint currentTick)
        {
            PlayerSimState before = fixture.Simulation.State;
            ReconcileResult result = fixture.Reconcile(snapshot, currentTick);
            Assert(result.NeedsResync, "无法完整重放时应要求重同步。");
            Assert(fixture.Simulation.Restores == 0 && fixture.Simulation.Steps == 0,
                "必须先完成预检，不能先恢复或只重放一部分。");
            Assert(fixture.Simulation.State.position == before.position,
                "预检失败时现场状态必须保持完整。");
        }

        static void RecordStep(PredictionHistory history, FakeSimulation simulation, uint tick)
        {
            var input = new PlayerInputFrame { tick = tick, move = Vector2.right };
            simulation.Step(input);
            history.Record(input, simulation.State);
        }

        static PlayerInputFrame ActiveInput(uint tick)
        {
            return new PlayerInputFrame
            {
                tick = tick,
                move = Vector2.right,
                look = Vector2.one,
                jump = true,
                fire = true,
                key1 = true,
                key2 = true,
                key3 = true
            };
        }

        static PlayerSimState InitialState()
        {
            return new PlayerSimState { grounded = true };
        }

        static void CheckHistoryPosition(PredictionHistory history, uint tick, float expected)
        {
            Assert(history.TryGet(tick, out PredictionFrame frame), "纠偏后的历史拍缺失：" + tick);
            Near(frame.PostState.position.x, expected, "历史拍末状态没有更新：" + tick);
        }

        static void ExpectInvalidOperation(Action operation)
        {
            try
            {
                operation();
            }
            catch (InvalidOperationException)
            {
                return;
            }
            throw new InvalidOperationException("非法操作应被明确拒绝。");
        }

        static void Assert(bool condition, string message)
        {
            if (!condition)
                throw new InvalidOperationException(message);
        }

        static void Near(float actual, float expected, string message)
        {
            Assert(Mathf.Abs(actual - expected) < 0.0001f,
                message + " actual=" + actual + " expected=" + expected);
        }

        sealed class Fixture
        {
            public readonly ReconciliationSettings Settings = new ReconciliationSettings();
            public readonly PredictionHistory History = new PredictionHistory(16);
            public readonly FakeSimulation Simulation = new FakeSimulation(InitialState());

            public Fixture(uint ticks)
            {
                History.Seed(0, Simulation.State);
                for (uint tick = 1; tick <= ticks; tick++)
                    RecordStep(History, Simulation, tick);
                Simulation.ClearObservations();
            }

            public PlayerAuthoritativeSnapshot Snapshot(uint tick)
            {
                Assert(History.TryGet(tick, out PredictionFrame frame), "测试快照的历史不存在。");
                return new PlayerAuthoritativeSnapshot
                {
                    Epoch = 1,
                    ServerTick = tick + 100,
                    StateInputTick = tick,
                    State = frame.PostState
                };
            }

            public ReconcileResult Reconcile(PlayerAuthoritativeSnapshot snapshot, uint currentTick)
            {
                return new PlayerReconciler(Simulation, History, Settings).Reconcile(snapshot, currentTick);
            }
        }

        sealed class FakeSimulation : IPlayerSimulation
        {
            public PlayerSimState State { get; private set; }
            public int Steps { get; private set; }
            public int Restores { get; private set; }
            public readonly List<uint> SteppedTicks = new List<uint>();

            public FakeSimulation(PlayerSimState initial)
            {
                State = initial;
            }

            public PlayerStepResult Step(in PlayerInputFrame input)
            {
                PlayerSimState state = State;
                state.velocity = new Vector3(input.move.x, 0f, input.move.y);
                state.position += state.velocity;
                state.yaw += input.look.x;
                state.pitch += input.look.y;
                if (input.jump)
                    state.grounded = false;
                State = state;
                Steps++;
                SteppedTicks.Add(input.tick);
                return new PlayerStepResult { fired = input.fire, jumped = input.jump };
            }

            public void RestoreState(in PlayerSimState state)
            {
                State = state;
                Restores++;
            }

            public void ClearObservations()
            {
                Steps = 0;
                Restores = 0;
                SteppedTicks.Clear();
            }
        }
    }
}
