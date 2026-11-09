using System;
using System.Collections.Generic;
using System.Text;
using Core.Utility;
using GamePlay.Player.MotionFSM;
using Unity.Pipeline.Commands;
using UnityEngine;

namespace FPSGame.Editor.Validation
{
    /// <summary>不创建角色、Animator 或模型，验证动作状态机的语义事件与生命周期。</summary>
    public static class PlayerMotionValidation
    {
        [CliCommand("player_motion_validate", "Validate presentation-independent player motion events", Tags = new[] { "fpsgame/validation" })]
        public static string Run()
        {
            var report = new StringBuilder();
            int passed = 0;
            RunCase("无表现组件也能启动并发布动作事件", TestStandaloneMachine, report, ref passed);
            RunCase("TP 待机与四方向移动只在变化时通知", TestLocomotion, report, ref passed);
            RunCase("视角切换保留装备姿态且同视角幂等", TestViews, report, ref passed);
            RunCase("装备、同类换装及卸下均发布持握动作", TestEquipment, report, ref passed);
            RunCase("真实装备屏蔽旧模型切换按键", TestWeaponInput, report, ref passed);
            RunCase("停止后不发布，反复启动不累积内部订阅", TestStopAndRestart, report, ref passed);
            RunCase("普通事件监听可以独立订阅与退订", TestSubscribers, report, ref passed);
            RunCase("不同角色的状态与事件互不影响", TestIndependentMachines, report, ref passed);
            RunCase("无效移动阈值被拒绝", TestInvalidThresholds, report, ref passed);
            return "PASS " + passed + " checks\n" + report;
        }

        private static void TestStandaloneMachine()
        {
            var machine = new PlayerMotionStateMachine();
            var views = new List<EPlayerModelType>();
            var poses = new List<PlayerHoldPose>();
            machine.OnViewChanged += views.Add;
            machine.OnHoldPoseChanged += poses.Add;
            Require(!machine.IsStarted && machine.CurState == null, "构造状态机不应自动开始角色动作。");
            machine.SetView(EPlayerModelType.TP);
            Require(machine.IsStarted && machine.CurState != null && machine.View == EPlayerModelType.TP,
                "不提供 Animator 或模型也应能进入 TP 状态。");
            Require(views.Count == 1 && views[0] == EPlayerModelType.TP &&
                    poses.Count == 1 && poses[0] == PlayerHoldPose.Knife,
                "首次进入必须让独立订阅者获知视角和初始持握姿态。");
            machine.Tick(Vector3.zero);
            machine.PhysicsTick();
            machine.Stop();
            Require(!machine.IsStarted && machine.CurState == null, "停止后应退出整个状态树。");
        }

        private static void TestLocomotion()
        {
            var machine = new PlayerMotionStateMachine(2f);
            var directions = new List<EStandRunState>();
            machine.OnLocomotionChanged += directions.Add;
            machine.SetView(EPlayerModelType.TP);
            Require(machine.Locomotion == EStandRunState.NONE, "TP 初始应为待机。");
            directions.Clear();

            // 状态树允许第一拍从 Idle 进入 Run，下一拍决定方向。
            AdvanceDirection(machine, new Vector3(0f, 0f, 3f));
            Require(directions.Count == 1 && directions[0] == EStandRunState.N, "前向移动应只发布一次 N。");
            for (int i = 0; i < 20; i++) machine.Tick(new Vector3(0f, 0f, 3f));
            Require(directions.Count == 1, "方向未变时不能逐帧要求重播移动动画。");

            AdvanceDirection(machine, new Vector3(3f, 0f, -3f));
            AdvanceDirection(machine, new Vector3(3f, 0f, 0f));
            AdvanceDirection(machine, new Vector3(-3f, 0f, 0f));
            AdvanceDirection(machine, Vector3.zero);
            Require(directions.Count == 5 && directions[1] == EStandRunState.S &&
                    directions[2] == EStandRunState.E && directions[3] == EStandRunState.W &&
                    directions[4] == EStandRunState.NONE,
                "本地速度应依次得到 S、E、W、待机；斜向优先使用 z 方向。");
            for (int i = 0; i < 20; i++) machine.Tick(Vector3.zero);
            Require(directions.Count == 5, "持续待机也不能重复发布动作事件。");
            machine.Stop();
        }

