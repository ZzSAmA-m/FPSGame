using System;
using System.Text;
using GamePlay.Weapon;
using Unity.Pipeline.Commands;
using UnityEditor;
using UnityEngine;

namespace FPSGame.Editor
{
    /// <summary>检查 AK47 状态流转、弹药和动作边界。</summary>
    public static class AK47Validation
    {
        /// <summary>从菜单执行枪械回归检查。</summary>
        [MenuItem("Tools/FPSGame/Validate AK47")]
        static void RunFromMenu()
        {
            Debug.Log(RunAll());
        }

        /// <summary>执行全部枪械检查并返回报告。</summary>
        [CliCommand("ak47_validate", "Validate AK47 state machine", Tags = new[] { "fpsgame/validation" })]
        public static string RunAll()
        {
            var report = new StringBuilder();
            int passed = 0;
            Run("配置拒绝无效弹量、容量和时长", TestConfigValidation, report, ref passed);
            Run("未装备时拒绝开火与换弹", TestInitialState, report, ref passed);
            Run("准备按边界完成且不会重复开始", TestPreparing, report, ref passed);
            Run("开火只扣一发且按间隔解锁", TestShooting, report, ref passed);
            Run("空弹失败保持就绪状态", TestEmptyMagazine, report, ref passed);
            Run("满弹匣拒绝换弹", TestFullMagazine, report, ref passed);
            Run("换弹到期才装填且只完成一次", TestReloading, report, ref passed);
            Run("取消换弹不补弹且再次换弹重新计时", TestCancelReload, report, ref passed);
            Run("取消准备不会产生完成通知", TestCancelPrepare, report, ref passed);
            Run("不足一匣的备弹全部装填且不会凭空增加", TestPartialReserve, report, ref passed);
            Run("连续射击和换弹保持弹药守恒", TestAmmoConservation, report, ref passed);
            Run("配置工厂创建独立枪械实例", TestFactoryIsolation, report, ref passed);
            Run("事件观察到完整状态且拒绝重入动作", TestEventConsistency, report, ref passed);
            Run("过期和重复 Tick 不会倒退动作时间", TestStaleTicks, report, ref passed);
            Run("首次 Tick 前的失败请求不会固定零基准", TestRejectedBeforeFirstTick, report, ref passed);
            Run("准备、射击和换弹支持 Tick 回绕", TestTickWrap, report, ref passed);
            Run("已经到期的冷却不会在半圈后重新出现", TestExpiredCooldownAfterHalfRange, report, ref passed);
            Run("收起再装备不能绕过射击冷却", TestHolsterCooldown, report, ref passed);
            return "PASS " + passed + " checks\n" + report;
        }

        /// <summary>检查配置参数在构造时得到校验。</summary>
        static void TestConfigValidation()
        {
            ExpectInvalid(() => new GunConfig(-1, 3, 1, 1, 1));
            ExpectInvalid(() => new GunConfig(3, 0, 1, 1, 1));
            ExpectInvalid(() => new GunConfig(3, -1, 1, 1, 1));
            ExpectInvalid(() => new GunConfig(3, 3, 0, 1, 1));
            ExpectInvalid(() => new GunConfig(3, 3, 1, 0, 1));
            ExpectInvalid(() => new GunConfig(3, 3, 1, 1, 0));
            uint tooLong = (uint)int.MaxValue + 1;
            ExpectInvalid(() => new GunConfig(3, 3, tooLong, 1, 1));
            ExpectInvalid(() => new GunConfig(3, 3, 1, tooLong, 1));
            ExpectInvalid(() => new GunConfig(3, 3, 1, 1, tooLong));
        }

        /// <summary>检查初始弹药划分和未装备限制。</summary>
        static void TestInitialState()
        {
            var gun = new AK47(new GunConfig(100, 30, 2, 3, 4));
            Assert(gun.GunState == GunState.None, "新枪应处于 None。");
            Assert(gun.Magazine.Rounds == 30 && gun.Magazine.ReserveRounds == 70 && gun.Magazine.TotalBullets == 100,
                "100 发总弹量应分为 30 发弹匣与 70 发备弹。");
            Assert(!gun.TryFire() && !gun.TryReload(), "未装备时不能开火或换弹。");
            Assert(gun.GunState == GunState.None && gun.Magazine.TotalBullets == 100, "失败请求不能改变状态或弹量。");
        }

