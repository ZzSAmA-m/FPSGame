using System;
using System.Text;
using GamePlay.Player.Simulation;
using Unity.Pipeline.Commands;

namespace FPSGame.Editor
{
    /// <summary>检查长帧补算和新预测的时钟重置。</summary>
    public static class SimulationClockValidation
    {
        [CliCommand("simulation_clock_validate", "Validate fixed-step clock recovery", Tags = new[] { "fpsgame/validation" })]
        public static string RunAll()
        {
            var report = new StringBuilder();
            int passed = 0;
            Run("长帧分帧补算不丢 Tick", TestLongFrame, report, ref passed);
            Run("持续 10 FPS 仍按 60 Hz 模拟", TestSustainedSlowFrames, report, ref passed);
            Run("超出预算的连续低帧保留时间债", TestSustainedOverload, report, ref passed);
            Run("Reset 清空旧债并重置采样时刻", TestReset, report, ref passed);
            Run("PendingTicks 包含尚未 Advance 的时间且不消费", TestPendingBeforeAdvance, report, ref passed);
            Run("时间倒退重新建立起点", TestTimeBackwards, report, ref passed);
            Run("小数步长保留到下一次累计", TestFractionalStep, report, ref passed);
            Run("无效时间不会污染后续模拟", TestInvalidTime, report, ref passed);
            return "PASS " + passed + " checks\n" + report;
        }

        static void TestLongFrame()
        {
            var clock = new SimulationClock();
            clock.Reset(10d);
            clock.Advance(11.2d);
            int simulated = DrainFrame(clock);
            Assert(simulated == SimTime.MaxStepsPerFrame && clock.PendingTicks(11.2d) == 72 - simulated,
                "首帧只能处理预算内的步数。");
            int remainingFrames = (72 + SimTime.MaxStepsPerFrame - 1) / SimTime.MaxStepsPerFrame - 1;
            for (int frame = 0; frame < remainingFrames; frame++)
            {
                clock.Advance(11.2d);
                simulated += DrainFrame(clock);
            }
            Assert(simulated == 72 && !clock.TryStep(), "1.2 秒应最终完整处理 72 拍。");
        }

        static void TestSustainedSlowFrames()
        {
            var clock = new SimulationClock();
            clock.Reset(0d);
            int simulated = 0;
            for (int frame = 1; frame <= 10; frame++)
            {
                clock.Advance(frame * 0.1d);
                simulated += DrainFrame(clock);
            }
            Assert(simulated == 60 && clock.PendingTicks(1d) == 0, "持续 10 FPS 也应在一秒内完成 60 拍。");
        }

        static void TestSustainedOverload()
        {
            var clock = new SimulationClock();
            clock.Reset(0d);
            int simulated = 0;
            for (int frame = 1; frame <= 2; frame++)
            {
                clock.Advance(frame * 0.5d);
                simulated += DrainFrame(clock);
            }
            Assert(simulated == 2 * SimTime.MaxStepsPerFrame && clock.PendingTicks(1d) == 60 - simulated,
                "持续超出预算时不能丢掉尚未处理的时间。");
            while (clock.PendingTicks(1d) > 0)
                simulated += DrainFrame(clock);
            Assert(simulated == 60 && !clock.TryStep(), "帧率恢复后应能补完全部时间。");
        }

        static void TestReset()
        {
            var clock = new SimulationClock();
            clock.Reset(0d);
            clock.Advance(1d);
            DrainFrame(clock);
            clock.Reset(2d);
            clock.Advance(2d);
            Assert(!clock.TryStep() && clock.PendingTicks(2d) == 0, "新基准不能继承旧时间或本帧此前的时间。");
            clock.Advance(2d + SimTime.Dt);
            Assert(clock.TryStep() && !clock.TryStep(), "重置后只计算新起点之后的时间。");
        }

        static void TestPendingBeforeAdvance()
        {
            var clock = new SimulationClock();
            clock.Reset(0d);
            clock.Advance(2d * SimTime.Dt);
            Assert(clock.TryStep(), "应有一拍可消费。");
            Assert(clock.PendingTicks(5d * SimTime.Dt) == 4, "查询应包含未累计的本帧时间。");
            Assert(clock.PendingTicks(5d * SimTime.Dt) == 4, "重复查询不能修改时钟。");
            clock.Advance(5d * SimTime.Dt);
            Assert(DrainFrame(clock) == 4 && !clock.TryStep(), "Advance 不能重复累计查询过的时间。");
        }

        static void TestTimeBackwards()
        {
            var clock = new SimulationClock();
            clock.Reset(10d);
            clock.Advance(11d);
            Assert(clock.PendingTicks(5d) == 0, "旧时钟的数据不能带到倒退后的时间线。");
            clock.Advance(5d);
            Assert(!clock.TryStep(), "时间倒退应清除旧债。");
            clock.Advance(5d + SimTime.Dt);
            Assert(clock.TryStep() && !clock.TryStep(), "倒退后应从新的采样点正常推进。");
        }

        static void TestFractionalStep()
        {
            var clock = new SimulationClock();
            clock.Reset(0d);
            clock.Advance(SimTime.Dt * 0.5d);
            Assert(!clock.TryStep(), "半拍不能提前模拟。");
            clock.Advance(SimTime.Dt);
            Assert(clock.TryStep() && !clock.TryStep(), "两次半拍应合成完整一拍。");
        }

        static void TestInvalidTime()
        {
            var clock = new SimulationClock();
            clock.Reset(0d);
            clock.Advance(double.NaN);
            clock.Advance(double.PositiveInfinity);
            Assert(clock.PendingTicks(double.NaN) == 0 && !clock.TryStep(), "无效时间不能增加待处理步数。");
            clock.Advance(SimTime.Dt);
            Assert(clock.TryStep(), "无效采样之后仍应正常累计。");
            clock.Reset(double.NaN);
            clock.Advance(100d);
            Assert(!clock.TryStep(), "缺少有效起点时首次采样只建立起点。");
        }

        static int DrainFrame(SimulationClock clock)
        {
            int steps = 0;
            while (steps < SimTime.MaxStepsPerFrame && clock.TryStep())
                steps++;
            return steps;
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
                throw new InvalidOperationException("模拟时钟验证失败：" + name, exception);
            }
        }

        static void Assert(bool condition, string message)
        {
            if (!condition)
                throw new InvalidOperationException(message);
        }
    }
}