        private static void TestViews()
        {
            var machine = new PlayerMotionStateMachine();
            int views = 0;
            int poses = 0;
            machine.OnViewChanged += _ => views++;
            machine.OnHoldPoseChanged += _ => poses++;
            machine.SetEquipment(true, PlayerHoldPose.Rifle);
            Require(views == 0 && poses == 0, "未启动时装备参数应保存到状态机，而非请求播放。");
            machine.SetView(EPlayerModelType.TP);
            machine.SetView(EPlayerModelType.FP);
            Require(machine.View == EPlayerModelType.FP && machine.HoldPose == PlayerHoldPose.Rifle && views == 2 && poses == 2,
                "进入 FP 应保留当前步枪姿态并通知表现层。");
            machine.SetView(EPlayerModelType.FP);
            Require(views == 2 && poses == 2, "重复设置当前视角不应重播拔枪。");
            machine.SetView(EPlayerModelType.TP);
            Require(machine.View == EPlayerModelType.TP && machine.HoldPose == PlayerHoldPose.Rifle && views == 3 && poses == 3,
                "切回 TP 后应再次进入正确持握动作。");
            machine.Stop();
        }

        private static void TestEquipment()
        {
            var machine = new PlayerMotionStateMachine();
            var poses = new List<PlayerHoldPose>();
            machine.OnHoldPoseChanged += poses.Add;
            machine.SetView(EPlayerModelType.TP);
            poses.Clear();
            machine.SetEquipment(true, PlayerHoldPose.Rifle);
            machine.SetEquipment(true, PlayerHoldPose.Rifle);
            Require(machine.HasEquippedWeapon && poses.Count == 2 && poses[0] == PlayerHoldPose.Rifle && poses[1] == PlayerHoldPose.Rifle,
                "两次装备即使姿态相同，也应各通知一次拔枪与模型更新。");
            machine.SetEquipment(false, PlayerHoldPose.Knife);
            Require(!machine.HasEquippedWeapon && machine.HoldPose == PlayerHoldPose.Knife && poses.Count == 3 && poses[2] == PlayerHoldPose.Knife,
                "卸下武器必须通知持握姿态及真实装备标记发生变化。");
            machine.Stop();
        }

        private static void TestWeaponInput()
        {
            var machine = new PlayerMotionStateMachine();
            int notifications = 0;
            machine.OnHoldPoseChanged += _ => notifications++;
            machine.SetView(EPlayerModelType.FP);
            machine.HandleWeaponInput(WeaponInputAction.Key1);
            Require(machine.HoldPose == PlayerHoldPose.Rifle, "无真实装备时 Key1 保留旧的步枪动作调试入口。");
            machine.HandleWeaponInput(WeaponInputAction.Key3);
            Require(machine.HoldPose == PlayerHoldPose.Knife, "无真实装备时 Key3 保留旧的持刀动作调试入口。");
            machine.SetEquipment(true, PlayerHoldPose.Rifle);
            int before = notifications;
            machine.HandleWeaponInput(WeaponInputAction.Key3);
            machine.HandleWeaponInput(WeaponInputAction.Key1);
            machine.HandleWeaponInput(WeaponInputAction.Key2);
            machine.HandleWeaponInput(WeaponInputAction.MLeft);
            Require(machine.HoldPose == PlayerHoldPose.Rifle && notifications == before,
                "旧按键不得覆盖真实装备的动作或请求重复播放。");
            machine.Stop();
        }