        /// <summary>检查准备结束边界和重复请求。</summary>
        static void TestPreparing()
        {
            var gun = new AK47(new GunConfig(6, 3, 2, 3, 4));
            int completed = 0;
            gun.OnPrepareCompleted += () => completed++;
            gun.Tick(10);
            Assert(gun.TryPrepare(), "未装备枪械应允许开始准备。");
            Assert(!gun.TryPrepare() && !gun.TryFire() && !gun.TryReload(), "准备中应拒绝其他动作。");
            gun.Tick(11);
            Assert(gun.GunState == GunState.Preparing && completed == 0, "准备不能提前完成。");
            gun.Tick(12);
            Assert(gun.GunState == GunState.Normal && completed == 1, "到达结束拍应完成准备。");
            gun.Tick(12);
            gun.Tick(13);
            Assert(completed == 1 && !gun.TryPrepare(), "准备完成只能通知一次，就绪时不应重新准备。");
        }

        /// <summary>检查一次开火和冷却结束边界。</summary>
        static void TestShooting()
        {
            var gun = new AK47(new GunConfig(6, 3, 2, 3, 4));
            int shots = 0;
            gun.OnShot += () => shots++;
            Equip(gun);
            Assert(gun.TryFire(), "就绪且有弹时应开火成功。");
            Assert(gun.GunState == GunState.Shooting && gun.Magazine.Rounds == 2 && shots == 1, "成功开火应立即扣一发并通知一次。");
            Assert(!gun.TryFire() && !gun.TryReload() && !gun.TryPrepare(), "射击间隔内不能开始其他动作。");
            gun.Tick(4);
            Assert(gun.GunState == GunState.Shooting && shots == 1, "射击间隔不能提前结束。");
            gun.Tick(5);
            Assert(gun.GunState == GunState.Normal && shots == 1, "间隔结束不能再次扣弹或通知射击。");
            Assert(gun.TryFire() && !gun.TryFire() && gun.Magazine.Rounds == 1 && shots == 2, "同一拍不能重复开火。");
        }

        /// <summary>检查空弹请求不会卡在射击或换弹状态。</summary>
        static void TestEmptyMagazine()
        {
            var gun = new AK47(new GunConfig(0, 3, 1, 1, 2));
            Equip(gun);
            Assert(gun.Magazine.Empty && !gun.TryFire() && !gun.TryReload(), "没有任何弹药时开火与换弹都应失败。");
            Assert(gun.GunState == GunState.Normal && gun.Magazine.TotalBullets == 0, "空弹失败不能离开就绪状态。");
        }

        /// <summary>检查满弹请求不会启动换弹。</summary>
        static void TestFullMagazine()
        {
            var gun = new AK47(new GunConfig(100, 30, 1, 1, 2));
            Equip(gun);
            Assert(gun.Magazine.Full && !gun.Magazine.CanReload && !gun.TryReload(), "满弹匣应拒绝换弹。");
            Assert(gun.GunState == GunState.Normal && gun.Magazine.TotalBullets == 100, "失败换弹不能改变状态或总弹量。");
        }

        /// <summary>检查装填时机、动作互斥和完成通知。</summary>
        static void TestReloading()
        {
            var gun = new AK47(new GunConfig(6, 3, 2, 2, 4));
            int completed = 0;
            gun.OnReloadCompleted += () => completed++;
            Equip(gun);
            Assert(gun.TryFire(), "换弹前应能开火。");
            gun.Tick(4);
            Assert(gun.TryReload(), "存在缺口和备弹时应允许换弹。");
            Assert(gun.Magazine.Rounds == 2 && gun.Magazine.ReserveRounds == 3, "开始换弹不能立即装填。");
            Assert(!gun.TryReload() && !gun.TryFire() && !gun.TryPrepare(), "换弹中不能重启动作或开火。");
            gun.Tick(7);
            Assert(gun.GunState == GunState.Reloading && gun.Magazine.Rounds == 2 && completed == 0, "换弹不能提前完成。");
            gun.Tick(8);
            Assert(gun.GunState == GunState.Normal && gun.Magazine.Rounds == 3 && gun.Magazine.ReserveRounds == 2,
                "换弹完成应只填补缺口并扣除备弹。");
            gun.Tick(8);
            gun.Tick(9);
            Assert(completed == 1 && gun.Magazine.TotalBullets == 5, "装填只能完成一次，且不能增加总弹量。");
        }

