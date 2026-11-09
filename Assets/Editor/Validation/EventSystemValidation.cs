using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Threading;
using Core.Event;
using Core.ZLog;
using GamePlay.Events;
using UnityEditor;
using UnityEngine;
using Log = Core.ZLog.ZLog;
using SceneCamera = GamePlay.Camera.CameraController;

namespace FPSGame.Editor.Validation
{
    /// <summary>在编辑模式验证事件中心，并恢复玩法事件与日志设置。</summary>
    public static class EventSystemValidation
    {
        /// <summary>每个参数数量单独测量的发布次数。</summary>
        private const int GcIterations = 100000;

        /// <summary>零参数分配测试的专用事件编号。</summary>
        private const int GcZeroId = 900001;

        /// <summary>一参数分配测试的专用事件编号。</summary>
        private const int GcOneId = 900002;

        /// <summary>二参数分配测试的专用事件编号。</summary>
        private const int GcTwoId = 900003;

        /// <summary>三参数分配测试的专用事件编号。</summary>
        private const int GcThreeId = 900004;

        /// <summary>供无分配监听函数写入的简单结果。</summary>
        private static int _gcSink;

        /// <summary>当前验证使用的日志捕获器。</summary>
        private static CaptureLogHandler _capture;

        /// <summary>是否已有验证正在执行。</summary>
        private static bool _running;

        /// <summary>执行隔离检查，拒绝在运行模式中重置事件中心。</summary>
        public static string Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return "REFUSED：请先退出 Play Mode，再验证事件中心。";
            if (_running)
                return "REFUSED：事件验证已经在执行。";

            _running = true;
            bool oldEnable = Log.Enable;
            bool oldLogEnabled = Debug.unityLogger.logEnabled;
            LogType oldFilter = Debug.unityLogger.filterLogType;
            ILogHandler oldHandler = Debug.unityLogger.logHandler;
            var channels = (LogChannel[])Enum.GetValues(typeof(LogChannel));
            var frequencies = (LogFrequency[])Enum.GetValues(typeof(LogFrequency));
            var channelFlags = new bool[channels.Length];
            var frequencyFlags = new bool[frequencies.Length];
            for (int i = 0; i < channels.Length; i++)
                channelFlags[i] = Log.IsChannelVisible(channels[i]);
            for (int i = 0; i < frequencies.Length; i++)
                frequencyFlags[i] = Log.IsFrequencyVisible(frequencies[i]);

            _capture = new CaptureLogHandler();
            var report = new StringBuilder();
            int passed = 0;
            try
            {
                Debug.unityLogger.logHandler = _capture;
                Debug.unityLogger.logEnabled = true;
                Debug.unityLogger.filterLogType = LogType.Log;
                RunCase("零到三参数发布与移除", TestArities, report, ref passed);
                RunCase("注册幂等和冲突保留监听及日志配置", TestRegistration, report, ref passed);
                RunCase("未注册与类型不符拒绝执行", TestInvalidSignatures, report, ref passed);
                RunCase("原委托判重且 owner 不改变身份", TestOwnerAndIdentity, report, ref passed);
                RunCase("空委托和多播委托被拒绝", TestInvalidDelegates, report, ref passed);
                RunCase("Action 逆变保持原委托去重和移除", TestContravariance, report, ref passed);
                RunCase("分发中删除跳过且新增留到下次", TestMutationDuringDispatch, report, ref passed);
                RunCase("自退订再订阅不会在当次重复执行", TestSelfResubscribe, report, ref passed);
                RunCase("不同事件允许同步嵌套", TestNestedEvents, report, ref passed);
                RunCase("同事件递归拒绝且后续监听继续", TestRecursivePublish, report, ref passed);
                RunCase("监听异常隔离且下次仍可发布", TestListenerException, report, ref passed);
                RunCase("已销毁直接目标清理而 owner 不控制生命周期", TestDestroyedTargets, report, ref passed);
                RunCase("分发中重置被拒绝", TestResetDuringDispatch, report, ref passed);
                RunCase("非主线程调用拒绝修改", TestThreadAffinity, report, ref passed);
                RunCase("发布日志默认详细且支持频率配置", TestPublishLogging, report, ref passed);
                RunCase("日志回调重发同事件不会递归日志或锁住分发", TestLogCallbackReentry, report, ref passed);
                RunCase("隐藏日志后各参数数量热路径零分配", () => TestGc(report), report, ref passed);
                return "PASS " + passed + " checks\n" + report;
            }
            finally
            {
                try
                {
                    RestoreGameplayEvents();
                }
                finally
                {
                    Log.Enable = oldEnable;
                    for (int i = 0; i < channels.Length; i++)
                        Log.SetChannelVisible(channels[i], channelFlags[i]);
                    for (int i = 0; i < frequencies.Length; i++)
                        Log.SetFrequencyVisible(frequencies[i], frequencyFlags[i]);
                    Debug.unityLogger.logHandler = oldHandler;
                    Debug.unityLogger.filterLogType = oldFilter;
                    Debug.unityLogger.logEnabled = oldLogEnabled;
                    _capture = null;
                    _running = false;
                }
            }
        }