        private static void TestStopAndRestart()
        {
            var machine = new PlayerMotionStateMachine();
            int notifications = 0;
            machine.OnViewChanged += _ => notifications++;
            machine.OnLocomotionChanged += _ => notifications++;
            machine.OnHoldPoseChanged += _ => notifications++;
            for (int i = 0; i < 5; i++)
            {
                machine.SetView(i % 2 == 0 ? EPlayerModelType.FP : EPlayerModelType.TP);
                machine.Stop();
            }
            int stopped = notifications;
            machine.Tick(Vector3.forward * 5f);
            machine.PhysicsTick();
            machine.HandleWeaponInput(WeaponInputAction.Key1);
            machine.SetEquipment(true, PlayerHoldPose.Rifle);
            Require(notifications == stopped && !machine.IsStarted && machine.CurState == null,
                "停止后输入、Tick 和装备信息不得调用已退出的状态或表现监听者。");

            machine.SetEquipment(false, PlayerHoldPose.Knife);
            machine.SetView(EPlayerModelType.FP);
            int before = notifications;
            machine.HandleWeaponInput(WeaponInputAction.Key1);
            Require(notifications == before + 1 && machine.HoldPose == PlayerHoldPose.Rifle,
                "多次重启后一次输入只能产生一次动作通知，不能累积内部订阅。");
            machine.Stop();
        }

        private static void TestSubscribers()
        {
            var machine = new PlayerMotionStateMachine();
            int first = 0;
            int second = 0;
            Action<PlayerHoldPose> onFirst = _ => first++;
            Action<PlayerHoldPose> onSecond = _ => second++;
            machine.OnHoldPoseChanged += onFirst;
            machine.OnHoldPoseChanged += onSecond;
            machine.SetView(EPlayerModelType.TP);
            Require(first == 1 && second == 1, "同一普通 event 应向两个表现订阅者通知相同状态。");
            machine.OnHoldPoseChanged -= onFirst;
            machine.SetEquipment(true, PlayerHoldPose.Rifle);
            Require(first == 1 && second == 2, "一个表现组件退订不能影响其他订阅者。");
            machine.OnHoldPoseChanged += onFirst;
            Require(first == 1 && second == 2, "重新订阅本身不能向其他组件广播重播。");
            machine.SetEquipment(false, PlayerHoldPose.Knife);
            Require(first == 2 && second == 3, "重新订阅后应正常接收后续通知。");
            machine.Stop();
        }

        private static void TestIndependentMachines()
        {
            var first = new PlayerMotionStateMachine();
            var second = new PlayerMotionStateMachine();
            first.SetView(EPlayerModelType.FP);
            second.SetView(EPlayerModelType.TP);
            int secondNotifications = 0;
            second.OnHoldPoseChanged += _ => secondNotifications++;
            first.SetEquipment(true, PlayerHoldPose.Rifle);
            Require(first.HasEquippedWeapon && !second.HasEquippedWeapon && second.HoldPose == PlayerHoldPose.Knife && secondNotifications == 0,
                "一个角色的装备变化不得发布到另一个角色的动作状态机。");
            first.Stop();
            second.Stop();
        }

        private static void TestInvalidThresholds()
        {
            foreach (float threshold in new[] { -1f, float.NaN, float.PositiveInfinity, float.NegativeInfinity })
            {
                bool rejected = false;
                try { _ = new PlayerMotionStateMachine(threshold); }
                catch (ArgumentOutOfRangeException) { rejected = true; }
                Require(rejected, "移动阈值必须拒绝负数、NaN 和 Infinity。");
            }
        }

        private static void AdvanceDirection(PlayerMotionStateMachine machine, Vector3 velocity)
        {
            machine.Tick(velocity);
            machine.Tick(velocity);
        }

        private static void RunCase(string name, Action test, StringBuilder report, ref int passed)
        {
            try { test(); }
            catch (Exception exception) { throw new InvalidOperationException("动作状态验证失败：" + name, exception); }
            passed++;
            report.AppendLine("PASS " + name);
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
