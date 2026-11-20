using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using GamePlay.HoldSystem;
using GamePlay.Player;
using GamePlay.Player.Combat;
using GamePlay.Player.Simulation;
using GamePlay.Player.Simulation.Reconciliation;
using Mirror;
using Network.Server;
using Network.Server.Combat;
using Unity.Pipeline.Commands;
using UnityEditor;
using UnityEngine;

namespace FPSGame.Editor.Validation
{
    /// <summary>验证射击时间安全边界和真实枪械到数值的批量伤害链路。</summary>
    public static class CombatServerValidation
    {
        const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;
        [CliCommand("combat_server_validate", "Validate authoritative damage, shot time and same-tick trades", Tags = new[] { "fpsgame/validation" })]
        public static string Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return "REFUSED 请先退出 Play Mode。";
            var report = new StringBuilder();
            int passed = 0;
            void Test(string name, Action test) { test(); passed++; report.AppendLine("PASS " + name); }
            Test("首次接收时间随输入去重固定，合成输入无射击时间", TestReceipt);
            Test("视图已包含显示缓冲，排队年龄不重复扣除", TestTime);
            Test("未来、非有限、倒退与异常年龄被拒绝", TestInvalidTime);
            Test("合法高延迟与服务器排队仍限制在 200 ms", TestWindow);
            Test("预测 Epoch 重建保留世界时间，允许新输入轮次", TestEpoch);
            Test("快照区间必须包含实际显示 Tick 且受历史容量约束", TestBracket);
            Test("真实枪械身体伤害只提交一次并经过护甲分摊", TestBodyArmor);
            Test("头部使用独立伤害，死亡后下一拍不再发射", TestHeadDeath);
            Test("死亡后的权威输入仍被确认，角色继续下落但不响应操作", TestDeadServerInput);
            Test("本地死亡后屏蔽实时操作与旧输入重放，保留物理步进", TestDeadPrediction);
            Test("同拍先完成全部移动，再查询移动后目标", TestMovementPhase);
            Test("同一 Tick 双方致死射击均生效", TestTrade);
            Test("伤害回调内重生阻断本批次旧生命的后续伤害", TestReentrantRespawn);
            Test("无效时间保留开火扣弹与事件，但不提交伤害", TestInvalidShot);
            Test("等待首批输入的目标也保留可查询历史", TestAwaitingTarget);
            Test("重生与传送不跨连续段，断线移除历史", TestContinuity);
            return "PASS " + passed + " checks\n" + report;
        }