        /// <summary>清理测试事件并恢复已启用场景相机的监听。</summary>
        private static void RestoreGameplayEvents()
        {
            Require(EventCenter.Reset(), "验证结束时应允许重置测试事件。");
            Require(GameEventBootstrap.RegisterAll(), "验证结束时应恢复玩法事件注册。");
            SceneCamera[] cameras = UnityEngine.Object.FindObjectsByType<SceneCamera>(
                FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            foreach (SceneCamera camera in cameras)
            {
                if (!camera.isActiveAndEnabled || !camera.gameObject.scene.IsValid() || EditorUtility.IsPersistent(camera))
                    continue;
                EventCenter.AddListener<Transform>(CameraEventIds.Attach, camera.AttachCameraToModel, camera);
                EventCenter.AddListener(CameraEventIds.Detach, camera.DetachCameraFromPlayer, camera);
            }
        }

        /// <summary>检查各参数数量的注册、发布和具名方法移除。</summary>
        private static void TestArities()
        {
            var zero = new ListenerProbe();
            var one = new ListenerProbe();
            var two = new ListenerProbe();
            var three = new ListenerProbe();
            Require(EventCenter.Register(910000, "Arity.Zero"), "应注册零参数事件。");
            Require(EventCenter.Register<int>(910001, "Arity.One"), "应注册一参数事件。");
            Require(EventCenter.Register<int, float>(910002, "Arity.Two"), "应注册二参数事件。");
            Require(EventCenter.Register<int, float, Vector3>(910003, "Arity.Three"), "应注册三参数事件。");
            Require(EventCenter.AddListener(910000, zero.OnZero), "应添加零参数监听。");
            Require(EventCenter.AddListener<int>(910001, one.OnInt), "应添加一参数监听。");
            Require(EventCenter.AddListener<int, float>(910002, two.OnTwo), "应添加二参数监听。");
            Require(EventCenter.AddListener<int, float, Vector3>(910003, three.OnThree), "应添加三参数监听。");
            EventCenter.Publish(910000);
            EventCenter.Publish(910001, 17);
            EventCenter.Publish(910002, 18, 2.5f);
            EventCenter.Publish(910003, 19, 3.5f, new Vector3(1f, 2f, 3f));
            Require(zero.Calls == 1 && one.Calls == 1 && one.LastInt == 17, "零到一参数应准确调用。");
            Require(two.Calls == 1 && two.LastInt == 18 && two.LastFloat == 2.5f, "二参数应保持类型和顺序。");
            Require(three.Calls == 1 && three.LastInt == 19 && three.LastFloat == 3.5f && three.LastVector == new Vector3(1f, 2f, 3f),
                "三参数应准确传递值类型。");
            Require(EventCenter.RemoveListener(910000, zero.OnZero), "应按同实例具名方法移除零参数监听。");
            Require(EventCenter.RemoveListener<int>(910001, one.OnInt), "应移除一参数监听。");
            Require(EventCenter.RemoveListener<int, float>(910002, two.OnTwo), "应移除二参数监听。");
            Require(EventCenter.RemoveListener<int, float, Vector3>(910003, three.OnThree), "应移除三参数监听。");
            EventCenter.Publish(910000);
            EventCenter.Publish(910001, 1);
            EventCenter.Publish(910002, 1, 1f);
            EventCenter.Publish(910003, 1, 1f, Vector3.zero);
            Require(zero.Calls == 1 && one.Calls == 1 && two.Calls == 1 && three.Calls == 1, "已移除监听不应再调用。");
        }

        /// <summary>检查幂等注册及冲突不会覆盖现有事件。</summary>
        private static void TestRegistration()
        {
            const int id = 911000;
            var probe = new ListenerProbe();
            Require(EventCenter.Register(id, "Registration.Stable"), "首次注册应成功。");
            Require(EventCenter.AddListener(id, probe.OnZero), "应保存现有监听。");
            Require(EventCenter.ConfigurePublishLog(id, LogFrequency.High, false), "应设置事件日志。");
            Require(EventCenter.Register(id, "Registration.Stable"), "相同定义重复注册应成功。");
            Require(!EventCenter.Register(id, "Registration.Renamed"), "相同编号不同名称应拒绝。");
            Require(!EventCenter.Register<int>(id, "Registration.Stable"), "相同编号不同参数数量应拒绝。");
            Require(EventCenter.GetListenerCount(id) == 1, "注册幂等或冲突都不能清空监听。");
            _capture.Clear();
            EventCenter.Publish(id);
            string text = _capture.JoinMessages();
            Require(probe.Calls == 1 && text.Contains("[High]"), "注册后应保留原监听与频率配置。");
            Require(!text.Contains(nameof(ListenerProbe.OnZero)), "注册后应保留关闭详细日志的配置。");

            Require(EventCenter.Register<int, string>(id + 1, "Registration.Order"), "应注册二参数定义。");
            Require(!EventCenter.Register<string, int>(id + 1, "Registration.Order"), "参数类型顺序改变应视为冲突。");
            Require(_capture.ErrorCount > 0, "注册冲突应产生诊断错误。");
        }

        /// <summary>检查未注册和类型不匹配不会触发监听。</summary>
        private static void TestInvalidSignatures()
        {
            const int id = 912000;
            var probe = new ListenerProbe();
            Require(!EventCenter.IsRegistered(id), "未注册编号应返回 false。");
            Require(EventCenter.GetListenerCount(id) == -1, "未注册监听查询应返回 -1。");
            Require(!EventCenter.AddListener(id, probe.OnZero), "未注册事件不能添加监听。");
            Require(!EventCenter.RemoveListener(id, probe.OnZero), "未注册事件不能移除监听。");
            EventCenter.Publish(id);
            Require(_capture.ErrorCount > 0, "未注册调用应有错误诊断。");
            Require(EventCenter.Register<int>(id, "Signature.Int"), "应注册整数事件。");
            Require(EventCenter.AddListener<int>(id, probe.OnInt), "应添加正确类型监听。");
            _capture.Clear();
            Require(!EventCenter.AddListener<string>(id, probe.OnString), "不匹配的监听类型应拒绝。");
            Require(!EventCenter.RemoveListener<string>(id, probe.OnString), "不匹配的移除类型应拒绝。");
            EventCenter.Publish(id, "wrong");
            EventCenter.Publish(id);
            Require(probe.Calls == 0 && EventCenter.GetListenerCount(id) == 1, "签名错误不能调用或删除正确监听。");
            Require(_capture.ErrorCount > 0, "签名错误应记录错误。");
            EventCenter.Publish(id, 7);
            Require(probe.Calls == 1 && probe.LastInt == 7, "错误请求之后正确发布仍应正常。");
        }

        /// <summary>检查委托目标身份和可选 owner 元数据的边界。</summary>
        private static void TestOwnerAndIdentity()
        {
            const int id = 913000;
            var first = new ListenerProbe();
            var second = new ListenerProbe();
            Action listener = first.OnZero;
            Require(EventCenter.Register(id, "Identity.Owner"), "应注册测试事件。");
            Require(EventCenter.AddListener(id, listener, new object()), "应添加首个监听。");
            Require(!EventCenter.AddListener(id, listener, new object()), "改变 owner 不能重复添加同一原委托。");
            Require(EventCenter.AddListener(id, second.OnZero, first), "不同实例的同名方法应独立添加。");
            Require(EventCenter.GetListenerCount(id) == 2, "监听数量应按原委托身份计算。");
            EventCenter.Publish(id);
            Require(first.Calls == 1 && second.Calls == 1, "两个实例的方法都应执行一次。");
            Require(EventCenter.RemoveListener(id, first.OnZero), "移除不应要求重复传入 owner。");
            EventCenter.Publish(id);
            Require(first.Calls == 1 && second.Calls == 2 && _capture.WarningCount > 0, "重复警告和移除都不应影响另一实例。");
        }

        /// <summary>检查空回调与组合回调无法绕过逐监听管理。</summary>
        private static void TestInvalidDelegates()
        {
            const int id = 914000;
            var first = new ListenerProbe();
            var second = new ListenerProbe();
            Action combined = first.OnZero;
            combined += second.OnZero;
            Action<int> combinedInt = first.OnInt;
            combinedInt += second.OnInt;
            Require(EventCenter.Register(id, "Delegate.Zero") && EventCenter.Register<int>(id + 1, "Delegate.Int"), "应注册测试定义。");
            Require(!EventCenter.AddListener(id, (Action)null), "空回调应拒绝。");
            Require(!EventCenter.RemoveListener(id, (Action)null), "空移除回调应拒绝。");
            Require(!EventCenter.AddListener(id, combined), "多播回调应拒绝。");
            Require(!EventCenter.RemoveListener(id, combined), "多播移除应拒绝。");
            Require(!EventCenter.AddListener<int>(id + 1, combinedInt), "泛型多播回调也应拒绝。");
            Require(EventCenter.GetListenerCount(id) == 0 && EventCenter.GetListenerCount(id + 1) == 0,
                "非法回调不能留下任何监听。");
            Require(_capture.ErrorCount + _capture.WarningCount > 0, "拒绝非法委托应产生诊断。");
        }

        /// <summary>检查逆变委托可以调用并按原始身份管理。</summary>
        private static void TestContravariance()
        {
            const int id = 915000;
            var probe = new ListenerProbe();
            Action<object> original = probe.OnObject;
            Action<string> variant = original;
            Require(EventCenter.Register<string>(id, "Delegate.Contravariance"), "应注册字符串事件。");
            Require(EventCenter.AddListener<string>(id, variant), "合法 Action 逆变应允许添加。");
            Require(!EventCenter.AddListener<string>(id, original, new object()), "逆变后的同一原委托仍应判重。");
            EventCenter.Publish(id, "value");
            Require(probe.Calls == 1 && Equals(probe.LastObject, "value"), "逆变监听应接收到原字符串。");
            Require(EventCenter.RemoveListener<string>(id, original), "应使用原始逆变委托移除。");
            EventCenter.Publish(id, "after remove");
            Require(probe.Calls == 1 && EventCenter.GetListenerCount(id) == 0, "移除后不应再调用逆变监听。");
        }

        /// <summary>检查固定初始数量、删除跳过和新增延迟生效。</summary>
        private static void TestMutationDuringDispatch()
        {
            const int id = 916000;
            var trace = new StringBuilder();
            bool changed = false;
            Action second = () => trace.Append('B');
            Action third = () => trace.Append('C');
            Action added = () => trace.Append('D');
            Action first = () =>
            {
                trace.Append('A');
                if (changed)
                    return;
                changed = true;
                Require(EventCenter.RemoveListener(id, second), "回调中应能移除尚未执行的监听。");
                Require(EventCenter.AddListener(id, added), "回调中应能添加后续监听。");
            };
            EventCenter.Register(id, "Dispatch.Mutation");
            EventCenter.AddListener(id, first);
            EventCenter.AddListener(id, second);
            EventCenter.AddListener(id, third);
            EventCenter.Publish(id);
            Require(trace.ToString() == "AC", "当次应跳过 B，且不提前执行新加入的 D。");
            trace.Clear();
            EventCenter.Publish(id);
            Require(trace.ToString() == "ACD" && EventCenter.GetListenerCount(id) == 3, "下次应按剩余监听顺序执行新增项。");
        }

        /// <summary>检查移除再添加自身只会影响后续发布。</summary>
        private static void TestSelfResubscribe()
        {
            const int id = 917000;
            var trace = new StringBuilder();
            bool changed = false;
            Action self = null;
            self = () =>
            {
                trace.Append('S');
                if (changed)
                    return;
                changed = true;
                Require(EventCenter.RemoveListener(id, self), "监听应能移除自身。");
                Require(EventCenter.AddListener(id, self), "已移除的原委托应能重新添加。");
            };
            EventCenter.Register(id, "Dispatch.SelfResubscribe");
            EventCenter.AddListener(id, self);
            EventCenter.AddListener(id, () => trace.Append('T'));
            EventCenter.Publish(id);
            Require(trace.ToString() == "ST", "重新订阅的自身不能在当次再次执行。");
            trace.Clear();
            EventCenter.Publish(id);
            Require(trace.ToString() == "TS" && EventCenter.GetListenerCount(id) == 2, "重订阅应在下次按新增位置执行。");
        }

        /// <summary>检查不同事件之间的同步嵌套顺序。</summary>
        private static void TestNestedEvents()
        {
            var trace = new StringBuilder();
            EventCenter.Register(918000, "Nested.Outer");
            EventCenter.Register(918001, "Nested.Inner");
            EventCenter.AddListener(918000, () => { trace.Append('A'); EventCenter.Publish(918001); });
            EventCenter.AddListener(918000, () => trace.Append('C'));
            EventCenter.AddListener(918001, () => trace.Append('B'));
            EventCenter.Publish(918000);
            Require(trace.ToString() == "ABC" && _capture.ErrorCount == 0, "不同事件应同步嵌套并返回外层继续执行。");
        }

        /// <summary>检查直接递归被拒绝而不会中断其他监听。</summary>
        private static void TestRecursivePublish()
        {
            const int id = 919000;
            var trace = new StringBuilder();
            EventCenter.Register(id, "Dispatch.Recursive");
            EventCenter.AddListener(id, () => { trace.Append('A'); EventCenter.Publish(id); });
            EventCenter.AddListener(id, () => trace.Append('B'));
            EventCenter.Publish(id);
            Require(trace.ToString() == "AB" && _capture.ErrorCount > 0, "递归应记录错误，并继续外层后续监听。");
        }

        /// <summary>检查单个监听抛错不会中止分发或锁住下一次发布。</summary>
        private static void TestListenerException()
        {
            const int id = 920000;
            var probe = new ListenerProbe();
            EventCenter.Register(id, "Dispatch.Exception");
            EventCenter.AddListener(id, probe.ThrowOnZero);
            EventCenter.AddListener(id, probe.OnZero);
            EventCenter.Publish(id);
            EventCenter.Publish(id);
            Require(probe.Calls == 2 && _capture.ErrorCount + _capture.ExceptionCount >= 2, "每次异常都应被记录，后续监听和下次发布仍正常。");
        }

        /// <summary>检查直接 Unity 回调目标和诊断 owner 的销毁语义。</summary>
        private static void TestDestroyedTargets()
        {
            const int id = 920100;
            var target = ScriptableObject.CreateInstance<UnityReceiver>();
            var owner = ScriptableObject.CreateInstance<UnityReceiver>();
            var probe = new ListenerProbe();
            try
            {
                EventCenter.Register(id, "Lifecycle.DirectTarget");
                EventCenter.Register(id + 1, "Lifecycle.OwnerOnly");
                Require(EventCenter.AddListener(id, target.Receive), "应能绑定 Unity 对象的具名回调。");
                Require(EventCenter.AddListener(id + 1, probe.OnZero, owner), "应能独立设置诊断 owner。");
                UnityEngine.Object.DestroyImmediate(target);
                UnityEngine.Object.DestroyImmediate(owner);
                EventCenter.Publish(id);
                EventCenter.Publish(id + 1);
                Require(target.Calls == 0 && EventCenter.GetListenerCount(id) == 0, "已销毁的直接 Unity 目标应跳过并清理。");
                Require(probe.Calls == 1 && EventCenter.GetListenerCount(id + 1) == 1, "仅 owner 销毁不能替代显式退订。");
            }
            finally
            {
                if (target != null) UnityEngine.Object.DestroyImmediate(target);
                if (owner != null) UnityEngine.Object.DestroyImmediate(owner);
            }
        }

        /// <summary>检查分发中不能重置正在使用的注册表。</summary>
        private static void TestResetDuringDispatch()
        {
            const int id = 921000;
            bool reset = true;
            var probe = new ListenerProbe();
            EventCenter.Register(id, "Dispatch.Reset");
            EventCenter.AddListener(id, () => reset = EventCenter.Reset());
            EventCenter.AddListener(id, probe.OnZero);
            EventCenter.Publish(id);
            Require(!reset && probe.Calls == 1 && EventCenter.IsRegistered(id) && EventCenter.GetListenerCount(id) == 2,
                "分发中重置必须拒绝且保留当前监听。");
            Require(EventCenter.Reset() && !EventCenter.IsRegistered(id), "分发结束后应能重置注册表。");
        }

        /// <summary>检查工作线程不能注册、发布或重置主线程事件。</summary>
        private static void TestThreadAffinity()
        {
            const int id = 922000;
            var probe = new ListenerProbe();
            bool registered = true;
            bool reset = true;
            Exception failure = null;
            EventCenter.Register(id, "Thread.Main");
            EventCenter.AddListener(id, probe.OnZero);
            var thread = new Thread(() =>
            {
                try
                {
                    registered = EventCenter.Register(id + 1, "Thread.Rejected");
                    EventCenter.Publish(id);
                    reset = EventCenter.Reset();
                }
                catch (Exception exception)
                {
                    failure = exception;
                }
            });
            thread.Start();
            Require(thread.Join(5000), "工作线程检查应在限定时间结束。");
            Require(failure == null && !registered && !reset && probe.Calls == 0 && !EventCenter.IsRegistered(id + 1),
                "非主线程操作应拒绝而不修改注册表或执行监听。");
            EventCenter.Publish(id);
            Require(probe.Calls == 1 && _capture.ErrorCount > 0, "拒绝工作线程后主线程发布仍正常。");
        }

        /// <summary>检查默认日志以及详细开关和频率设置。</summary>
        private static void TestPublishLogging()
        {
            const int id = 923000;
            var probe = new ListenerProbe();
            EventCenter.Register(id, "Logging.Publish");
            EventCenter.AddListener(id, probe.OnZero, probe);
            Require(_capture.JoinMessages().Contains("[Low]"), "注册和添加日志应采用 Low 频率。");
            _capture.Clear();
            EventCenter.Publish(id);
            string detailed = _capture.JoinMessages();
            Require(detailed.Contains("[Event][Medium]") && detailed.Contains(nameof(ListenerProbe.OnZero)),
                "默认发布应记录 Event/Medium 与详细监听信息。");
            Require(EventCenter.ConfigurePublishLog(id, LogFrequency.High, false), "应支持 High 汇总日志。");
            _capture.Clear();
            EventCenter.Publish(id);
            string summary = _capture.JoinMessages();
            Require(summary.Contains("[High]") && summary.Contains("Logging.Publish") && !summary.Contains(nameof(ListenerProbe.OnZero)),
                "关闭明细后应保留汇总并使用配置频率。");
            Require(EventCenter.ConfigurePublishLog(id, LogFrequency.Extreme, true), "应支持 Extreme 详细日志。");
            _capture.Clear();
            EventCenter.Publish(id);
            Require(_capture.JoinMessages().Contains("[Extreme]") && probe.Calls == 3, "修改日志配置不应影响实际分发。");
        }

        /// <summary>检查日志处理器转发同事件不会造成错误日志递归。</summary>
        private static void TestLogCallbackReentry()
        {
            const int id = 923100;
            var probe = new ListenerProbe();
            EventCenter.Register(id, "Logging.Reentry");
            EventCenter.AddListener(id, probe.OnZero);
            EventCenter.ConfigurePublishLog(id, LogFrequency.Medium, false);
            var handler = new ReentrantLogHandler(_capture, () => EventCenter.Publish(id), 8);
            Debug.unityLogger.logHandler = handler;
            try
            {
                EventCenter.Publish(id);
                Require(probe.Calls == 1 && !handler.BudgetExceeded, "日志中的同事件发布应被拒绝，外层业务仅执行一次。");
                EventCenter.Publish(id);
                Require(probe.Calls == 2 && !handler.BudgetExceeded && handler.CallbackCount > 0,
                    "日志回调退出后后续发布仍应正常，且确实执行过重入检查。");
            }
            finally
            {
                Debug.unityLogger.logHandler = _capture;
            }
        }

        /// <summary>分别检查总开关、分类和频率隐藏后的零分配热路径。</summary>
        private static void TestGc(StringBuilder report)
        {
            Require(EventCenter.Register(GcZeroId, "GC.Zero"), "应注册零参数分配检查。");
            Require(EventCenter.Register<int>(GcOneId, "GC.One"), "应注册一参数分配检查。");
            Require(EventCenter.Register<int, float>(GcTwoId, "GC.Two"), "应注册二参数分配检查。");
            Require(EventCenter.Register<int, float, Vector3>(GcThreeId, "GC.Three"), "应注册三参数分配检查。");
            EventCenter.AddListener(GcZeroId, GcZero);
            EventCenter.AddListener<int>(GcOneId, GcOne);
            EventCenter.AddListener<int, float>(GcTwoId, GcTwo);
            EventCenter.AddListener<int, float, Vector3>(GcThreeId, GcThree);

            Log.Enable = false;
            CheckGcGate("Enable=false", report);
            Log.Enable = true;
            Log.SetChannelVisible(LogChannel.Event, false);
            CheckGcGate("Event 分类隐藏", report);
            Log.SetChannelVisible(LogChannel.Event, true);
            Log.SetFrequencyVisible(LogFrequency.Medium, false);
            CheckGcGate("Medium 频率隐藏", report);
        }

        /// <summary>预热全部参数数量后独立测量并记录分配字节数。</summary>
        private static void CheckGcGate(string name, StringBuilder report)
        {
            for (int i = 0; i < 4096; i++)
            {
                EventCenter.Publish(GcZeroId);
                EventCenter.Publish(GcOneId, i);
                EventCenter.Publish(GcTwoId, i, 2f);
                EventCenter.Publish(GcThreeId, i, 3f, Vector3.one);
            }
            GC.GetAllocatedBytesForCurrentThread();
            long zero = MeasureZero();
            long one = MeasureOne();
            long two = MeasureTwo();
            long three = MeasureThree();
            Require(zero == 0 && one == 0 && two == 0 && three == 0,
                name + " 的 0~3 参数发布分配应全部为零，实际：" + zero + "/" + one + "/" + two + "/" + three + " B。");
            report.Append("GC ").Append(name).Append("：0/1/2/3 参数各 ").Append(GcIterations).Append(" 次 = ")
                .Append(zero).Append('/').Append(one).Append('/').Append(two).Append('/').Append(three).Append(" B\n");
        }

        /// <summary>只测量零参数发布循环的线程分配。</summary>
        private static long MeasureZero()
        {
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < GcIterations; i++)
                EventCenter.Publish(GcZeroId);
            return GC.GetAllocatedBytesForCurrentThread() - before;
        }

