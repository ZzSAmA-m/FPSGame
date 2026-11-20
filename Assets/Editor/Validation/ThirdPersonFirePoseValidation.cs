using System;
using System.Collections.Generic;
using System.Linq;
using GamePlay.HoldSystem;
using GamePlay.Player;
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
    /// <summary>隔离采样真实第三人称骨架，同时验证原始增量的故障与实际控制器的修复姿势。</summary>
    public static class ThirdPersonFirePoseValidation
    {
        private const string Root = "Assets/Resources/CS2/Agents/ctm_fbi_meter/";
        private static bool _running;
        private static readonly string[] StructuralBones =
        {
            "arm_upper_L", "arm_lower_L", "hand_L", "arm_upper_R", "arm_lower_R", "hand_R",
            "spine_0", "spine_1", "spine_2", "spine_3", "neck_0", "head_0"
        };

        public static void Validate() => Run();

        [CliCommand("gun_tp_fire_validate", "Validate real third-person AK fire pose, bone lengths and masked Animator playback", Tags = new[] { "fpsgame/validation" })]
        public static JObject Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("请先退出 Play Mode，再运行第三人称开火姿势验证。");
            if (_running) throw new InvalidOperationException("第三人称开火姿势验证已经在执行。");
            _running = true;
            try
            {
                var checks = new JArray();
                AnimationClip raw = Load<AnimationClip>(Root + "TPAnims/tp_shoot_ak.anim");
                AnimationClip hold = Load<AnimationClip>(Root + "TPAnims/tp_draw_ak.anim");
                AnimationClip fire = Load<AnimationClip>(Root + "PresentationAnims/tp_shoot_ak.anim");
                GameObject player = Load<GameObject>("Assets/Prefabs/Player/Player.prefab");
                PlayerAnimatorController presenter = player.GetComponent<PlayerAnimatorController>();
                Require(presenter != null, "真实 Player 缺少 PlayerAnimatorController。");
                var serialized = new SerializedObject(presenter);
                Animator sourceAnimator = serialized.FindProperty("_mainAnimator")?.objectReferenceValue as Animator;
                Require(sourceAnimator != null, "真实 Player 未绑定 _mainAnimator。");
                AnimatorController controller = sourceAnimator.runtimeAnimatorController as AnimatorController;
                Require(controller != null, "真实角色必须绑定 AnimatorController。");
                int up = Array.FindIndex(controller.layers, layer => layer.name == "Up Layer");
                int down = Array.FindIndex(controller.layers, layer => layer.name == "Down Layer");
                int fp = Array.FindIndex(controller.layers, layer => layer.name == "FP Layer");
                Require(up >= 0 && down >= 0 && fp >= 0, "真实控制器缺少 FP/Up/Down 层。");
                AnimatorState fireState = controller.layers[up].stateMachine.states.Select(value => value.state)
                    .SingleOrDefault(value => value.name == "tp_shoot_ak");
                AnimatorState holdState = controller.layers[up].stateMachine.states.Select(value => value.state)
                    .SingleOrDefault(value => value.motion == hold);
                Require(fireState != null && fireState.motion == fire && holdState != null &&
                    controller.layers[up].blendingMode == AnimatorLayerBlendingMode.Override,
                    "Up Layer 应将 tp_shoot_ak 绑定到生成的完整姿势 Clip，并保持 Override。");
                Pass(checks, "真实 Player 的 Up Layer 绑定生成的完整开火姿势，未将整个层改成 Additive");

                Require(Mathf.Abs(fire.length - 0.75f) < 0.00001f && Mathf.Abs(fire.frameRate - 24f) < 0.00001f &&
                    !AnimationUtility.GetAnimationClipSettings(fire).loopTime,
                    "生成动画应保持 .75 秒、24 fps 且不循环。");
                Pass(checks, "生成动画保留原始时长和帧率，且不循环");

                using var scope = new PreviewScope(sourceAnimator);
                Transform model = scope.Animator.transform;
                var names = StructuralBones.Concat(new[] { "root_motion", "pelvis", "wpnPivot", "wpn" }).ToArray();
                var bones = names.ToDictionary(name => name, name => FindBone(model, name));
                string chestPath = AnimationUtility.CalculateTransformPath(bones["spine_3"], model);
                AvatarMask upperMask = controller.layers[up].avatarMask;
                Require(upperMask != null && Enumerable.Range(0, upperMask.transformCount).Any(
                        i => upperMask.GetTransformPath(i) == chestPath && upperMask.GetTransformActive(i)),
                    "TP 上身 Mask 必须包含双臂的父胸骨 spine_3，避免实际分层与烘焙姿态不一致。");
                Transform[] bound = AnimationUtility.GetCurveBindings(fire)
                    .Where(binding => binding.type == typeof(Transform)).Select(binding => binding.path).Distinct()
                    .Select(path => scope.Paths.TryGetValue(path, out Transform bone) ? bone :
                        throw new InvalidOperationException("真实角色缺少生成动画绑定路径：" + path)).ToArray();
                Require(bound.Length > 0, "生成动画必须绑定真实骨骼。");
                scope.Animator.enabled = false;
                hold.SampleAnimation(scope.Root, hold.length);
                Dictionary<Transform, BonePose> holdPose = Capture(model, bound);
                Dictionary<string, BonePose> baseline = Capture(model, bones);
                GunSO definition = Load<GunSO>("Assets/SO/GunData/Definitions/AK47SO.asset");
                Require(definition.ResourcePack != null && definition.ResourcePack.ModelPrefab is GunModelController,
                    "真实 AK 必须通过资源包提供通用模型预制体。");
                var weapon = new WeaponPreview(scope.Scene, bones["wpn"], definition.ResourcePack);
                weapon.Hold();
                RelativePose rightGrip = RelativePose.Capture(bones["hand_R"], bones["wpn"]);
                RelativePose leftGrip = RelativePose.Capture(bones["hand_L"], bones["wpn"]);
                Dictionary<string, RelativePose> weaponHold = weapon.Capture(bones["hand_R"]);
                raw.SampleAnimation(scope.Root, 0f);
                float rawLeft = Length(model, bones["arm_lower_L"]);
                float rawRight = Length(model, bones["arm_lower_R"]);
                Require(rawLeft < 0.001f && rawRight < 0.001f && baseline["arm_lower_L"].Length > 0.1f &&
                    baseline["arm_lower_R"].Length > 0.1f,
                    "原始增量应能复现左右前臂骨长归零，以防验证测错角色或 Clip。");
                Pass(checks, "原始射击确实把前臂骨长覆盖为近零，复现人物蜷缩的原因");

                fire.SampleAnimation(scope.Root, 0f);
                float startPositionError = 0f, startRotationError = 0f;
                foreach (Transform bone in bound)
                {
                    BonePose pose = Snapshot(model, bone);
                    startPositionError = Mathf.Max(startPositionError, Vector3.Distance(pose.Position, holdPose[bone].Position));
                    startRotationError = Mathf.Max(startRotationError, Quaternion.Angle(pose.Rotation, holdPose[bone].Rotation));
                }
                Require(startPositionError < 0.001f && startRotationError < 0.1f,
                    $"开火首帧应等于持握末帧，实际偏差 {startPositionError}m / {startRotationError}°。");
                Pass(checks, "所有绑定的开火首帧与当前持握末帧一致");

                float minForearm = float.PositiveInfinity, maxLengthError = 0f, maxRootDisplacement = 0f;
                float maxWristDisplacement = 0f, maxWristRotation = 0f, maxHeadDisplacement = 0f;
                var rightGripError = new RelativeError();
                var leftGripError = new RelativeError();
                Dictionary<string, RelativeError> sampledModelErrors = weapon.NewErrors();
                float[] samples = SampleTimes(fire.length, fire, raw, hold, weapon.FireClip, weapon.HoldClip);
                foreach (float time in samples)
                {
                    fire.SampleAnimation(scope.Root, time);
                    weapon.FireAt(time);
                    foreach (string name in StructuralBones)
                        maxLengthError = Mathf.Max(maxLengthError, Mathf.Abs(Length(model, bones[name]) - baseline[name].Length));
                    foreach (string name in new[] { "arm_lower_L", "hand_L", "arm_lower_R", "hand_R" })
                        minForearm = Mathf.Min(minForearm, Length(model, bones[name]));
                    foreach (string name in new[] { "root_motion", "pelvis" })
                        maxRootDisplacement = Mathf.Max(maxRootDisplacement,
                            Vector3.Distance(Snapshot(model, bones[name]).Position, baseline[name].Position));
                    foreach (string name in new[] { "hand_L", "hand_R" })
                    {
                        BonePose wrist = Snapshot(model, bones[name]);
                        maxWristDisplacement = Mathf.Max(maxWristDisplacement, Vector3.Distance(wrist.Position, baseline[name].Position));
                        maxWristRotation = Mathf.Max(maxWristRotation, Quaternion.Angle(wrist.Rotation, baseline[name].Rotation));
                    }
                    rightGripError.Observe(RelativePose.Capture(bones["hand_R"], bones["wpn"]), rightGrip);
                    leftGripError.Observe(RelativePose.Capture(bones["hand_L"], bones["wpn"]), leftGrip);
                    weapon.Observe(bones["hand_R"], weaponHold, sampledModelErrors);
                    maxHeadDisplacement = Mathf.Max(maxHeadDisplacement,
                        Vector3.Distance(Snapshot(model, bones["head_0"]).Position, baseline["head_0"].Position));
                }
                Require(maxLengthError < 0.001f && maxRootDisplacement < 0.001f,
                    $"开火全程应保留骨长和骨盆位置，误差 {maxLengthError}m / {maxRootDisplacement}m。");
                Pass(checks, "逐帧保留双臂、双手、脊柱和头部骨长，根与骨盆没有跳动");
                Require(maxWristDisplacement > 0.001f || maxWristRotation > 0.1f,
                    "修复不能只用持握静帧替代，必须保留真实后坐力变化。");
                rightGripError.RequireBelow(0.001f, 0.1f, "直接采样右手到真正 wpn 的相对位姿");
                leftGripError.RequireBelow(0.01f, 1f, "直接采样左手到真正 wpn 的相对位姿");
                Pass(checks, "全部关键帧及120Hz帧间保留后坐力，真正 wpn 与右手固定、与左手保持握持");
                weapon.RequireStable(sampledModelErrors, "直接采样");
                Pass(checks, "直接采样角色并求值真实枪模型Animator后，模型根与枪身骨骼相对右手固定");

                fire.SampleAnimation(scope.Root, fire.length);
                float endPositionError = 0f, endRotationError = 0f;
                float endStructuralRotationError = 0f;
                var endStructuralBones = new HashSet<Transform>(StructuralBones
                    .Concat(new[] { "root_motion", "pelvis", "wpnPivot" }).Select(name => bones[name]));
                foreach (Transform bone in bound)
                {
                    BonePose pose = Snapshot(model, bone);
                    endPositionError = Mathf.Max(endPositionError, Vector3.Distance(pose.Position, holdPose[bone].Position));
                    float angle = Quaternion.Angle(pose.Rotation, holdPose[bone].Rotation);
                    endRotationError = Mathf.Max(endRotationError, angle);
                    if (endStructuralBones.Contains(bone))
                        endStructuralRotationError = Mathf.Max(endStructuralRotationError, angle);
                }
                // 原素材首帧含右食指扣扳机，末帧回正；首帧归零后会保留这段指节差异。
                // 身体必须回到持握，不能为了手指的合法变化放宽整副骨架的角度限制。
                Require(endPositionError < 0.02f && endStructuralRotationError < 1f,
                    $"开火末帧所有绑定位置及身体姿势应接近持握，实际偏差 {endPositionError}m / {endStructuralRotationError}°。");
                Transform triggerFinger = FindBone(model, "finger_index_1_R");
                string triggerPath = AnimationUtility.CalculateTransformPath(triggerFinger, model);
                float sourceTriggerDifference = Quaternion.Angle(LocalRotation(raw, triggerPath, 0f), LocalRotation(raw, triggerPath, raw.length));
                float generatedTriggerDifference = Quaternion.Angle(LocalRotation(hold, triggerPath, hold.length), LocalRotation(fire, triggerPath, fire.length));
                Require(sourceTriggerDifference > 10f && Mathf.Abs(sourceTriggerDifference - generatedTriggerDifference) < 0.1f,
                    "生成动画必须保留源素材的右食指扣扳机差异，不能强行重写其末帧。");
                Pass(checks, "射击末帧身体恢复持握，所有绑定位置保持接近，保留源素材右食指扣扳机的首末差异");

                scope.Animator.enabled = true;
                scope.Animator.runtimeAnimatorController = controller;
                scope.Animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                scope.Animator.applyRootMotion = false;
                scope.Animator.Rebind();
                scope.Animator.SetLayerWeight(down, 1f);
                scope.Animator.SetLayerWeight(up, 1f);
                scope.Animator.SetLayerWeight(fp, 0f);
                AnimatorState downState = controller.layers[down].stateMachine.defaultState;
                Require(downState != null, "下身必须有真实默认静止状态。");
                int downHash = Animator.StringToHash(controller.layers[down].name + "." + downState.name);
                int holdHash = Animator.StringToHash(controller.layers[up].name + "." + holdState.name);
                int fireHash = Animator.StringToHash(controller.layers[up].name + "." + fireState.name);
                scope.Animator.Play(downHash, down, 1f);
                scope.Animator.Play(holdHash, up, 1f);
                scope.Animator.Update(0f);
                weapon.Hold();
                Dictionary<string, BonePose> maskedHold = Capture(model, bones);
                RelativePose maskedRightGrip = RelativePose.Capture(bones["hand_R"], bones["wpn"]);
                RelativePose maskedLeftGrip = RelativePose.Capture(bones["hand_L"], bones["wpn"]);
                Dictionary<string, RelativePose> maskedWeaponHold = weapon.Capture(bones["hand_R"]);
                var maskedRightGripError = new RelativeError();
                var maskedLeftGripError = new RelativeError();
                Dictionary<string, RelativeError> maskedModelErrors = weapon.NewErrors();
                float maskedLengthError = 0f, maskedRootDisplacement = 0f;
                foreach (float time in samples)
                {
                    scope.Animator.Play(downHash, down, 1f);
                    scope.Animator.Play(fireHash, up, time / fire.length);
                    scope.Animator.Update(0f);
                    weapon.FireAt(time);
                    Require(scope.Animator.GetCurrentAnimatorStateInfo(up).fullPathHash == fireHash,
                        "真实 Animator 必须实际求值 Up Layer 的开火状态。");
                    foreach (string name in StructuralBones)
                        maskedLengthError = Mathf.Max(maskedLengthError, Mathf.Abs(Length(model, bones[name]) - maskedHold[name].Length));
                    foreach (string name in new[] { "root_motion", "pelvis" })
                        maskedRootDisplacement = Mathf.Max(maskedRootDisplacement,
                            Vector3.Distance(Snapshot(model, bones[name]).Position, maskedHold[name].Position));
                    maskedRightGripError.Observe(RelativePose.Capture(bones["hand_R"], bones["wpn"]), maskedRightGrip);
                    maskedLeftGripError.Observe(RelativePose.Capture(bones["hand_L"], bones["wpn"]), maskedLeftGrip);
                    weapon.Observe(bones["hand_R"], maskedWeaponHold, maskedModelErrors);
                }
                Require(maskedLengthError < 0.001f && maskedRootDisplacement < 0.001f,
                    $"真实分层 Animator 仍发生塌缩或根偏移：骨长{maskedLengthError}m，根{maskedRootDisplacement}m。");
                maskedRightGripError.RequireBelow(0.001f, 0.1f, "真实分层Animator右手到真正 wpn 的相对位姿");
                maskedLeftGripError.RequireBelow(0.01f, 1f, "真实分层Animator左手到真正 wpn 的相对位姿");
                Pass(checks, "真实分层Animator在全部关键帧和120Hz帧间保持完整骨架及双手握枪约束");
                weapon.RequireStable(maskedModelErrors, "真实分层Animator");
                Pass(checks, "真实角色及模型Animator共同求值后，模型根和weapon/weapon_offset枪身相对右手固定");

                return new JObject
                {
                    ["result"] = "PASS", ["passed"] = checks.Count, ["checks"] = checks,
                    ["metrics"] = new JObject
                    {
                        ["sampleCount"] = samples.Length, ["rawLeftForearmLength"] = rawLeft, ["rawRightForearmLength"] = rawRight,
                        ["minimumForearmLength"] = minForearm, ["maximumBoneLengthError"] = maxLengthError,
                        ["maximumRootDisplacement"] = maxRootDisplacement, ["maximumHeadDisplacement"] = maxHeadDisplacement,
                        ["maximumWristDisplacement"] = maxWristDisplacement, ["maximumWristRotationDegrees"] = maxWristRotation,
                        ["sampleRateHz"] = 120, ["rightHandToWpn"] = rightGripError.Report(),
                        ["leftHandToWpn"] = leftGripError.Report(), ["sampledModelToRightHand"] = weapon.Report(sampledModelErrors),
                        ["startPositionError"] = startPositionError,
                        ["startRotationErrorDegrees"] = startRotationError, ["endPositionError"] = endPositionError,
                        ["endRotationErrorDegrees"] = endRotationError,
                        ["endStructuralRotationErrorDegrees"] = endStructuralRotationError,
                        ["sourceTriggerFingerDifferenceDegrees"] = sourceTriggerDifference,
                        ["generatedTriggerFingerDifferenceDegrees"] = generatedTriggerDifference,
                        ["sourceTriggerFingerEndpointDifferencePreserved"] = true,
                        ["maskedBoneLengthError"] = maskedLengthError,
                        ["maskedRootDisplacement"] = maskedRootDisplacement,
                        ["maskedRightHandToWpn"] = maskedRightGripError.Report(),
                        ["maskedLeftHandToWpn"] = maskedLeftGripError.Report(),
                        ["maskedModelToRightHand"] = weapon.Report(maskedModelErrors),
                        ["downStillState"] = downState.name
                    }
                };
            }
            finally { _running = false; }
        }

        private static float[] SampleTimes(float duration, params AnimationClip[] clips)
        {
            var times = new SortedSet<float> { 0f, duration };
            for (int frame = 1; frame / 120f < duration; frame++) times.Add(frame / 120f);
            foreach (AnimationClip clip in clips.Distinct())
                foreach (EditorCurveBinding binding in AnimationUtility.GetCurveBindings(clip))
                {
                    AnimationCurve curve = AnimationUtility.GetEditorCurve(clip, binding);
                    if (curve == null) continue;
                    foreach (Keyframe key in curve.keys)
                        if (key.time >= 0f && key.time <= duration) times.Add(key.time);
                }
            // 还检查输出关键帧之间的中点，不能只在烘焙采样点上证明握持正确。
            float[] keyTimes = times.ToArray();
            for (int i = 1; i < keyTimes.Length; i++)
                if (keyTimes[i] - keyTimes[i - 1] > 0.000001f)
                    times.Add((keyTimes[i] + keyTimes[i - 1]) * 0.5f);
            return times.ToArray();
        }

        private static Quaternion LocalRotation(AnimationClip clip, string path, float time)
        {
            float[] values = new float[4];
            string[] channels = { "x", "y", "z", "w" };
            for (int i = 0; i < channels.Length; i++)
            {
                AnimationCurve curve = AnimationUtility.GetEditorCurve(clip,
                    EditorCurveBinding.FloatCurve(path, typeof(Transform), "m_LocalRotation." + channels[i]));
                Require(curve != null, "缺少食指旋转曲线：" + clip.name + " / " + path);
                values[i] = curve.Evaluate(time);
            }
            return new Quaternion(values[0], values[1], values[2], values[3]).normalized;
        }

        private static Transform FindBone(Transform root, string name)
        {
            Transform[] found = root.GetComponentsInChildren<Transform>(true).Where(value => value.name == name).ToArray();
            Require(found.Length == 1, "真实 Animator 骨架必须能唯一定位：" + name);
            return found[0];
        }

        private static float Length(Transform root, Transform bone) => bone.parent != null
            ? Vector3.Distance(root.InverseTransformPoint(bone.position), root.InverseTransformPoint(bone.parent.position)) : 0f;

        private static BonePose Snapshot(Transform root, Transform bone) => new BonePose
        {
            Position = root.InverseTransformPoint(bone.position),
            Rotation = Quaternion.Inverse(root.rotation) * bone.rotation,
            Length = Length(root, bone)
        };

        private static Dictionary<Transform, BonePose> Capture(Transform root, IEnumerable<Transform> bones) =>
            bones.ToDictionary(value => value, value => Snapshot(root, value));

        private static Dictionary<string, BonePose> Capture(Transform root, Dictionary<string, Transform> bones) =>
            bones.ToDictionary(pair => pair.Key, pair => Snapshot(root, pair.Value));

        private static T Load<T>(string path) where T : Object
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            Require(asset != null, "缺少真实验证资产：" + path);
            return asset;
        }

        private static void Pass(JArray checks, string message) => checks.Add("PASS " + message);

        private static void Require(bool value, string message)
        {
            if (!value) throw new InvalidOperationException(message);
        }

        private struct BonePose
        {
            public Vector3 Position;
            public Quaternion Rotation;
            public float Length;
        }

        private struct RelativePose
        {
            public Vector3 Position;
            public Quaternion Rotation;
            public Vector3 Scale;

            public static RelativePose Capture(Transform reference, Transform target)
            {
                // 使用完整层级矩阵计入枪模型内部的坐标转换及100倍缩放；旋转取正交基，不能把带scale的矩阵直接当旋转。
                Matrix4x4 matrix = reference.worldToLocalMatrix * target.localToWorldMatrix;
                Vector3 x = matrix.GetColumn(0), y = matrix.GetColumn(1), z = matrix.GetColumn(2);
                Vector3 scale = new Vector3(x.magnitude, y.magnitude, z.magnitude);
                Require(scale.x > 0.0000001f && scale.y > 0.0000001f && scale.z > 0.0000001f,
                    "握枪相对位姿存在退化缩放：" + target.name);
                if (Vector3.Dot(Vector3.Cross(x, y), z) < 0f) scale.x = -scale.x;
                z.Normalize();
                y -= Vector3.Dot(y, z) * z;
                Require(y.sqrMagnitude > 0.00000001f, "握枪相对旋转的轴发生退化：" + target.name);
                y.Normalize();
                return new RelativePose
                {
                    Position = matrix.MultiplyPoint3x4(Vector3.zero),
                    Rotation = Quaternion.LookRotation(z, y),
                    Scale = scale
                };
            }
        }

        private sealed class RelativeError
        {
            public float Position;
            public float Rotation;
            public float RelativeScale;

            public void Observe(RelativePose actual, RelativePose baseline)
            {
                Position = Mathf.Max(Position, Vector3.Distance(actual.Position, baseline.Position));
                Rotation = Mathf.Max(Rotation, Quaternion.Angle(actual.Rotation, baseline.Rotation));
                RelativeScale = Mathf.Max(RelativeScale, Mathf.Abs(actual.Scale.x / baseline.Scale.x - 1f),
                    Mathf.Abs(actual.Scale.y / baseline.Scale.y - 1f), Mathf.Abs(actual.Scale.z / baseline.Scale.z - 1f));
            }

            public void RequireBelow(float positionLimit, float rotationLimit, string label)
            {
                Require(Position < positionLimit && Rotation < rotationLimit,
                    $"{label}超出约束：{Position}m / {Rotation}°（要求 <{positionLimit}m / {rotationLimit}°）。");
            }

            public JObject Report() => new JObject
            {
                ["maximumPositionErrorMeters"] = Position, ["maximumRotationErrorDegrees"] = Rotation,
                ["maximumRelativeScaleError"] = RelativeScale
            };
        }

        private sealed class WeaponPreview
        {
            public readonly AnimationClip HoldClip;
            public readonly AnimationClip FireClip;
            private readonly Animator _animator;
            private readonly int _holdHash;
            private readonly int _fireHash;
            private readonly Dictionary<string, Transform> _targets;

            public WeaponPreview(Scene scene, Transform mount, GunResourcePackSO resources)
            {
                GameObject root = Object.Instantiate(resources.ModelPrefab.gameObject, new InstantiateParameters { scene = scene });
                root.hideFlags = HideFlags.HideAndDontSave;
                root.transform.SetParent(mount, false);
                root.transform.localPosition = Vector3.zero;
                root.transform.localRotation = Quaternion.identity;
                root.SetActive(true);
                GunModelController model = root.GetComponent<GunModelController>();
                Require(model != null && model.ModelAnimator != null, "真实资源包模型缺少绑定的 GunModelController/Animator。");
                _animator = model.ModelAnimator;
                AnimatorController controller = _animator.runtimeAnimatorController as AnimatorController;
                Require(controller != null && controller.layers.Length > 0, "真实模型缺少 AnimatorController。");
                Require(resources.TryGetAnimation(WeaponPresentationAction.Hold, WeaponAnimationTarget.Model, out string holdName),
                    "真实模型资源未配置持握状态。");
                Require(resources.TryGetAnimation(WeaponPresentationAction.Fire, WeaponAnimationTarget.Model, out string fireName),
                    "真实模型资源未配置开火状态。");
                HoldClip = ResolveClip(controller, holdName, out _holdHash);
                FireClip = ResolveClip(controller, fireName, out _fireHash);
                _animator.enabled = true;
                _animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                _animator.applyRootMotion = false;
                _animator.speed = 1f;
                _animator.Rebind();

                var paths = _animator.GetComponentsInChildren<Transform>(true)
                    .ToDictionary(value => AnimationUtility.CalculateTransformPath(value, _animator.transform), value => value);
                Transform weapon = BoundModelBone(FireClip, paths, "/weapon");
                Transform offset = BoundModelBone(FireClip, paths, "/weapon/weapon_offset");
                Require(offset.IsChildOf(weapon), "真实模型的 weapon_offset 必须属于 weapon 枪身骨骼。");
                _targets = new Dictionary<string, Transform>
                {
                    ["equipmentRoot"] = root.transform, ["modelAnimatorRoot"] = _animator.transform,
                    ["weapon"] = weapon, ["weapon_offset"] = offset
                };
                Hold();
            }

            public void Hold() => Play(_holdHash, 1f);

            public void FireAt(float time)
            {
                if (time < FireClip.length) Play(_fireHash, time / FireClip.length);
                else Hold();
            }

            private void Play(int state, float normalizedTime)
            {
                _animator.Play(state, 0, normalizedTime);
                _animator.Update(0f);
                Require(_animator.GetCurrentAnimatorStateInfo(0).fullPathHash == state,
                    "真实枪模型 Animator 没有实际求值资源包配置的状态。");
            }

            public Dictionary<string, RelativePose> Capture(Transform hand) =>
                _targets.ToDictionary(pair => pair.Key, pair => RelativePose.Capture(hand, pair.Value));

            public Dictionary<string, RelativeError> NewErrors() => _targets.ToDictionary(pair => pair.Key, _ => new RelativeError());

            public void Observe(Transform hand, Dictionary<string, RelativePose> baseline, Dictionary<string, RelativeError> errors)
            {
                foreach (var pair in _targets)
                    errors[pair.Key].Observe(RelativePose.Capture(hand, pair.Value), baseline[pair.Key]);
            }

            public void RequireStable(Dictionary<string, RelativeError> errors, string label)
            {
                foreach (var pair in errors)
                {
                    pair.Value.RequireBelow(0.001f, 0.1f, label + "枪模型 " + pair.Key + " 相对右手");
                    Require(pair.Value.RelativeScale < 0.0001f,
                        $"{label}枪模型 {pair.Key} 的相对缩放不应变化：{pair.Value.RelativeScale}。");
                }
            }

            public JObject Report(Dictionary<string, RelativeError> errors)
            {
                var result = new JObject();
                foreach (var pair in errors)
                {
                    JObject entry = pair.Value.Report();
                    entry["pathFromEquipmentRoot"] = AnimationUtility.CalculateTransformPath(_targets[pair.Key], _targets["equipmentRoot"]);
                    result[pair.Key] = entry;
                }
                return result;
            }

            private static AnimationClip ResolveClip(AnimatorController controller, string name, out int hash)
            {
                AnimatorControllerLayer layer = controller.layers[0];
                AnimatorState state = layer.stateMachine.states.Select(value => value.state)
                    .SingleOrDefault(value => value.name == name || layer.name + "." + value.name == name);
                Require(state != null && state.motion is AnimationClip, "真实枪模型状态必须直接绑定 Clip：" + name);
                hash = Animator.StringToHash(layer.name + "." + state.name);
                return (AnimationClip)state.motion;
            }

            private static Transform BoundModelBone(AnimationClip clip, Dictionary<string, Transform> paths, string suffix)
            {
                string path = AnimationUtility.GetCurveBindings(clip).Where(binding => binding.type == typeof(Transform))
                    .Select(binding => binding.path).Distinct().SingleOrDefault(value => value.EndsWith(suffix, StringComparison.Ordinal));
                Require(path != null && paths.ContainsKey(path), "模型真实骨架缺少动画绑定：" + suffix);
                return paths[path];
            }
        }

        private sealed class PreviewScope : IDisposable
        {
            public readonly Scene Scene;
            public readonly GameObject Root;
            public readonly Animator Animator;
            public readonly Dictionary<string, Transform> Paths;

            public PreviewScope(Animator source)
            {
                Scene = EditorSceneManager.NewPreviewScene();
                try
                {
                    Root = Object.Instantiate(source.gameObject, new InstantiateParameters { scene = Scene });
                    Root.hideFlags = HideFlags.HideAndDontSave;
                    Root.SetActive(true);
                    Animator = Root.GetComponent<Animator>();
                    Require(Animator != null, "预览模型缺少真实 Animator。");
                    Paths = Root.GetComponentsInChildren<Transform>(true)
                        .ToDictionary(value => AnimationUtility.CalculateTransformPath(value, Root.transform), value => value);
                }
                catch
                {
                    EditorSceneManager.ClosePreviewScene(Scene);
                    throw;
                }
            }

            public void Dispose()
            {
                if (Scene.IsValid()) EditorSceneManager.ClosePreviewScene(Scene);
            }
        }
    }
}