        static ServerPlayerInput Input(double view = 98, double receipt = 100, uint inputTick = 1, uint epoch = 1,
            uint from = 98, uint to = 98, double rtt = 0)
            => new(1, epoch, null, new PlayerInputFrame {tick = inputTick, viewServerTick = view,
                viewFromTick = from, viewToTick = to}, receipt, rtt);
        static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
        static void TestReceipt()
        {
            var queue = new PlayerInputQueue();
            var frame = new PlayerInputFrame {tick = 1, fire = true};
            queue.Push(frame, 20); frame.fire = false; queue.Push(frame, 40);
            Require(queue.ResolveNext().fire && queue.ResolvedReceiptTick == 20, "冗余包改写首次时间。");
            queue.Commit(1); queue.ResolveNext();
            Require(double.IsNaN(queue.ResolvedReceiptTick), "合成输入不应冒用旧接收时间。");
            queue.Reset(); queue.Push(new PlayerInputFrame {tick = 1}, 50); queue.ResolveNext();
            Require(queue.ResolvedReceiptTick == 50, "Reset 未清理输入时间。");
        }
        static void TestTime()
        {
            var validator = new ServerShotTimeValidator(new ServerCombatSettings());
            var result = validator.Validate(104, Input());
            Require(result.IsValid && result.QueryTick == 98 && !result.Clamped, "视图时间应直接查询。");
            result = validator.Validate(109, Input(98,107,from:98,to:98,rtt:.12));
            Require(result.IsValid && result.QueryTick == 98,
                "Mirror 显示快照下行与输入上行的耗时必须合并校验，不能只计半个 RTT。");
        }
        static void TestInvalidTime()
        {
            var v = new ServerShotTimeValidator(new ServerCombatSettings());
            Require(v.Validate(100, Input(double.NaN)).Failure == ShotTimeFailure.InvalidTime, "NaN 未拒绝。");
            Require(v.Validate(100, Input(110, from:110,to:110)).Failure == ShotTimeFailure.FutureTime, "未来未拒绝。");
            Require(v.Validate(100, Input(50, from:50,to:50)).Failure == ShotTimeFailure.ImplausibleAge, "异常年龄未拒绝。");
            Require(v.Validate(100, Input()).IsValid, "基准无效。");
            Require(v.Validate(101, Input(97,101,2,from:97,to:97)).Failure == ShotTimeFailure.BackwardsTime, "倒退未拒绝。");
            Require(v.Validate(100, Input(receipt:double.NaN)).Failure == ShotTimeFailure.MissingReceipt, "未知首次接收未拒绝。");
        }
        static void TestWindow()
        {
            var v = new ServerShotTimeValidator(new ServerCombatSettings());
            var r = v.Validate(120, Input(90,100,from:90,to:90,rtt:.135));
            Require(r.IsValid && r.Clamped && Math.Abs(r.QueryTick - 108) < .00001, "回溯未夹到执行 Tick 减 12。");
        }
        static void TestEpoch()
        {
            var v = new ServerShotTimeValidator(new ServerCombatSettings());
            Require(v.Validate(100, Input(inputTick:40)).IsValid, "基准失败。");
            Require(v.Validate(100, Input(97.5,100,1,2,97,98)).IsValid, "新轮次应独立校验。");
        }
        static void TestBracket()
        {
            var v = new ServerShotTimeValidator(new ServerCombatSettings());
            Require(v.Validate(100, Input(from:100,to:102)).Failure == ShotTimeFailure.InvalidBracket, "区间外视图未拒绝。");
            Require(v.Validate(100, Input(from:1,to:100)).Failure == ShotTimeFailure.InvalidBracket, "过大区间未拒绝。");
        }
        static Dictionary<uint,PlayerBrain> Actors(params Scope[] scopes)
        {
            var actors = new Dictionary<uint,PlayerBrain>();
            foreach (var scope in scopes) actors.Add(scope.Id,scope.Brain);
            return actors;
        }
        static void Warm(ServerGameLogic logic, Dictionary<uint,PlayerBrain> actors, params Scope[] scopes)
        {
            for (uint tick=1;tick<10;tick++)
            {
                var inputs = new List<ServerPlayerInput>();
                foreach(var scope in scopes) inputs.Add(scope.Input(tick));
                var r = logic.Step(tick, inputs, actors);
                Require(r.Failures.Count==0, "准备阶段失败。");
            }
        }
        static void TestBodyArmor()
        {
            using var a=new Scope(1,Vector3.zero);
            using var b=new Scope(2,Vector3.forward*10);
            a.ViewHeight(1); b.Stats.RestoreArmor(100);
            var logic=new ServerGameLogic(); var actors=Actors(a,b); Warm(logic,actors,a,b);
            var r=logic.Step(10,new[]{a.Input(10,true),b.Input(10)},actors);
            Require(r.Shots.Count==1 && r.Shots[0].Result.Region==HitRegion.Body && r.Shots[0].Applied, "身体未命中。");
            b.Stats.TryGetState(out var state);
            Require(state.Health==80 && state.Armor==80 && r.Shots[0].Damage.HealthLost==20, "护甲分摊或单发次数错误。");
        }
        static void TestHeadDeath()
        {
            using var a=new Scope(1,Vector3.zero);
            using var b=new Scope(2,Vector3.forward*10);
            var logic=new ServerGameLogic(); var actors=Actors(a,b); Warm(logic,actors,a,b);
            var r=logic.Step(10,new[]{a.Input(10,true),b.Input(10)},actors);
            Require(r.Shots.Count==1 && r.Shots[0].Result.Region==HitRegion.Head && r.Shots[0].Killed, "头部未致死。");
            int rounds=b.Gun.Magazine.Rounds;
            logic.Step(11,new[]{b.Input(11,true)},actors);
            Require(b.Gun.Magazine.Rounds==rounds, "死亡玩家仍可发射。");
        }

