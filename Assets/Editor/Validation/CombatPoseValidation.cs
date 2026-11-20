using System;
using System.Reflection;
using System.Text;
using Core.Utility;
using GamePlay.Player;
using GamePlay.Player.Combat;
using GamePlay.Player.Simulation;
using Network.Server.Combat;
using Unity.Pipeline.Commands;
using UnityEditor;
using UnityEngine;

namespace FPSGame.Editor.Validation
{
    /// <summary>以真实预制体验证手动动画时钟和客户端历史形状一致性。</summary>
    public static class CombatPoseValidation
    {
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        [CliCommand("combat_pose_validate", "Validate server pose stepping and displayed historical hitboxes", Tags = new[] {"fpsgame/validation"})]
        public static string Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return "REFUSED 请先退出 Play Mode。";
            var report=new StringBuilder(); int passed=0;
            void Test(string name,Action test){test();passed++;report.AppendLine("PASS "+name);}
            Test("真实 Animator 仅按手动步长推进，同帧补算拥有独立姿态",TestManualTime);
            Test("固定步长动作支持准备、开火、换弹与切枪，关闭恢复动画设置",TestWeaponActions);
            Test("远端根节点加骨骼插值与历史快照对重建的头身几何一致",TestReconstruction);
            Test("远端覆盖可恢复且重复应用不累计骨骼偏移",TestRemoteRestore);
            return "PASS "+passed+" checks\n"+report;
        }
        static void Require(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}
        static void Invoke(object target,string method)=>target.GetType().GetMethod(method,Private)?.Invoke(target,null);
        static void Near(Vector3 a,Vector3 b,string message)=>Require(Vector3.Distance(a,b)<.0001f,message+" error="+Vector3.Distance(a,b));
        static void TestManualTime()
        {
            using var s=new Scope();
            var actor=s.Actor;
            s.Pose.StepServer(0);
            float a=actor.GetCurrentAnimatorStateInfo(0).normalizedTime;
            s.Pose.StepServer(SimTime.StepSeconds);
            float b=actor.GetCurrentAnimatorStateInfo(0).normalizedTime;
            s.Pose.StepServer(SimTime.StepSeconds);
            float c=actor.GetCurrentAnimatorStateInfo(0).normalizedTime;
            Require(b>a && c>b && actor.speed==0,"每个补算 Tick 必须独立推进动画。");
            actor.Update(.2f);
            Require(Mathf.Abs(actor.GetCurrentAnimatorStateInfo(0).normalizedTime-c)<.00001f,"自动求值不得额外推进服务器动画。");
            Require(s.Pose.CapturePose().IsValid && s.Brain.Hitboxes.Capture().IsValid,"手动采样姿态无效。");
        }
        static void TestWeaponActions()
        {
            using var s=new Scope();
            var holder=s.Root.GetComponent<PlayerHolder>();
            uint tick=0; bool fired=false;
            for(int i=0;i<240;i++)
            {
                var r=s.Brain.StepServer(new PlayerInputFrame{tick=++tick,fire=i>100&&i<105,reload=i==110,key3=i==200,key1=i==220});
                fired|=r.Fired;
                s.Pose.StepServer(SimTime.StepSeconds);
                Require(s.Pose.CapturePose().IsValid,"动作阶段丢失骨骼姿态。");
            }
            Require(fired && holder.CurrentSlot==GamePlay.HoldSystem.WeaponSlot.Rifle,"真实武器动作链路未完成。");
            s.Actor.speed=0; s.Pose.Shutdown();
            Require(!s.Pose.IsServerDriven && s.Actor.speed==1,"关闭没有恢复原始动画时钟。");
        }
        static void TestReconstruction()
        {
            using var s=new Scope();
            var history=new ServerHitboxHistory();
            s.Brain.Sight.ApplyAngles(10,-30);s.Brain.Simulation.CaptureFromBody();s.Pose.StepServer(.1f);
            var a=s.Pose.CapturePose();history.Record(1,2,s.Brain.Hitboxes);
            s.Root.transform.position+=new Vector3(4,1,2);
            s.Brain.Sight.ApplyAngles(170,50);s.Brain.Simulation.CaptureFromBody();s.Pose.StepServer(.1f);
            var b=s.Pose.CapturePose();history.Record(1,4,s.Brain.Hitboxes);
            Require(history.TrySamplePair(1,3,2,4,out var sample,out _),"历史快照对采样失败。");
            s.Pose.Shutdown();s.Brain.SetRole(PawnRole.Interpolated);
            s.Root.transform.position-=new Vector3(2,.5f,1);s.Brain.Sight.ApplyAngles(90,10);
            s.Pose.SetRemotePose(PlayerCombatPose.Lerp(a,b,.5f));Invoke(s.Pose,"LateUpdate");
            var displayed=s.Brain.Hitboxes.Capture();
            Near(sample.Geometry.Head.Center,displayed.Head.Center,"头部重建偏差");
            Near(sample.Geometry.Body.PointA,displayed.Body.PointA,"胶囊 A 重建偏差");
            Near(sample.Geometry.Body.PointB,displayed.Body.PointB,"胶囊 B 重建偏差");
            Require(!history.TrySamplePair(1,3,4,2,out _,out _),"反向快照对不应有效。");
        }
        static void TestRemoteRestore()
        {
            using var s=new Scope();
            var pose=s.Pose.CapturePose();
            s.Pose.Shutdown();s.Brain.SetRole(PawnRole.Interpolated);
            Vector3 original=s.Pose.HeadBone.localPosition;
            Quaternion rotation=s.Pose.HeadBone.localRotation;
            pose.headPosition+=Vector3.right*.1f;
            for(int i=0;i<5;i++)
            {
                s.Pose.SetRemotePose(pose);Invoke(s.Pose,"LateUpdate");
                Near(s.Pose.HeadBone.position,s.Root.transform.TransformPoint(pose.headPosition),"远端权威头部位置");
                s.Pose.RestoreRemotePose();Near(s.Pose.HeadBone.localPosition,original,"远端覆盖恢复");
                Require(Quaternion.Angle(s.Pose.HeadBone.localRotation,rotation)<.05f,"旋转恢复错误。");
            }
            s.Pose.ClearRemotePose();Near(s.Pose.HeadBone.localPosition,original,"清理姿态");
        }
        sealed class Scope:IDisposable
        {
            readonly IDisposable _fixture;
            public GameObject Root{get;}
            public PlayerBrain Brain{get;}
            public PlayerCombatPoseController Pose{get;}
            public Animator Actor{get;}
            public Scope()
            {
                var type=typeof(PlayerAimPoseValidation).GetNestedType("Scope",BindingFlags.NonPublic);
                var fixture=Activator.CreateInstance(type);_fixture=(IDisposable)fixture;
                Root=(GameObject)type.GetField("Root").GetValue(fixture);
                Actor=(Animator)type.GetField("Actor").GetValue(fixture);
                Brain=Root.GetComponent<PlayerBrain>();Pose=Root.GetComponent<PlayerCombatPoseController>();
                Invoke(Brain,"Awake");Invoke(Brain.Simulation,"Awake");
                Brain.SetRole(PawnRole.Authoritative);
                Invoke(Brain.Motion,"OnEnable");Invoke(Brain.Weapon,"OnEnable");
                Invoke(Brain.Animator,"OnEnable");Invoke(Root.GetComponent<GamePlay.Player.Presentation.PlayerAimPoseController>(),"OnEnable");
                Brain.Stats.InitializeServer();Brain.Motion.SetView(EPlayerModelType.TP);
                Actor.Rebind();Actor.Update(0);
                Pose.InitializeServer();Brain.Hitboxes.InitializeServer();
            }
            public void Dispose()
            {
                Brain.Hitboxes.Shutdown();Pose.Shutdown();Brain.Stats.Shutdown();
                Invoke(Brain.Animator,"OnDisable");Invoke(Brain.Weapon,"OnDisable");Invoke(Brain.Motion,"OnDisable");
                _fixture.Dispose();
            }
        }
    }
}