        /// <summary>检查取消换弹后不会遗留旧完成动作。</summary>
        static void TestCancelReload()
        {
            var gun = new AK47(new GunConfig(6, 3, 2, 2, 4));
            int completed = 0;
            gun.OnReloadCompleted += () => completed++;
            Equip(gun);
            gun.TryFire();
            gun.Tick(4);
            Assert(gun.TryReload(), "应允许开始待取消的换弹。");
            gun.Tick(5);
            gun.Unequip();
            gun.Tick(100);
            Assert(gun.GunState == GunState.None && gun.Magazine.Rounds == 2 && gun.Magazine.ReserveRounds == 3 && completed == 0,
                "取消换弹后旧动作不能装填或通知完成。");
            Assert(gun.TryPrepare(), "收起后应能重新准备。");
            gun.Tick(102);
            Assert(gun.TryReload(), "重新准备后应能再次换弹。");
            gun.Tick(105);
            Assert(gun.GunState == GunState.Reloading && gun.Magazine.Rounds == 2, "再次进入换弹应重新计时。");
            gun.Tick(106);
            Assert(gun.Magazine.Rounds == 3 && completed == 1, "第二次换弹应独立完成一次。");
        }

        /// <summary>检查收起会取消尚未完成的准备。</summary>
        static void TestCancelPrepare()
        {
            var gun = new AK47(new GunConfig(6, 3, 2, 2, 4));
            int completed = 0;
            int changes = 0;
            gun.OnPrepareCompleted += () => completed++;
            gun.OnSwitchState += _ => changes++;
            gun.Tick(0);
            gun.TryPrepare();
            gun.Unequip();
            gun.Unequip();
            gun.Tick(100);
            Assert(gun.GunState == GunState.None && completed == 0 && changes == 2,
                "收起应取消准备，重复收起不能重复通知。");
        }

        /// <summary>检查不足一匣的备弹与耗尽边界。</summary>
        static void TestPartialReserve()
        {
            var gun = new AK47(new GunConfig(4, 3, 1, 1, 2));
            Equip(gun);
            for (uint tick = 1; tick <= 3; tick++)
            {
                Assert(gun.TryFire(), "初始弹匣中的每一发都应能射出。");
                gun.Tick(tick + 1);
            }
            Assert(gun.Magazine.Rounds == 0 && gun.Magazine.ReserveRounds == 1, "打空后应只剩一发备弹。");
            Assert(gun.TryReload(), "一发备弹也应允许换弹。");
            gun.Tick(6);
            Assert(gun.Magazine.Rounds == 1 && gun.Magazine.ReserveRounds == 0 && gun.Magazine.TotalBullets == 1,
                "备弹不足时只能装入实际剩余弹量。");
            Assert(gun.TryFire(), "最后一发应能正常射出。");
            gun.Tick(7);
            Assert(!gun.TryReload() && !gun.TryFire() && gun.Magazine.TotalBullets == 0 && gun.GunState == GunState.Normal,
                "总弹量耗尽后不能生成新弹药或卡住状态。");
        }

        /// <summary>检查多次换弹之后总射击数等于初始总弹量。</summary>
        static void TestAmmoConservation()
        {
            var gun = new AK47(new GunConfig(11, 3, 1, 1, 2));
            Equip(gun);
            uint tick = 1;
            int shots = 0;
            while (gun.Magazine.TotalBullets > 0)
            {
                if (gun.Magazine.Empty)
                {
                    Assert(gun.TryReload(), "剩余备弹应能用于再次装填。");
                    tick += 2;
                    gun.Tick(tick);
                }
                Assert(gun.TryFire(), "每次装填后的有效弹药应能正常射出。");
                shots++;
                Assert(gun.Magazine.TotalBullets == 11 - shots && gun.Magazine.Rounds + gun.Magazine.ReserveRounds == 11 - shots,
                    "装填与射击必须保持剩余弹药守恒。");
                Assert(shots <= 11, "射击数不能超过初始总弹量。");
                gun.Tick(++tick);
            }
            Assert(shots == 11 && !gun.TryFire() && !gun.TryReload(), "全部弹药应恰好支持十一发射击。");
        }