        static void TestDeadServerInput()
        {
            using var scope = new Scope(1, Vector3.zero);
            var logic = new ServerGameLogic();
            var actors = Actors(scope);
            Warm(logic, actors, scope);
            logic.Step(10, new[] { scope.Input(10, true) }, actors);
            for (uint tick = 11; tick <= 14; tick++)
                logic.Step(tick, new[] { scope.Input(tick) }, actors);
            int rounds = scope.Gun.Magazine.Rounds;
            Require(rounds < scope.Gun.Config.Capacity && scope.Gun.GunState == GunState.Normal,
                "测试枪械必须有空余弹仓且处于可操作状态。");

            scope.Stats.ApplyDamage(1000);
            PlayerSimState falling = scope.Brain.State;
            falling.verticalVelocity = -2f;
            falling.grounded = false;
            scope.Brain.Simulation.RestoreState(falling);
            var frame = new PlayerInputFrame
            {
                tick = 15, move = Vector2.right, look = new Vector2(30f, 15f), jump = true, fire = true, reload = true
            };
            var first = logic.Step(15, new[] { new ServerPlayerInput(scope.Id, 1, scope.Brain, frame) }, actors);
            frame.tick++;
            frame.fire = false;
            frame.key3 = true;
            var second = logic.Step(16, new[] { new ServerPlayerInput(scope.Id, 1, scope.Brain, frame) }, actors);

            Require(first.ProcessedInputs.Count == 1 && first.ProcessedInputs[0].InputTick == 15 &&
                second.ProcessedInputs.Count == 1 && second.ProcessedInputs[0].InputTick == 16,
                "死亡输入没有持续确认。");
            Require(first.Shots.Count == 0 && first.Events.Count == 0 && second.Events.Count == 0 &&
                scope.Brain.Weapon.CurrentSlot == WeaponSlot.Rifle &&
                scope.Gun.GunState == GunState.Normal && scope.Gun.Magazine.Rounds == rounds,
                "死亡后仍处理了射击、换弹或切栏。");
            CheckNeutralMovement(falling, scope.Brain.State);

            PlayerSimState grounded = scope.Brain.State;
            grounded.grounded = true;
            grounded.verticalVelocity = 0f;
            scope.Brain.Simulation.RestoreState(grounded);
            frame.tick++;
            logic.Step(17, new[] { new ServerPlayerInput(scope.Id, 1, scope.Brain, frame) }, actors);
            Require(Mathf.Approximately(scope.Brain.State.position.y, grounded.position.y), "死亡后仍响应起跳。");
        }

        static void TestDeadPrediction()
        {
            using var scope = new Scope(1, Vector3.zero, role: PawnRole.Predicted);
            PlayerSimState falling = scope.Brain.State;
            falling.verticalVelocity = -2f;
            falling.grounded = false;
            scope.Brain.Simulation.RestoreState(falling);
            var oldInput = new PlayerInputFrame { tick = 2, move = Vector2.right, look = Vector2.one * 30f };
            PlayerSimState predicted = scope.Brain.StepPredicted(oldInput).State;
            Require(predicted.position.x != falling.position.x && predicted.yaw != falling.yaw,
                "测试输入在存活时必须产生移动和转向。");
            var history = new PredictionHistory(8);
            history.Seed(1, predicted);
            history.Record(oldInput, predicted);
            var reconciler = new PlayerReconciler(scope.Brain.Simulation, history, new ReconciliationSettings());
            typeof(PlayerStatsController).GetMethod("ApplyLifeState", Private)
                .Invoke(scope.Stats, new object[] { PlayerLifeState.Dead, 1UL });

            var snapshot = new PlayerAuthoritativeSnapshot { StateInputTick = 1, State = falling };
            scope.Brain.Simulation.SetReplayMode(true);
            ReconcileResult reconciliation;
            try { reconciliation = reconciler.Reconcile(snapshot, 2, acceptsGameplayInput: false); }
            finally { scope.Brain.Simulation.SetReplayMode(false); }
            Require(reconciliation.Status == ReconcileStatus.Replayed && reconciliation.ReplayedTicks == 1,
                "死亡后的旧输入必须走实际回放。");
            CheckNeutralMovement(falling, reconciliation.After);

            var next = oldInput;
            next.tick++;
            next.key3 = true;
            next.fire = true;
            next.reload = true;
            PlayerLiveStepResult result = scope.Brain.StepPredicted(next);
            CheckNeutralMovement(reconciliation.After, result.State);
            Require(!result.Fired && scope.Brain.Weapon.CurrentSlot == WeaponSlot.Rifle,
                "本地死亡后仍响应开火或切栏。");
        }