        /// <summary>只测量整数参数发布循环的线程分配。</summary>
        private static long MeasureOne()
        {
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < GcIterations; i++)
                EventCenter.Publish(GcOneId, i);
            return GC.GetAllocatedBytesForCurrentThread() - before;
        }

        /// <summary>只测量二参数发布循环的线程分配。</summary>
        private static long MeasureTwo()
        {
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < GcIterations; i++)
                EventCenter.Publish(GcTwoId, i, 2f);
            return GC.GetAllocatedBytesForCurrentThread() - before;
        }

        /// <summary>只测量含 Vector3 的三参数发布循环分配。</summary>
        private static long MeasureThree()
        {
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < GcIterations; i++)
                EventCenter.Publish(GcThreeId, i, 3f, Vector3.one);
            return GC.GetAllocatedBytesForCurrentThread() - before;
        }

        /// <summary>零参数分配检查使用的静态监听。</summary>
        private static void GcZero() { _gcSink++; }

        /// <summary>一参数分配检查使用的静态监听。</summary>
        private static void GcOne(int value) { _gcSink ^= value; }

        /// <summary>二参数分配检查使用的静态监听。</summary>
        private static void GcTwo(int value, float amount) { _gcSink = value ^ (int)amount; }

        /// <summary>三参数分配检查使用的静态监听。</summary>
        private static void GcThree(int value, float amount, Vector3 point) { _gcSink = value ^ (int)amount ^ (int)point.x; }

        /// <summary>为单个场景清空测试事件并补充失败名称。</summary>
        private static void RunCase(string name, Action test, StringBuilder report, ref int passed)
        {
            Log.Enable = true;
            Log.SetAllChannelsVisible(true);
            Log.SetAllFrequenciesVisible(true);
            Require(EventCenter.Reset(), "每个测试场景开始前应能重置事件中心。");
            _capture.Clear();
            try
            {
                test();
                passed++;
                report.Append("PASS ").Append(name).Append('\n');
            }
            catch (Exception exception)
            {
                throw new InvalidOperationException("事件验证失败：" + name, exception);
            }
        }

        /// <summary>在条件不成立时中止当前场景。</summary>
        private static void Require(bool condition, string message)
        {
            if (!condition)
                throw new InvalidOperationException(message);
        }

        /// <summary>提供可区分目标实例的具名测试监听。</summary>
        private sealed class ListenerProbe
        {
            /// <summary>这个实例累计收到的调用次数。</summary>
            internal int Calls;

            /// <summary>最近收到的整数值。</summary>
            internal int LastInt;

            /// <summary>最近收到的浮点值。</summary>
            internal float LastFloat;

            /// <summary>最近收到的向量值。</summary>
            internal Vector3 LastVector;

            /// <summary>最近收到的引用参数。</summary>
            internal object LastObject;

            /// <summary>接收零参数调用。</summary>
            internal void OnZero() { Calls++; }

            /// <summary>接收整数参数调用。</summary>
            internal void OnInt(int value) { Calls++; LastInt = value; }

            /// <summary>接收字符串参数调用。</summary>
            internal void OnString(string value) { Calls++; LastObject = value; }

            /// <summary>接收逆变对象参数调用。</summary>
            internal void OnObject(object value) { Calls++; LastObject = value; }

            /// <summary>接收有序二参数调用。</summary>
            internal void OnTwo(int value, float amount) { Calls++; LastInt = value; LastFloat = amount; }

            /// <summary>接收包含向量的三参数调用。</summary>
            internal void OnThree(int value, float amount, Vector3 point) { Calls++; LastInt = value; LastFloat = amount; LastVector = point; }

            /// <summary>模拟单个监听发生业务异常。</summary>
            internal void ThrowOnZero() { throw new InvalidOperationException("Expected listener failure"); }
        }

        /// <summary>提供可以销毁的直接 Unity 回调目标。</summary>
        private sealed class UnityReceiver : ScriptableObject
        {
            /// <summary>即使原生对象销毁也可核对的托管调用次数。</summary>
            internal int Calls;

            /// <summary>记录直接绑定方法是否意外执行。</summary>
            internal void Receive() { Calls++; }
        }

        /// <summary>用有限次日志回调验证事件日志的重入保护。</summary>
        private sealed class ReentrantLogHandler : ILogHandler
        {
            /// <summary>继续捕获日志的原始测试处理器。</summary>
            private readonly ILogHandler _forward;

            /// <summary>每次日志输出尝试执行的重入动作。</summary>
            private readonly Action _callback;

            /// <summary>允许尝试重入的最大次数。</summary>
            private readonly int _budget;

            /// <summary>已经尝试执行的重入次数。</summary>
            internal int CallbackCount { get; private set; }

            /// <summary>是否触发用于避免栈溢出的测试保护。</summary>
            internal bool BudgetExceeded { get; private set; }

            /// <summary>创建有限预算的回调日志处理器。</summary>
            internal ReentrantLogHandler(ILogHandler forward, Action callback, int budget)
            {
                _forward = forward;
                _callback = callback;
                _budget = budget;
            }

            /// <summary>捕获日志后有限次尝试重新发布事件。</summary>
            public void LogFormat(LogType logType, UnityEngine.Object context, string format, params object[] args)
            {
                _forward.LogFormat(logType, context, format, args);
                if (CallbackCount >= _budget)
                {
                    BudgetExceeded = true;
                    return;
                }
                CallbackCount++;
                _callback();
            }

            /// <summary>继续捕获异常，不额外产生异常重入。</summary>
            public void LogException(Exception exception, UnityEngine.Object context)
            {
                _forward.LogException(exception, context);
            }
        }

        /// <summary>暂存预期日志，不把错误测试写入实际 Console。</summary>
        private sealed class CaptureLogHandler : ILogHandler
        {
            /// <summary>保护主线程和工作线程的捕获记录。</summary>
            private readonly object _sync = new object();

            /// <summary>保留用于核对格式的少量日志文本。</summary>
            private readonly List<string> _messages = new List<string>();

            /// <summary>捕获到的错误数量。</summary>
            internal int ErrorCount { get; private set; }

            /// <summary>捕获到的警告数量。</summary>
            internal int WarningCount { get; private set; }

            /// <summary>捕获到的异常数量。</summary>
            internal int ExceptionCount { get; private set; }

            /// <summary>保存格式化日志而不转发到原始处理器。</summary>
            public void LogFormat(LogType logType, UnityEngine.Object context, string format, params object[] args)
            {
                string text = string.Format(CultureInfo.InvariantCulture, format, args);
                lock (_sync)
                {
                    if (logType == LogType.Error || logType == LogType.Assert)
                        ErrorCount++;
                    if (logType == LogType.Warning)
                        WarningCount++;
                    if (_messages.Count < 4096)
                        _messages.Add(text);
                }
            }

            /// <summary>保存异常记录而不污染实际 Console。</summary>
            public void LogException(Exception exception, UnityEngine.Object context)
            {
                lock (_sync)
                {
                    ExceptionCount++;
                    if (_messages.Count < 4096)
                        _messages.Add(exception.ToString());
                }
            }

            /// <summary>清除当前场景已经捕获的日志。</summary>
            internal void Clear()
            {
                lock (_sync)
                {
                    _messages.Clear();
                    ErrorCount = 0;
                    WarningCount = 0;
                    ExceptionCount = 0;
                }
            }

            /// <summary>合并少量记录以核对诊断内容。</summary>
            internal string JoinMessages()
            {
                lock (_sync)
                    return string.Join("\n", _messages);
            }
        }
    }
}