        /// <summary>检查同一配置不会共享运行中的弹匣和状态。</summary>
        static void TestFactoryIsolation()
        {
            var data = ScriptableObject.CreateInstance<AK47SO>();
            try
            {
                AK47 first = data.CreateGun();
                AK47 second = data.CreateGun();
                Assert(!ReferenceEquals(first, second) && !ReferenceEquals(first.Magazine, second.Magazine), "工厂应为每次请求创建独立枪械和弹匣。");
                int secondRounds = second.Magazine.Rounds;
                Equip(first);
                Assert(first.TryFire(), "第一把枪应能独立开火。");
                Assert(second.GunState == GunState.None && second.Magazine.Rounds == secondRounds, "第一把枪的操作不能改变第二把枪。");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(data);
            }
        }

        /// <summary>检查通知不会暴露半完成状态或允许回调嵌套动作。</summary>
        static void TestEventConsistency()
        {
            var gun = new AK47(new GunConfig(6, 3, 1, 1, 2));
            int shots = 0;
            int reloads = 0;
            Equip(gun);
            gun.OnSwitchState += state =>
            {
                if (state != GunState.Shooting)
                    return;
                Assert(gun.GunState == GunState.Shooting && gun.Magazine.Rounds == 2, "状态通知前必须提交本次扣弹与状态。");
                Assert(!gun.TryFire() && !gun.TryPrepare() && !gun.TryReload(), "状态回调中的嵌套动作应被拒绝。");
                gun.Tick(100);
                gun.Unequip();
                Assert(gun.GunState == GunState.Shooting, "状态回调中的 Tick 和收起不能中断外层动作。");
            };
            gun.OnShot += () =>
            {
                shots++;
                Assert(gun.GunState == GunState.Shooting && gun.Magazine.Rounds == 2 && !gun.TryFire(), "射击通知应观察到已提交状态并拒绝重入。");
            };
            gun.OnReloadCompleted += () =>
            {
                reloads++;
                Assert(gun.GunState == GunState.Normal && gun.Magazine.Rounds == 3 && gun.Magazine.ReserveRounds == 2,
                    "换弹完成通知前必须提交状态与双方弹量。");
                Assert(!gun.TryFire() && !gun.TryReload(), "完成通知中的重入动作应被拒绝。");
            };
            Assert(gun.TryFire() && shots == 1, "正常开火应只产生一次通知。");
            gun.Tick(2);
            Assert(gun.TryReload(), "外层通知结束后应能正常发起下一动作。");
            gun.Tick(4);
            Assert(reloads == 1 && gun.GunState == GunState.Normal, "换弹完成后应解除外层操作锁。");
        }

        /// <summary>检查旧 Tick 不会移动后续动作的时间起点。</summary>
        static void TestStaleTicks()
        {
            var gun = new AK47(new GunConfig(6, 3, 2, 2, 4));
            Equip(gun, 100);
            gun.Tick(100);
            gun.Tick(102);
            Assert(gun.TryFire(), "旧 Tick 请求不能阻止正常开火。");
            gun.Tick(103);
            Assert(gun.GunState == GunState.Shooting, "旧 Tick 不能让射击冷却提前结束。");
            gun.Tick(104);
            Assert(gun.GunState == GunState.Normal, "正确的新 Tick 应按原时间线完成动作。");
        }

        /// <summary>检查首次显式 Tick 前的失败操作不会建立错误基准。</summary>
        static void TestRejectedBeforeFirstTick()
        {
            var gun = new AK47(new GunConfig(3, 3, 2, 2, 2));
            Assert(!gun.TryFire() && !gun.TryReload(), "未装备时的开火与换弹应失败。");
            gun.Unequip();
            gun.Tick(uint.MaxValue - 2);
            Assert(gun.TryPrepare(), "失败请求后仍应接受回绕前的首次 Tick。");
            gun.Tick(uint.MaxValue - 1);
            Assert(gun.GunState == GunState.Preparing, "准备应以首次显式 Tick 为起点。");
            gun.Tick(uint.MaxValue);
            Assert(gun.GunState == GunState.Normal, "首次 Tick 前的失败请求不能把准备计时固定在零拍。");
        }