        static void CheckNeutralMovement(in PlayerSimState before, in PlayerSimState after)
        {
            Require(Mathf.Approximately(after.position.x, before.position.x) &&
                Mathf.Approximately(after.position.z, before.position.z) &&
                Mathf.Approximately(after.yaw, before.yaw) &&
                Mathf.Approximately(after.pitch, before.pitch) && after.position.y < before.position.y,
                "死亡后仍移动或转向，或者未继续下落。");
        }

        static void TestMovementPhase()
        {
            using var a=new Scope(1,Vector3.zero);
            using var b=new Scope(2,new Vector3(1,0,10));
            a.ViewHeight(1);
            var logic=new ServerGameLogic(); var actors=Actors(a,b); Warm(logic,actors,a,b);
            var state=b.Brain.State; state.position.x=0; b.Brain.Simulation.RestoreState(state);
            var r=logic.Step(10,new[]{a.Input(10,true,view:10),b.Input(10)},actors);
            Require(r.Shots.Count==1 && r.Shots[0].Applied, "查询未使用全体完成后的本拍几何。");
        }
        static void TestTrade()
        {
            using var a=new Scope(1,Vector3.zero);
            using var b=new Scope(2,Vector3.forward*10,180);
            var logic=new ServerGameLogic();var actors=Actors(a,b);Warm(logic,actors,a,b);
            var r=logic.Step(10,new[]{a.Input(10,true),b.Input(10,true)},actors);
            Require(r.Shots.Count==2 && r.Shots[0].Killed && r.Shots[1].Killed &&
                a.Stats.LifeState==PlayerLifeState.Dead && b.Stats.LifeState==PlayerLifeState.Dead, "同拍互换失败。");
        }
        static void TestReentrantRespawn()
        {
            using var a=new Scope(1,Vector3.zero);
            using var b=new Scope(2,Vector3.forward*10);
            using var c=new Scope(3,Vector3.zero);
            var logic=new ServerGameLogic();var actors=Actors(a,b,c);Warm(logic,actors,a,b,c);
            b.Stats.OnDied += ()=>b.Stats.ResetForSpawn();
            var r=logic.Step(10,new[]{a.Input(10,true),c.Input(10,true)},actors);
            b.Stats.TryGetState(out var state);
            Require(r.Shots.Count==2 && r.Shots[0].Applied && !r.Shots[1].Applied && state.Health==100,
                "旧生命的第二发伤害泄漏到重生生命。");
        }
        static void TestInvalidShot()
        {
            using var a=new Scope(1,Vector3.zero);
            using var b=new Scope(2,Vector3.forward*10);
            var logic=new ServerGameLogic();var actors=Actors(a,b);Warm(logic,actors,a,b);
            int rounds=a.Gun.Magazine.Rounds;
            var r=logic.Step(10,new[]{a.Input(10,true,view:double.NaN)},actors);
            b.Stats.TryGetState(out var state);
            Require(r.Shots.Count==1 && !r.Shots[0].Applied && !r.Shots[0].Time.IsValid &&
                a.Gun.Magazine.Rounds==rounds-1 && state.Health==100 && r.Events.Count>0,
                "非法时间应只拒绝伤害，保留权威射击。");
        }
        static void TestAwaitingTarget()
        {
            using var a=new Scope(1,Vector3.zero);
            using var b=new Scope(2,Vector3.forward*10);
            var logic=new ServerGameLogic();var actors=Actors(a,b);Warm(logic,actors,a);
            Require(logic.History.TrySample(2,8,out _,out _), "无输入目标未记录。");
            var r=logic.Step(10,new[]{a.Input(10,true)},actors);
            Require(r.Shots.Count==1 && r.Shots[0].Applied, "无输入目标无法命中。");
        }
        static void TestContinuity()
        {
            using var a=new Scope(1,Vector3.zero);
            var logic=new ServerGameLogic();var actors=Actors(a);Warm(logic,actors,a);
            uint life=a.Brain.Hitboxes.LifeGeneration;
            a.Stats.ResetForSpawn();
            logic.Step(10,new[]{a.Input(10)},actors);
            Require(a.Brain.Hitboxes.LifeGeneration>life &&
                !logic.History.TrySample(1,8,out _,out var failure) && failure==HitboxHistoryFailure.LifeChanged,
                "存活时出生重置也必须隔离旧生命。");
            var state=a.Brain.State;state.position.x+=10;a.Brain.Simulation.RestoreState(state);
            logic.Step(11,new[]{a.Input(11)},actors);
            Require(!logic.History.TrySample(1,10.5,out _,out failure) && failure==HitboxHistoryFailure.Discontinuous,
                "传送仍跨段插值。");
            logic.RemovePlayer(1);
            Require(!logic.History.TrySample(1,11,out _,out failure) && failure==HitboxHistoryFailure.UnknownPlayer, "断线未清除。");
        }

