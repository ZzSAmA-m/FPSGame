using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Core.Utility;
using GamePlay.HoldSystem;
using GamePlay.Player;
using GamePlay.Player.Presentation;
using GamePlay.Player.Simulation;
using Newtonsoft.Json.Linq;
using Unity.Pipeline.Commands;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace FPSGame.Editor.Validation
{
    /// <summary>真实资源的 TP 瞄准叠加和共享骨架 FP 隔离验证；只在预览场景采样。</summary>
    public static class PlayerAimPoseValidation
    {
        private const string PlayerPath = "Assets/Prefabs/Player/Player.prefab";
        private const string GunPath = "Assets/SO/GunData/Definitions/AK47SO.asset";
        private static bool _running;
        private static readonly string[] SpineNames = { "spine_0", "spine_1", "spine_2", "spine_3" };
        private static readonly Dictionary<string, string> BoneFields = new()
        {
            ["_neck"] = "neck_0", ["_head"] = "head_0",
            ["_leftUpperArm"] = "arm_upper_L", ["_leftForearm"] = "arm_lower_L", ["_leftHand"] = "hand_L",
            ["_rightUpperArm"] = "arm_upper_R", ["_rightForearm"] = "arm_lower_R", ["_rightHand"] = "hand_R"
        };
        private static readonly float[] Pitches = { -89f, -60f, -30f, 0f, 30f, 60f, 89f };
        private static readonly float[] Yaws = { 0f, 90f, 180f, 270f };
        private static readonly float[] FireTimes = { 0f, 1f / 24f, 2f / 24f, 0.125f, 5f / 24f, 0.375f, 0.7416667f };

        [CliCommand("player_aim_pose_setup", "Bind TP-only procedural aim bones on the real Player prefab", Tags = new[] { "fpsgame/validation" })]
        public static JObject Setup()
        {
            RequireEdit();
            var existingIdentity = Load<GameObject>(PlayerPath).GetComponent<Mirror.NetworkIdentity>();
            Require(existingIdentity != null && existingIdentity.assetId != 0, "真实 Player 必须保留有效的网络资源编号。");
            uint originalAssetId = existingIdentity.assetId;
            GameObject root = PrefabUtility.LoadPrefabContents(PlayerPath);
            try
            {
                PlayerAimPoseController aim = root.GetComponent<PlayerAimPoseController>();
                bool created = aim == null;
                if (created) aim = root.AddComponent<PlayerAimPoseController>();
                Configure(root, aim);
                GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, PlayerPath);
                // LoadPrefabContents 的临时场景会令 Mirror.OnValidate 清空编号；保存回资源后恢复原值。
                var identity = saved.GetComponent<Mirror.NetworkIdentity>();
                var network = new SerializedObject(identity);
                network.FindProperty("_assetId").longValue = originalAssetId;
                network.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(identity);
                AssetDatabase.SaveAssetIfDirty(saved);
                return new JObject { ["ok"] = true, ["created"] = created, ["prefab"] = PlayerPath,
                    ["component"] = typeof(PlayerAimPoseController).FullName, ["executionOrder"] = 20000 };
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        internal static void Configure(GameObject root, PlayerAimPoseController aim)
        {
            Animator animator = MainAnimator(root);
            PlayerModelController model = root.GetComponent<PlayerModelController>();
            PlayerHolder holder = root.GetComponent<PlayerHolder>();
            Require(animator != null && model != null && holder != null && model.ThirdPersonRoot != null,
                "真实玩家必须提供 TP 根、角色 Animator 和装备栏。");
            Require(animator.transform.IsChildOf(model.ThirdPersonRoot), "配置时角色骨架必须位于 TP 模型根内。");
            var serialized = new SerializedObject(aim);
            Set(serialized, "_thirdPersonRoot", model.ThirdPersonRoot);
            Set(serialized, "_animator", animator);
            Set(serialized, "_weaponMount", holder.WeaponMount);
            SerializedProperty spines = Property(serialized, "_spines");
            spines.arraySize = SpineNames.Length;
            for (int i = 0; i < SpineNames.Length; i++)
                spines.GetArrayElementAtIndex(i).objectReferenceValue = Bone(animator.transform, SpineNames[i]);
            foreach (var field in BoneFields) Set(serialized, field.Key, Bone(animator.transform, field.Value));
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        [CliCommand("player_aim_pose_validate", "Validate TP multi-bone pitch, real dual-arm grip and FP isolation", Tags = new[] { "fpsgame/validation" })]
        public static JObject Run()
        {
            RequireEdit();
            if (_running) throw new InvalidOperationException("TP 瞄准验证正在运行。");
            _running = true;
            try
            {
                var checks = new JArray();
                using var scope = new Scope();
                var metrics = new Metrics();
                TestBindings(scope);
                checks.Add("PASS 真实 Player 的 TP 根、脊柱颈头、双臂及 wpn 配置完整，不绑定 FP 节点");
                TestViewChangingContract();
                checks.Add("PASS 视角切换前通知在赋值和退出动作之前发生，同视角保持幂等");
                scope.ReadyGun();
                foreach (float yaw in Yaws)
                foreach (float pitch in Pitches)
                {
                    scope.Frame(WeaponPresentationAction.Hold, 1f, yaw, pitch, 1f, metrics, true);
                    foreach (float time in FireTimes)
                        scope.Frame(WeaponPresentationAction.Fire, time, yaw, pitch, 1f, metrics, true);
                }
                Require(scope.Aim.FallbackCount == 0, "真实 AK47 持握和开火在正常及极限视角不得触发不可达回退。");
                checks.Add("PASS 四种 yaw 与七种 pitch 下持握和开火均叠加完整俯仰，双臂握枪且无回退");

                scope.PrepareGun();
                foreach (float pitch in Pitches)
                foreach (float time in new[] { 0f, 0.35f, 0.85f })
                    scope.Frame(WeaponPresentationAction.Prepare, time, 0f, pitch, 1f, metrics, false);
                Require(scope.Aim.CurrentAimWeight == 0f, "准备时瞄准 IK 权重必须归零。");
                checks.Add("PASS 准备时仅叠加躯干和颈头，保留本帧动画手枪关系");

                scope.ReadyGun();
                scope.ReloadGun();
                foreach (float pitch in Pitches)
                foreach (float time in new[] { 0f, 0.4f, 1.1f, 2.2f })
                    scope.Frame(WeaponPresentationAction.Reload, time, 90f, pitch, 1f, metrics, false);
                Require(scope.Aim.CurrentAimWeight == 0f, "换弹时瞄准 IK 权重必须归零。");
                checks.Add("PASS 换弹保留原手臂编排、手腕朝向和枪械关系，不强制瞄准");
                TestRemoteActionGate();
                checks.Add("PASS 远端换弹和重新准备让出双臂 IK，取消后恢复瞄准");

                scope.ReadyGun();
                TestBlend(scope, metrics);
                checks.Add("PASS 0.1秒约束淡变期间枪与双手使用同一组目标，不产生握持脱离");
                TestNoAccumulation(scope);
                checks.Add("PASS 1000帧缺少Animator重新写入时无累积，恢复后回到当帧动画姿态");
                TestFpIsolation(scope);
                checks.Add("PASS TP覆盖在模型切往FP前撤销，FP各动作不采样/应用姿态或执行IK且Transform完全一致");

                scope.SetView(EPlayerModelType.TP);
                scope.ReadyGun();
                scope.Frame(WeaponPresentationAction.Hold, 1f, 0f, 60f, 1f, metrics, true);
                scope.Aim.RestoreAnimatedPose();
                Require(scope.Holder.SelectSlot(WeaponSlot.Knife), "应能切刀验证。");
                scope.Motion.StateMachine.SetEquipment(true, GamePlay.Player.MotionFSM.PlayerHoldPose.Knife);
                foreach (float pitch in Pitches)
                    scope.Frame(WeaponPresentationAction.Hold, 1f, 180f, pitch, 1f, metrics, false);
                Require(scope.Aim.CurrentAimWeight == 0f, "刀具不得执行枪械瞄准 IK。");
                checks.Add("PASS 换枪撤销旧覆盖，刀具仅应用身体与颈头及挂点随动");
                return new JObject { ["result"] = "PASS", ["passed"] = checks.Count, ["checks"] = checks,
                    ["metrics"] = metrics.Report(scope.Aim) };
            }
            finally { _running = false; }
        }

        private static void TestBindings(Scope scope)
        {
            Require(scope.Aim != null, "Player Prefab 尚未接入 PlayerAimPoseController，请先执行 player_aim_pose_setup。");
            var serialized = new SerializedObject(scope.Aim);
            Require(Property(serialized, "_thirdPersonRoot").objectReferenceValue == scope.Model.ThirdPersonRoot,
                "姿态组件必须绑定 TP 根。");
            Require(Property(serialized, "_animator").objectReferenceValue == scope.Actor, "姿态组件应绑定真实角色 Animator。");
            var transforms = new List<Transform> { scope.Holder.WeaponMount };
            transforms.AddRange(SpineNames.Select(name => scope.Bones[name]));
            transforms.AddRange(BoneFields.Values.Select(name => scope.Bones[name]));
            Require(transforms.All(value => value != null && value.IsChildOf(scope.Model.ThirdPersonRoot)),
                "全部姿态绑定必须位于当前 TP 模型范围。");
        }

        private static void TestRemoteActionGate()
        {
            using var scope = new Scope();
            scope.ReadyGun();
            PlayerBrain brain = scope.Root.GetComponent<PlayerBrain>();
            PlayerWeaponController weapon = scope.Root.GetComponent<PlayerWeaponController>();
            Require(brain != null && weapon != null, "远端验证需要真实角色的实时门面和武器组件。");
            typeof(PlayerBrain).GetField("<Role>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(brain, PawnRole.Interpolated);
            Invoke(weapon, "OnDisable");
            Invoke(weapon, "OnEnable");
            var metrics = new Metrics();
            scope.Frame(WeaponPresentationAction.Hold, 1f, 0f, 60f, 1f, metrics, true);
            Require(scope.Aim.CurrentAimWeight == 1f, "远端持握应允许完整瞄准 IK。");
            int ikFrames = scope.Aim.IKFrameCount;
            Require(weapon.PresentRemoteReload(WeaponSlot.Rifle), "远端换弹表现入口应成功接受当前长枪。");
            foreach (float pitch in Pitches)
                scope.Frame(WeaponPresentationAction.Reload, 0.4f, 0f, pitch, 1f, metrics, false);
            Require(weapon.IsRemoteReloading && scope.Aim.CurrentAimWeight == 0f && scope.Aim.IKFrameCount == ikFrames,
                "远端换弹期间双臂 IK 应完全让出控制。");
            Require(weapon.PresentRemoteActionCanceled(WeaponSlot.Rifle, GunState.Reloading),
                "远端取消应结束当前换弹。");
            scope.Frame(WeaponPresentationAction.Hold, 1f, 0f, 60f, 1f, metrics, true);
            Require(!weapon.IsRemoteReloading && scope.Aim.CurrentAimWeight == 1f,
                "换弹取消后应恢复持握瞄准。");
            ikFrames = scope.Aim.IKFrameCount;
            Require(weapon.PresentRemotePrepare(WeaponSlot.Rifle), "远端同枪准备应能开始。");
            scope.Frame(WeaponPresentationAction.Prepare, 0.35f, 0f, 60f, 1f, metrics, false);
            Require(weapon.IsRemotePreparing && scope.Aim.CurrentAimWeight == 0f && scope.Aim.IKFrameCount == ikFrames,
                "远端准备期间应由原动作控制双臂。");
            Require(weapon.PresentRemoteActionCanceled(WeaponSlot.Rifle, GunState.Preparing),
                "远端准备取消应成功清理动作。");
            scope.Frame(WeaponPresentationAction.Hold, 1f, 0f, 60f, 1f, metrics, true);
            Require(scope.Aim.CurrentAimWeight == 1f, "准备取消后应恢复瞄准。");
        }

        private static void TestViewChangingContract()
        {
            var state = new GamePlay.Player.MotionFSM.PlayerMotionStateMachine();
            state.SetView(EPlayerModelType.TP);
            int changing = 0, changed = 0;
            state.OnViewChanging += (oldView, newView) =>
            {
                changing++;
                Require(oldView == EPlayerModelType.TP && newView == EPlayerModelType.FP && state.View == oldView,
                    "切换前通知必须提供旧/新视角且旧视角尚未被改写。");
                Require(state.CurState != null, "切换前原动作尚未退出。");
            };
            state.OnViewChanged += _ => { changed++; Require(changing == 1, "后通知不能先于前通知。"); };
            state.SetView(EPlayerModelType.FP);
            state.SetView(EPlayerModelType.FP);
            Require(changing == 1 && changed == 1, "相同视角不得重复发送切换通知。");
            state.Stop();
        }

        private static void TestBlend(Scope scope, Metrics metrics)
        {
            scope.PrepareGun();
            scope.Frame(WeaponPresentationAction.Prepare, 0.5f, 0f, 60f, 1f, metrics, false);
            scope.ReadyGun();
            float previous = scope.Aim.CurrentAimWeight;
            for (int frame = 0; frame < 8; frame++)
            {
                scope.Frame(WeaponPresentationAction.Hold, 1f, 0f, 60f, 1f / 60f, metrics, false);
                Require(scope.Aim.CurrentAimWeight >= previous, "持握瞄准权重应该单调淡入。");
                previous = scope.Aim.CurrentAimWeight;
            }
            Require(scope.Aim.CurrentAimWeight > 0.999f, "瞄准权重应在0.1秒内到达1。");
        }

        private static void TestNoAccumulation(Scope scope)
        {
            scope.ReadyGun();
            scope.Render(WeaponPresentationAction.Hold, 1f, 0f, 60f);
            Dictionary<Transform, LocalPose> animated = CaptureLocal(scope.Actor.transform);
            Require(scope.Aim.ApplyCurrentPose(1f), "非零TP俯仰应应用姿态。");
            Dictionary<Transform, LocalPose> first = CaptureLocal(scope.Actor.transform);
            for (int frame = 0; frame < 1000; frame++)
                scope.Aim.ApplyCurrentPose(1f / 60f);
            AssertSameLocal(first, CaptureLocal(scope.Actor.transform), "稀疏动画连续应用不得累积", 0.00005f, 0.1f);
            scope.Aim.RestoreAnimatedPose();
            AssertSameLocal(animated, CaptureLocal(scope.Actor.transform), "恢复必须返回本帧动画姿态", 0.00001f, 0.05f);
            scope.Aim.ApplyCurrentPose(1f);
            scope.Aim.enabled = false;
            Invoke(scope.Aim, "OnDisable");
            AssertSameLocal(animated, CaptureLocal(scope.Actor.transform), "停用必须撤销最后一帧覆盖", 0.00001f, 0.05f);
            scope.Aim.enabled = true;
            Invoke(scope.Aim, "OnEnable");
        }

        private static void TestFpIsolation(Scope scope)
        {
            scope.ReadyGun();
            scope.Render(WeaponPresentationAction.Hold, 1f, 0f, 60f);
            scope.Aim.ApplyCurrentPose(1f);
            bool cleanBeforeParentChange = false;
            Action<EPlayerModelType, EPlayerModelType> before = (oldView, newView) =>
            {
                cleanBeforeParentChange = oldView == EPlayerModelType.TP && newView == EPlayerModelType.FP &&
                    !scope.Aim.HasAppliedPose && scope.Actor.transform.IsChildOf(scope.Model.ThirdPersonRoot);
            };
            scope.Motion.StateMachine.OnViewChanging += before;
            scope.SetView(EPlayerModelType.FP);
            scope.Motion.StateMachine.OnViewChanging -= before;
            Require(cleanBeforeParentChange, "TP 覆盖必须在模型改父节点前已经撤销。");
            Require(scope.Actor.transform.IsChildOf(scope.Model.FirstPersonRoot), "现有模型切换应正常进入 FP 根。");
            long applyBefore = scope.Aim.AppliedFrameCount, ikBefore = scope.Aim.IKFrameCount;
            foreach (WeaponPresentationAction action in Enum.GetValues(typeof(WeaponPresentationAction)))
            foreach (float pitch in new[] { -89f, 0f, 89f })
            {
                scope.Render(action, action == WeaponPresentationAction.Hold ? 1f : 0.15f, 90f, pitch);
                Dictionary<Transform, LocalPose> baseline = CaptureLocal(scope.Root.transform);
                Require(!scope.Aim.ApplyCurrentPose(1f), "FP 必须在任何骨骼采样/修改前拒绝 TP 姿态应用。");
                AssertSameLocal(baseline, CaptureLocal(scope.Root.transform), "FP相机、模型和动画结果不得受TP层影响", 0f, 0f);
            }
            Require(scope.Aim.AppliedFrameCount == applyBefore && scope.Aim.IKFrameCount == ikBefore,
                "FP 期间姿态应用和 IK 计数必须保持不变。");
        }

        private static Animator MainAnimator(GameObject player)
        {
            var presenter = player.GetComponent<PlayerAnimatorController>();
            Require(presenter != null, "真实 Player 缺少角色动画组件。");
            return Property(new SerializedObject(presenter), "_mainAnimator").objectReferenceValue as Animator;
        }

        private static Transform Bone(Transform root, string name)
        {
            Transform[] found = root.GetComponentsInChildren<Transform>(true).Where(value => value.name == name).ToArray();
            Require(found.Length == 1, "必须唯一定位真实骨骼：" + name);
            return found[0];
        }

        private static SerializedProperty Property(SerializedObject owner, string name) => owner.FindProperty(name) ??
            throw new InvalidOperationException(owner.targetObject.GetType().Name + " 缺少字段 " + name);
        private static void Set(SerializedObject owner, string name, Object value) => Property(owner, name).objectReferenceValue = value;
        private static T Load<T>(string path) where T : Object => AssetDatabase.LoadAssetAtPath<T>(path) ??
            throw new InvalidOperationException("缺少真实资源：" + path);
        private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
        private static void RequireEdit() { if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("请先退出 Play Mode。"); }
        private static void Invoke(Object target, string name) => target.GetType()
            .GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)?.Invoke(target, null);

        private struct LocalPose
        {
            public Vector3 Position, Scale;
            public Quaternion Rotation;
        }
        private static Dictionary<Transform, LocalPose> CaptureLocal(Transform root) => root.GetComponentsInChildren<Transform>(true)
            .ToDictionary(value => value, value => new LocalPose { Position = value.localPosition, Rotation = value.localRotation, Scale = value.localScale });
        private static void AssertSameLocal(Dictionary<Transform, LocalPose> expected, Dictionary<Transform, LocalPose> actual,
            string label, float positionTolerance, float angleTolerance)
        {
            foreach (var entry in expected)
            {
                Require(actual.TryGetValue(entry.Key, out LocalPose value), label + "：骨架对象消失。");
                Require(Vector3.Distance(entry.Value.Position, value.Position) <= positionTolerance &&
                    Vector3.Distance(entry.Value.Scale, value.Scale) <= positionTolerance &&
                    (entry.Value.Rotation.Equals(value.Rotation) || Quaternion.Angle(entry.Value.Rotation, value.Rotation) <= angleTolerance),
                    label + "：" + entry.Key.name);
            }
        }

        private struct RelativePose
        {
            public Vector3 Position;
            public Quaternion Rotation;
            public static RelativePose Capture(Transform reference, Transform target)
            {
                Matrix4x4 value = reference.worldToLocalMatrix * target.localToWorldMatrix;
                Vector3 z = value.GetColumn(2), y = value.GetColumn(1);
                z.Normalize(); y -= z * Vector3.Dot(y, z); y.Normalize();
                return new RelativePose { Position = value.GetColumn(3), Rotation = Quaternion.LookRotation(z, y) };
            }
        }

        private sealed class Metrics
        {
            public int Frames;
            public float RightGripPosition, RightGripRotation, LeftGripPosition, LeftGripRotation;
            public float GunAimRotation, HeadRotation, BoneLength, Scale;
            public void Grip(RelativePose before, RelativePose after, bool right, string context)
            {
                float p = Vector3.Distance(before.Position, after.Position), r = Quaternion.Angle(before.Rotation, after.Rotation);
                if (right) { RightGripPosition = Mathf.Max(RightGripPosition, p); RightGripRotation = Mathf.Max(RightGripRotation, r); }
                else { LeftGripPosition = Mathf.Max(LeftGripPosition, p); LeftGripRotation = Mathf.Max(LeftGripRotation, r); }
                Require(p < 0.001f && r < 0.1f, $"{context}：{(right ? "右" : "左")}手相对本帧动画枪姿态发生脱离：{p}m / {r}°。");
            }
            public JObject Report(PlayerAimPoseController aim) => new JObject
            {
                ["testedFrames"] = Frames, ["maximumRightGripPositionErrorMeters"] = RightGripPosition,
                ["maximumRightGripRotationErrorDegrees"] = RightGripRotation,
                ["maximumLeftGripPositionErrorMeters"] = LeftGripPosition,
                ["maximumLeftGripRotationErrorDegrees"] = LeftGripRotation,
                ["maximumGunAimRotationErrorDegrees"] = GunAimRotation, ["maximumHeadPitchErrorDegrees"] = HeadRotation,
                ["maximumBoneLengthErrorMeters"] = BoneLength, ["maximumScaleError"] = Scale,
                ["fallbackCount"] = aim.FallbackCount, ["appliedFrameCount"] = aim.AppliedFrameCount,
                ["ikFrameCount"] = aim.IKFrameCount
            };
        }

        private sealed class Scope : IDisposable
        {
            public readonly Scene Scene;
            public readonly GameObject Root;
            public readonly PlayerAimPoseController Aim;
            public readonly PlayerMotionController Motion;
            public readonly PlayerModelController Model;
            public readonly PlayerSightController Sight;
            public readonly PlayerHolder Holder;
            public readonly Animator Actor;
            public readonly Dictionary<string, Transform> Bones;
            private readonly AnimatorController _controller;
            private readonly GunSO _definition;
            private uint _tick;

            public Scope()
            {
                Scene = EditorSceneManager.NewPreviewScene();
                try
                {
                    Root = Object.Instantiate(Load<GameObject>(PlayerPath), new InstantiateParameters { scene = Scene });
                    Root.hideFlags = HideFlags.HideAndDontSave;
                    Root.SetActive(true);
                    Aim = Root.GetComponent<PlayerAimPoseController>();
                    Require(Aim != null, "请先执行 player_aim_pose_setup。");
                    Motion = Root.GetComponent<PlayerMotionController>(); Model = Root.GetComponent<PlayerModelController>();
                    Sight = Root.GetComponent<PlayerSightController>(); Holder = Root.GetComponent<PlayerHolder>(); Actor = MainAnimator(Root);
                    _controller = Actor.runtimeAnimatorController as AnimatorController;
                    Require(_controller != null && Actor.updateMode == AnimatorUpdateMode.Normal, "真实角色必须使用 Normal Animator。");
                    Bones = SpineNames.Concat(BoneFields.Values).Concat(new[] { "root_motion", "pelvis", "wpnPivot", "wpn" })
                        .Distinct().ToDictionary(name => name, name => Bone(Actor.transform, name));
                    Motion.StateMachine.SetView(EPlayerModelType.TP);
                    Invoke(Model, "OnEnable");
                    Require(Holder.InitializeLoadout(), "真实装备栏应能初始化。");
                    _definition = Load<GunSO>(GunPath);
                    Require(Holder.EquipWeapon(_definition), "真实 AK47 应能装备。");
                    Motion.StateMachine.SetEquipment(true, GamePlay.Player.MotionFSM.PlayerHoldPose.Rifle);
                    Actor.enabled = true; Actor.cullingMode = AnimatorCullingMode.AlwaysAnimate; Actor.applyRootMotion = false;
                    Actor.Rebind();
                }
                catch { EditorSceneManager.ClosePreviewScene(Scene); throw; }
            }

            private Gun CurrentGun => Holder.CurrentWeapon as Gun;
            public void SetView(EPlayerModelType view) => Motion.StateMachine.SetView(view);
            public void ReadyGun()
            {
                Aim.RestoreAnimatedPose();
                if (Holder.CurrentWeapon is not Gun) Require(Holder.SelectSlot(WeaponSlot.Rifle), "应能切回长枪。");
                Gun gun = CurrentGun;
                if (gun.GunState == GunState.Normal) return;
                gun.Unequip(); gun.Tick(++_tick); Require(gun.TryPrepare(), "应能准备枪械。");
                _tick += gun.Config.PrepareTicks; gun.Tick(_tick);
                Require(gun.GunState == GunState.Normal, "准备拍数后应进入持握。");
                Motion.StateMachine.SetEquipment(true, GamePlay.Player.MotionFSM.PlayerHoldPose.Rifle);
            }
            public void PrepareGun()
            {
                Aim.RestoreAnimatedPose(); Gun gun = CurrentGun; gun.Unequip(); gun.Tick(++_tick);
                Require(gun.TryPrepare(), "应能进入准备动作。");
            }
            public void ReloadGun()
            {
                ReadyGun(); Gun gun = CurrentGun;
                Require(gun.TryFire(), "换弹验证需先消耗一发。");
                _tick += gun.Config.ShotIntervalTicks; gun.Tick(_tick);
                Require(gun.TryReload(), "应能进入换弹动作。");
            }

            public void Render(WeaponPresentationAction action, float time, float yaw, float pitch)
            {
                Aim.RestoreAnimatedPose();
                Sight.ApplyAngles(yaw, pitch);
                bool fp = Motion.StateMachine.View == EPlayerModelType.FP;
                Actor.SetLayerWeight(PlayerAnimatorController.UpLayer, 1f);
                Actor.SetLayerWeight(PlayerAnimatorController.FPLayer, fp ? 1f : 0f);
                AnimatorState down = _controller.layers[0].stateMachine.defaultState;
                Actor.Play(Animator.StringToHash(_controller.layers[0].name + "." + down.name), 0, 1f);
                int layer = fp ? PlayerAnimatorController.FPLayer : PlayerAnimatorController.UpLayer;
                string name;
                if (Holder.CurrentWeapon is Gun)
                {
                    Require(_definition.ResourcePack.TryGetAnimation(action, fp ? WeaponAnimationTarget.FirstPerson : WeaponAnimationTarget.ThirdPerson, out name),
                        "真实资源包缺少动作：" + action);
                }
                else name = fp ? "FP Layer.fp_draw_butterfly" : "Up Layer.tp_draw_butterfly";
                AnimatorState state = FindState(_controller.layers[layer].stateMachine, name);
                Require(state != null && state.motion is AnimationClip, "真实角色缺少目标状态：" + name);
                var clip = (AnimationClip)state.motion;
                float normalized = action == WeaponPresentationAction.Hold ? 1f : time / clip.length;
                Actor.Play(Animator.StringToHash(name), layer, normalized);
                Actor.Update(0f);
                if (Holder.CurrentController is GunModelController model && model.ModelAnimator != null)
                {
                    Animator animator = model.ModelAnimator;
                    WeaponPresentationAction modelAction = action;
                    if (!_definition.ResourcePack.TryGetAnimation(modelAction, WeaponAnimationTarget.Model, out string modelName))
                        _definition.ResourcePack.TryGetAnimation(modelAction = WeaponPresentationAction.Hold, WeaponAnimationTarget.Model, out modelName);
                    var controller = animator.runtimeAnimatorController as AnimatorController;
                    AnimatorState modelState = FindState(controller.layers[0].stateMachine, modelName);
                    var modelClip = modelState.motion as AnimationClip;
                    float modelTime = modelAction == WeaponPresentationAction.Hold || time >= modelClip.length ? 1f : time / modelClip.length;
                    animator.Play(Animator.StringToHash(modelName), 0, modelTime); animator.Update(0f);
                }
            }

            public void Frame(WeaponPresentationAction action, float time, float yaw, float pitch, float dt, Metrics metrics, bool strictAim)
            {
                Render(action, time, yaw, pitch);
                var original = CaptureLocal(Actor.transform);
                Vector3 rootPosition = Root.transform.position; Quaternion rootRotation = Root.transform.rotation;
                Transform right = Bones["hand_R"], left = Bones["hand_L"], mount = Bones["wpn"];
                RelativePose rightBefore = RelativePose.Capture(right, mount), leftBefore = RelativePose.Capture(left, mount);
                Quaternion gunBefore = mount.rotation, headBefore = Bones["head_0"].rotation;
                Quaternion expected = Quaternion.AngleAxis(pitch, Root.transform.right);
                var lengths = new[] { "arm_lower_L", "hand_L", "arm_lower_R", "hand_R" }
                    .ToDictionary(name => name, name => Vector3.Distance(Bones[name].position, Bones[name].parent.position));
                int rounds = CurrentGun?.Magazine.Rounds ?? 0;
                float speed = Actor.speed;
                AnimatorStateInfo actorState = Actor.GetCurrentAnimatorStateInfo(PlayerAnimatorController.UpLayer);
                bool applied = Aim.ApplyCurrentPose(dt);
                if (pitch == 0f)
                {
                    Require(!applied, "零俯仰应跳过程序化姿态修改。");
                    AssertSameLocal(original, CaptureLocal(Actor.transform), "零俯仰应与原动画恒等", 0f, 0f);
                }
                else Require(applied, "有效 TP 非零俯仰应应用姿态。");
                string context = $"{action}, yaw={yaw}, pitch={pitch}, t={time}";
                metrics.Grip(rightBefore, RelativePose.Capture(right, mount), true, context);
                metrics.Grip(leftBefore, RelativePose.Capture(left, mount), false, context);
                if (strictAim)
                {
                    float error = Quaternion.Angle(expected * gunBefore, mount.rotation);
                    metrics.GunAimRotation = Mathf.Max(metrics.GunAimRotation, error);
                    Require(error < 0.1f, $"稳定持握/开火应获得完整视角俯仰：{yaw}°/{pitch}°/{time}s，枪偏差{error}°。");
                }
                float headError = Quaternion.Angle(expected * headBefore, Bones["head_0"].rotation);
                metrics.HeadRotation = Mathf.Max(metrics.HeadRotation, headError);
                Require(headError < 0.1f, "颈头和脊柱累计应补足完整视线增量：" + headError);
                foreach (var pair in lengths)
                {
                    float error = Mathf.Abs(pair.Value - Vector3.Distance(Bones[pair.Key].position, Bones[pair.Key].parent.position));
                    metrics.BoneLength = Mathf.Max(metrics.BoneLength, error);
                    Require(error < 0.0001f, "IK 不得拉伸骨段：" + pair.Key);
                }
                foreach (var pair in original)
                {
                    if (pair.Key == null) continue;
                    float error = Vector3.Distance(pair.Value.Scale, pair.Key.localScale);
                    metrics.Scale = Mathf.Max(metrics.Scale, error);
                    Require(error < 0.00001f, "程序化姿态不得修改骨骼缩放。");
                }
                Require(Vector3.Distance(rootPosition, Root.transform.position) < 0.000001f && Quaternion.Angle(rootRotation, Root.transform.rotation) < 0.05f,
                    "TP 表现不得修改物理根节点。");
                Require((CurrentGun?.Magazine.Rounds ?? 0) == rounds && Actor.speed == speed &&
                    Actor.GetCurrentAnimatorStateInfo(PlayerAnimatorController.UpLayer).fullPathHash == actorState.fullPathHash &&
                    Actor.GetCurrentAnimatorStateInfo(PlayerAnimatorController.UpLayer).normalizedTime == actorState.normalizedTime,
                    "TP 姿态不得推进动画、枪械逻辑或扣弹。");
                metrics.Frames++;
            }

            public void Dispose()
            {
                if (Aim != null) Aim.RestoreAnimatedPose();
                if (Model != null) Invoke(Model, "OnDisable");
                if (Motion != null) Motion.StateMachine.Stop();
                if (Scene.IsValid()) EditorSceneManager.ClosePreviewScene(Scene);
            }
        }

        private static AnimatorState FindState(AnimatorStateMachine machine, string fullName)
        {
            if (string.IsNullOrWhiteSpace(fullName)) return null;
            foreach (ChildAnimatorState child in machine.states)
                if (fullName == child.state.name || fullName.EndsWith("." + child.state.name, StringComparison.Ordinal)) return child.state;
            foreach (ChildAnimatorStateMachine child in machine.stateMachines)
            {
                AnimatorState found = FindState(child.stateMachine, fullName);
                if (found != null) return found;
            }
            return null;
        }
    }
}