        /// <summary>检查跨 uint 回绕的三个动作结束边界。</summary>
        static void TestTickWrap()
        {
            var gun = new AK47(new GunConfig(6, 3, 2, 2, 3));
            gun.Tick(uint.MaxValue - 2);
            Assert(gun.TryPrepare(), "回绕前应能开始准备。");
            gun.Tick(uint.MaxValue - 1);
            Assert(gun.GunState == GunState.Preparing, "回绕前的准备不能提前完成。");
            gun.Tick(uint.MaxValue);
            Assert(gun.GunState == GunState.Normal && gun.TryFire(), "回绕边界应能完成准备并开火。");
            gun.Tick(0);
            Assert(gun.GunState == GunState.Shooting, "回绕后的第零拍尚未结束射击冷却。");
            gun.Tick(1);
            Assert(gun.GunState == GunState.Normal && gun.TryReload(), "回绕后的结束拍应正常解锁换弹。");
            gun.Tick(3);
            Assert(gun.GunState == GunState.Reloading && gun.Magazine.Rounds == 2, "回绕后的换弹不能提前装填。");
            gun.Tick(4);
            Assert(gun.GunState == GunState.Normal && gun.Magazine.Rounds == 3 && gun.Magazine.TotalBullets == 5,
                "跨回绕后仍应按正确 Tick 完成换弹。");
        }

        /// <summary>检查已完成的冷却不会被旧截止拍重新锁定。</summary>
        static void TestExpiredCooldownAfterHalfRange()
        {
            var gun = new AK47(new GunConfig(3, 3, 1, 2, 2));
            Equip(gun);
            Assert(gun.TryFire(), "第一发应在就绪后正常射出。");
            gun.Tick(3);
            Assert(gun.GunState == GunState.Normal, "第一发冷却应在第三拍结束。");
            gun.Tick((uint)int.MaxValue + 3);
            gun.Tick((uint)int.MaxValue + 4);
            Assert(gun.GunState == GunState.Normal && gun.TryFire() && gun.Magazine.Rounds == 1,
                "冷却结束后经过半圈时间也不能重新生效。");
        }

        /// <summary>检查重新装备仍保留上次射击的冷却截止时间。</summary>
        static void TestHolsterCooldown()
        {
            var gun = new AK47(new GunConfig(6, 3, 1, 10, 2));
            Equip(gun);
            Assert(gun.TryFire(), "应能完成第一次射击。");
            gun.Unequip();
            gun.Tick(2);
            Assert(gun.TryPrepare(), "收起后应能再次准备。");
            gun.Tick(3);
            Assert(gun.GunState == GunState.Normal && !gun.TryFire(), "再次准备完成不能绕过旧射击冷却。");
            gun.Tick(10);
            Assert(!gun.TryFire(), "原冷却截止前一拍仍不能开火。");
            gun.Tick(11);
            Assert(gun.TryFire() && gun.Magazine.Rounds == 1, "原冷却截止后应能再次开火。");
        }

        /// <summary>把枪械从指定 Tick 推进到准备完成。</summary>
        static void Equip(AK47 gun, uint tick = 0)
        {
            gun.Tick(tick);
            Assert(gun.TryPrepare(), "测试枪械应能开始准备。");
            gun.Tick(unchecked(tick + gun.Config.PrepareTicks));
            Assert(gun.GunState == GunState.Normal, "测试枪械应完成准备并就绪。");
        }

        /// <summary>检查无效配置会在创建时抛出参数异常。</summary>
        static void ExpectInvalid(Action action)
        {
            try
            {
                action();
            }
            catch (ArgumentException)
            {
                return;
            }
            throw new InvalidOperationException("无效配置应抛出参数异常。");
        }

        /// <summary>执行单个场景并补充失败名称。</summary>
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
                throw new InvalidOperationException("AK47 验证失败：" + name, exception);
            }
        }

        /// <summary>在条件不成立时报告场景失败。</summary>
        static void Assert(bool condition, string message)
        {
            if (!condition)
                throw new InvalidOperationException(message);
        }
    }
}