        sealed class Scope : IDisposable
        {
            readonly IDisposable _fixture;
            readonly Type _type;
            readonly object _value;
            public uint Id {get;}
            public PlayerBrain Brain {get;}
            public PlayerStatsController Stats {get;}
            public GamePlay.HoldSystem.Gun Gun {get;}
            public Scope(uint id, Vector3 position, float yaw = 0, PawnRole role = PawnRole.Authoritative)
            {
                Id=id;
                _type=typeof(PlayerInputFacadeValidation).GetNestedType("PreviewScope",BindingFlags.NonPublic);
                _value=Activator.CreateInstance(_type,new object[]{role});
                _fixture=(IDisposable)_value;
                Brain=(PlayerBrain)_type.GetProperty("Brain").GetValue(_value);
                var root=Brain.gameObject;
                root.AddComponent<NetworkIdentity>();
                root.AddComponent<PlayerController>();
                var definition=(GamePlay.HoldSystem.GunSO)_type.GetField("_definition",Private).GetValue(_value);
                var so=new SerializedObject(definition);so.FindProperty("_headDamage").intValue=100;
                so.FindProperty("_bodyDamage").intValue=40;so.ApplyModifiedPropertiesWithoutUndo();
                Gun=(GamePlay.HoldSystem.Gun)_type.GetMethod("EquipRifle").Invoke(_value,null);
                Stats=root.AddComponent<PlayerStatsController>();
                Set(Stats,"_playerBasicData",AssetDatabase.LoadAssetAtPath<PlayerBasicDataSO>("Assets/SO/PlayerData/Basic/PlayerBasicDataTest.asset"));
                Bind(Brain,"Stats",Stats);
                if (role == PawnRole.Authoritative) Stats.InitializeServer();
                else Stats.InitializeClient(true);
                var hitboxes=root.AddComponent<PlayerHitboxController>();Bind(Brain,"Hitboxes",hitboxes);
                var head=new GameObject("TestHead");head.transform.SetParent(root.transform,false);head.transform.localPosition=Vector3.up*1.8f;
                var sphere=head.AddComponent<SphereCollider>();sphere.radius=.18f;sphere.isTrigger=true;
                var body=new GameObject("TestBody");body.transform.SetParent(root.transform,false);body.transform.localPosition=Vector3.up*.8f;
                var capsule=body.AddComponent<CapsuleCollider>();capsule.radius=.4f;capsule.height=1.6f;capsule.isTrigger=true;
                Set(hitboxes,"_head",sphere);Set(hitboxes,"_body",capsule);
                Brain.Simulation.RestoreState(new PlayerSimState{position=position+Vector3.up*100,yaw=yaw});
                if (role == PawnRole.Authoritative) hitboxes.InitializeServer();
                Brain.Weapon.ConsumeActions();
            }
            public void ViewHeight(float height) { var view=Brain.Sight.View;view.localPosition=new Vector3(0,height,.1f); }
            public ServerPlayerInput Input(uint tick,bool fire=false,double? view=null)
            {
                double time=view??Math.Max(1,tick-2);
                uint endpoint=double.IsNaN(time)?0:(uint)time;
                return new ServerPlayerInput(Id,1,Brain,new PlayerInputFrame{tick=tick,fire=fire,
                    viewServerTick=time,viewFromTick=endpoint,viewToTick=endpoint},tick);
            }
            public void Dispose(){Stats.Shutdown();_fixture.Dispose();}
        }
        static void Set(object target,string name,object value)=>target.GetType().GetField(name,Private).SetValue(target,value);
        static void Bind(object target,string name,object value)=>Set(target,"<"+name+">k__BackingField",value);
    }
}
