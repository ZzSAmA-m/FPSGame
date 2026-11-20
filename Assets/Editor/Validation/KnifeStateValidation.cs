using System;
using System.Collections.Generic;
using GamePlay.HoldSystem;
using Unity.Pipeline.Commands;

namespace FPSGame.Editor.Validation
{
    public static class KnifeStateValidation
    {
        [CliCommand("knife_state_validate", "Validate knife action deadlines, interruption and instance isolation", Tags = new[] { "fpsgame/validation" })]
        public static string Run()
        {
            TestActionDeadlines();
            TestUnequip();
            TestTickWrap();
            TestUnequipDuringNotification();
            return "PASS 4 checks";
        }

        private static Knife CreateKnife() => new(new KnifeConfig(40, 65, 2, 3, 5));

        private static void TestActionDeadlines()
        {
            var knife = CreateKnife();
            var events = new List<KnifeState>();
            knife.OnStateChanged += events.Add;
            knife.Tick(0);
            Require(!knife.TryAttack(KnifeAttackKind.Light) && knife.TryPrepare(), "收刀时只能准备。");
            knife.Tick(1);
            Require(knife.ActionProgress == 0.5f && !knife.TryPrepare() && !knife.TryAttack(KnifeAttackKind.Heavy),
                "准备尚未结束时不得攻击或重复准备。");
            knife.Tick(2);
            Require(knife.State == KnifeState.Ready && !knife.TryAttack((KnifeAttackKind)255) &&
                    knife.TryAttack(KnifeAttackKind.Light), "准备截止拍应能接受轻击。");
            knife.Tick(4);
            Require(!knife.TryPrepare() && !knife.TryAttack(KnifeAttackKind.Heavy) &&
                    !knife.TryAttack(KnifeAttackKind.Light), "轻击期间不得打断或连发。");
            knife.Tick(5);
            Require(knife.TryAttack(KnifeAttackKind.Heavy), "轻击截止拍应能接受重击。");
            knife.Tick(9);
            Require(!knife.TryPrepare() && !knife.TryAttack(KnifeAttackKind.Light) &&
                    !knife.TryAttack(KnifeAttackKind.Heavy), "重击期间不得打断或连发。");
            knife.Tick(10);
            Require(knife.State == KnifeState.Ready && events.Count == 6, "每个成功转换仅通知一次。");
        }

        private static void TestUnequip()
        {
            foreach (KnifeState target in new[] { KnifeState.Preparing, KnifeState.Ready, KnifeState.LightAttack, KnifeState.HeavyAttack })
            {
                Knife knife = CreateKnife();
                Knife other = CreateKnife();
                knife.Tick(10);
                knife.TryPrepare();
                if (target != KnifeState.Preparing) knife.Tick(12);
                if (target == KnifeState.LightAttack) knife.TryAttack(KnifeAttackKind.Light);
                if (target == KnifeState.HeavyAttack) knife.TryAttack(KnifeAttackKind.Heavy);
                Require(knife.State == target && other.State == KnifeState.None, "每把刀应有独立状态。");
                knife.Unequip();
                knife.Tick(30);
                Require(knife.State == KnifeState.None && !knife.TryAttack(KnifeAttackKind.Light) &&
                        knife.TryPrepare(), "动作被切走后不得延迟结束，重新装备需完整准备。");
                knife.Tick(31);
                Require(knife.State == KnifeState.Preparing, "切回时须从新起点准备。");
            }
        }

        private static void TestTickWrap()
        {
            var knife = CreateKnife();
            knife.Tick(uint.MaxValue - 1);
            knife.TryPrepare();
            knife.Tick(uint.MaxValue);
            knife.Tick(uint.MaxValue - 1);
            knife.Tick(uint.MaxValue);
            Require(knife.ActionProgress == 0.5f, "重复拍和旧拍不得倒退或推进进度。");
            knife.Tick(0);
            Require(knife.TryAttack(KnifeAttackKind.Heavy), "截止拍跨零时必须完成准备。");
            knife.Tick(11);
            Require(knife.State == KnifeState.Ready, "跳过截止拍后仍须结束动作。");
        }

        private static void TestUnequipDuringNotification()
        {
            var knife = CreateKnife();
            var states = new List<KnifeState>();
            knife.OnStateChanged += state =>
            {
                if (state == KnifeState.Preparing) knife.Unequip();
            };
            knife.OnStateChanged += states.Add;
            knife.TryPrepare();
            Require(states.Count == 2 && states[0] == KnifeState.Preparing && states[1] == KnifeState.None &&
                    knife.State == KnifeState.None, "动作回调中切走武器也应按顺序通知收刀。");
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
