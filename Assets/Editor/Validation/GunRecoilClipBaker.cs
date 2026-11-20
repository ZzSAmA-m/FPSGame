using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace FPSGame.Editor.Validation
{
    /// <summary>
    /// 原始 CS2 射击是增量，导出还带有根坐标转换。以首帧归零消除静态偏移，
    /// 将后坐力烘焙到持握末帧，生成可供 Override 层播放的完整姿势。
    /// </summary>
    public static class GunRecoilClipBaker
    {
        private const float ConstantTolerance = 0.0000001f;
        // 预留帧间误差余量，避免60Hz线性插值在快速后坐阶段接近1mm上限。
        private const float MinimumMountSampleRate = 120f;
        private const float MatrixTolerance = 0.0001f;
        private static readonly string[] Position = { "m_LocalPosition.x", "m_LocalPosition.y", "m_LocalPosition.z" };
        private static readonly string[] Rotation = { "m_LocalRotation.x", "m_LocalRotation.y", "m_LocalRotation.z", "m_LocalRotation.w" };
        private static readonly string[] Scale = { "m_LocalScale.x", "m_LocalScale.y", "m_LocalScale.z" };

        public static AnimationClip Bake(AnimationClip source, AnimationClip hold, string name,
            Transform rigRoot, string handPath, string mountPath)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (hold == null) throw new ArgumentNullException(nameof(hold));
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("需要输出动画名称。", nameof(name));
            if (!Finite(source.length) || !Finite(source.frameRate) || source.length <= 0f || source.frameRate <= 0f)
                throw new ArgumentException("后坐力源动画必须具有正时长和帧率。", nameof(source));
            if (!Finite(hold.length) || hold.length < 0f)
                throw new ArgumentException("持握基准动画时长无效。", nameof(hold));

            var sourceCurves = ReadCurves(source, true);
            var holdCurves = ReadCurves(hold, false);
            if (sourceCurves.Count == 0)
                throw new ArgumentException("后坐力源动画没有 Transform 曲线。", nameof(source));
            RigSnapshot rig = new RigSnapshot(rigRoot, handPath, mountPath);
            rig.ValidateBindings(sourceCurves);
            rig.ValidateBindings(holdCurves);

            var paths = new HashSet<string>();
            var times = new SortedSet<float> { 0f, source.length };
            foreach (var pair in sourceCurves)
            {
                paths.Add(pair.Key.path);
                foreach (Keyframe key in pair.Value.keys)
                    if (key.time >= 0f && key.time <= source.length)
                        times.Add(key.time);
            }
            for (int frame = 1; frame / source.frameRate < source.length; frame++)
                times.Add(frame / source.frameRate);
            // FBX key 时间和 frame/fps 可能只差一个 ULP，合并后避免极短线段。
            var mergedTimes = new List<float>();
            foreach (float time in times)
            {
                if (mergedTimes.Count == 0 || time - mergedTimes[mergedTimes.Count - 1] > 0.000001f)
                    mergedTimes.Add(time);
                else if (time == source.length)
                    mergedTimes[mergedTimes.Count - 1] = source.length;
            }
            float[] sampleTimes = mergedTimes.ToArray();

            // 预先检查全部路径与通道，不能用默认骨长或默认旋转掩盖基准缺失。
            foreach (string path in paths)
            {
                ValidateGroup(sourceCurves, holdCurves, path, Position);
                ValidateGroup(sourceCurves, holdCurves, path, Rotation);
                ValidateGroup(sourceCurves, holdCurves, path, Scale);
            }

            var result = new AnimationClip
            {
                name = name,
                frameRate = source.frameRate,
                legacy = false,
                wrapMode = WrapMode.Once
            };
            try
            {
                var settings = AnimationUtility.GetAnimationClipSettings(source);
                settings.loopTime = false;
                AnimationUtility.SetAnimationClipSettings(result, settings);
                foreach (string path in paths)
                {
                    if (HasGroup(sourceCurves, path, Position))
                    {
                        Vector3 baseline = EvaluateVector(holdCurves, path, Position, hold.length);
                        Vector3 zero = EvaluateVector(sourceCurves, path, Position, 0f);
                        var samples = new float[sampleTimes.Length][];
                        for (int i = 0; i < sampleTimes.Length; i++)
                        {
                            Vector3 value = baseline + (EvaluateVector(sourceCurves, path, Position, sampleTimes[i]) - zero);
                            samples[i] = new[] { value.x, value.y, value.z };
                        }
                        WriteGroup(result, path, Position, sampleTimes, samples);
                    }
                    if (HasGroup(sourceCurves, path, Rotation))
                    {
                        Quaternion baseline = EvaluateRotation(holdCurves, path, hold.length);
                        Quaternion inverseZero = Quaternion.Inverse(EvaluateRotation(sourceCurves, path, 0f));
                        var samples = new float[sampleTimes.Length][];
                        Quaternion previous = baseline;
                        for (int i = 0; i < sampleTimes.Length; i++)
                        {
                            Quaternion value = Normalize(baseline * inverseZero * EvaluateRotation(sourceCurves, path, sampleTimes[i]), path);
                            if (Quaternion.Dot(previous, value) < 0f)
                                value = new Quaternion(-value.x, -value.y, -value.z, -value.w);
                            samples[i] = new[] { value.x, value.y, value.z, value.w };
                            previous = value;
                        }
                        WriteGroup(result, path, Rotation, sampleTimes, samples);
                    }
                    if (HasGroup(sourceCurves, path, Scale))
                    {
                        Vector3 value = EvaluateVector(holdCurves, path, Scale, hold.length);
                        var values = new[] { value.x, value.y, value.z };
                        WriteGroup(result, path, Scale, new[] { 0f, source.length }, new[] { values, values });
                    }
                }
                result.EnsureQuaternionContinuity();
                CorrectWeaponMount(result, hold, holdCurves, rig, handPath, mountPath);
                result.EnsureQuaternionContinuity();
                return result;
            }
            catch
            {
                UnityEngine.Object.DestroyImmediate(result);
                throw;
            }
        }

        private static void CorrectWeaponMount(AnimationClip clip, AnimationClip hold,
            Dictionary<(string path, string property), AnimationCurve> holdCurves, RigSnapshot rig,
            string handPath, string mountPath)
        {
            var curves = ReadCurves(clip, true);
            var holdCache = new Dictionary<string, Matrix4x4>();
            Matrix4x4 heldHand = rig.World(handPath, holdCurves, hold.length, holdCache);
            Matrix4x4 heldMount = rig.World(mountPath, holdCurves, hold.length, holdCache);
            Matrix4x4 handToMount = Inverse(heldHand, handPath) * heldMount;
            RequireFinite(handToMount, "持握手与武器挂点的相对矩阵");

            var times = new SortedSet<float> { 0f, clip.length };
            foreach (var pair in curves)
                foreach (Keyframe key in pair.Value.keys)
                    if (key.time >= 0f && key.time <= clip.length) times.Add(key.time);
            float rate = Mathf.Max(MinimumMountSampleRate, clip.frameRate);
            for (int frame = 1; frame / rate < clip.length; frame++) times.Add(frame / rate);
            var samples = new List<float>();
            foreach (float time in times)
            {
                if (samples.Count == 0 || time - samples[samples.Count - 1] > 0.000001f) samples.Add(time);
                else if (time == clip.length) samples[samples.Count - 1] = time;
            }
            float[] sampleTimes = samples.ToArray();
            var positions = new float[sampleTimes.Length][];
            var rotations = new float[sampleTimes.Length][];
            Quaternion previous = Quaternion.identity;
            for (int i = 0; i < sampleTimes.Length; i++)
            {
                float time = sampleTimes[i];
                var cache = new Dictionary<string, Matrix4x4>();
                Matrix4x4 hand = rig.World(handPath, curves, time, cache);
                string parentPath = rig.Parent(mountPath);
                Matrix4x4 parent = parentPath == null ? Matrix4x4.identity : rig.World(parentPath, curves, time, cache);
                Matrix4x4 local = Inverse(parent, parentPath) * hand * handToMount;
                Vector3 scale = rig.LocalScale(mountPath, curves, time);
                DecomposeMount(local, scale, mountPath, out Vector3 position, out Quaternion rotation);
                if (i > 0 && Quaternion.Dot(previous, rotation) < 0f)
                    rotation = new Quaternion(-rotation.x, -rotation.y, -rotation.z, -rotation.w);
                positions[i] = new[] { position.x, position.y, position.z };
                rotations[i] = new[] { rotation.x, rotation.y, rotation.z, rotation.w };
                previous = rotation;
            }
            // 只校正挂点；身体、手指和挂点缩放保持第一阶段的烘焙结果。
            WriteGroup(clip, mountPath, Position, sampleTimes, positions);
            WriteGroup(clip, mountPath, Rotation, sampleTimes, rotations);
        }

        private static void DecomposeMount(Matrix4x4 matrix, Vector3 expectedScale, string path,
            out Vector3 position, out Quaternion rotation)
        {
            RequireFinite(matrix, path);
            position = matrix.GetColumn(3);
            Vector3 x = matrix.GetColumn(0), y = matrix.GetColumn(1), z = matrix.GetColumn(2);
            Vector3 actualScale = new Vector3(x.magnitude, y.magnitude, z.magnitude);
            RequireUniformScale(actualScale, path);
            for (int axis = 0; axis < 3; axis++)
                if (Mathf.Abs(actualScale[axis] - expectedScale[axis]) > MatrixTolerance * Mathf.Max(1f, expectedScale[axis]))
                    throw new InvalidOperationException($"无法在保留挂点缩放时固定握持关系：{path}");
            x /= actualScale.x;
            y /= actualScale.y;
            z /= actualScale.z;
            if (Mathf.Abs(Vector3.Dot(x, y)) > MatrixTolerance || Mathf.Abs(Vector3.Dot(x, z)) > MatrixTolerance ||
                Mathf.Abs(Vector3.Dot(y, z)) > MatrixTolerance || Vector3.Dot(Vector3.Cross(x, y), z) < 1f - MatrixTolerance)
                throw new InvalidOperationException($"武器挂点矩阵含剪切或反射，不能转换为普通 Transform：{path}");
            rotation = Normalize(Quaternion.LookRotation(z, y), path);
        }

        /// <summary>只读骨架和曲线做 FK；不采样、实例化或修改真实角色。</summary>
        private sealed class RigSnapshot
        {
            private readonly Dictionary<string, RigNode> _nodes = new();

            public RigSnapshot(Transform root, string handPath, string mountPath)
            {
                if (root == null) throw new ArgumentNullException(nameof(root));
                if (string.IsNullOrWhiteSpace(handPath)) throw new ArgumentException("必须提供相对骨架的手部路径。", nameof(handPath));
                if (string.IsNullOrWhiteSpace(mountPath)) throw new ArgumentException("必须提供相对骨架的武器挂点路径。", nameof(mountPath));
                foreach (Transform bone in root.GetComponentsInChildren<Transform>(true))
                {
                    string path = AnimationUtility.CalculateTransformPath(bone, root);
                    if (_nodes.ContainsKey(path)) throw new InvalidOperationException("骨架包含重名动画绑定路径：" + path);
                    RequireFinite(bone.localPosition, path);
                    RequireFinite(bone.localScale, path);
                    _nodes.Add(path, new RigNode
                    {
                        Bone = bone,
                        Parent = bone == root ? null : AnimationUtility.CalculateTransformPath(bone.parent, root),
                        Position = bone.localPosition,
                        Rotation = Normalize(bone.localRotation, path),
                        Scale = bone.localScale
                    });
                }
                if (!_nodes.TryGetValue(handPath, out RigNode hand))
                    throw new ArgumentException("骨架中找不到手部动画路径：" + handPath, nameof(handPath));
                if (!_nodes.TryGetValue(mountPath, out RigNode mount))
                    throw new ArgumentException("骨架中找不到武器挂点路径：" + mountPath, nameof(mountPath));
                if (hand.Bone == mount.Bone || hand.Bone.IsChildOf(mount.Bone))
                    throw new ArgumentException("手部不能是武器挂点自身或其后代，否则握持校正会形成依赖环。", nameof(mountPath));
            }

            public string Parent(string path) => _nodes[path].Parent;

            public void ValidateBindings(Dictionary<(string path, string property), AnimationCurve> curves)
            {
                var paths = new HashSet<string>();
                foreach (var pair in curves)
                {
                    if (!_nodes.ContainsKey(pair.Key.path))
                        throw new InvalidOperationException("真实骨架不存在动画绑定：" + pair.Key.path);
                    paths.Add(pair.Key.path);
                }
                foreach (string path in paths)
                foreach (string[] group in new[] { Position, Rotation, Scale })
                {
                    if (!HasGroup(curves, path, group)) continue;
                    foreach (string property in group)
                        if (!curves.ContainsKey((path, property)))
                            throw new InvalidOperationException($"动画缺少完整变换通道：{path} / {property}");
                }
            }

            public Vector3 LocalScale(string path, Dictionary<(string path, string property), AnimationCurve> curves, float time) =>
                HasGroup(curves, path, Scale) ? EvaluateVector(curves, path, Scale, time) : _nodes[path].Scale;

            public Matrix4x4 World(string path, Dictionary<(string path, string property), AnimationCurve> curves,
                float time, Dictionary<string, Matrix4x4> cache)
            {
                if (cache.TryGetValue(path, out Matrix4x4 value)) return value;
                RigNode node = _nodes[path];
                Vector3 position = HasGroup(curves, path, Position) ? EvaluateVector(curves, path, Position, time) : node.Position;
                Quaternion rotation = HasGroup(curves, path, Rotation) ? EvaluateRotation(curves, path, time) : node.Rotation;
                Vector3 scale = LocalScale(path, curves, time);
                RequireUniformScale(scale, path);
                Matrix4x4 local = Matrix4x4.TRS(position, rotation, scale);
                value = node.Parent == null ? local : World(node.Parent, curves, time, cache) * local;
                RequireFinite(value, path);
                cache.Add(path, value);
                return value;
            }
        }

        private sealed class RigNode
        {
            public Transform Bone;
            public string Parent;
            public Vector3 Position;
            public Quaternion Rotation;
            public Vector3 Scale;
        }

        private static Matrix4x4 Inverse(Matrix4x4 value, string path)
        {
            RequireFinite(value, path);
            float determinant = value.determinant;
            if (!Finite(determinant) || Mathf.Abs(determinant) < 0.00000001f)
                throw new InvalidOperationException("骨架变换不可逆：" + path);
            Matrix4x4 result = value.inverse;
            RequireFinite(result, path);
            return result;
        }

        private static void RequireUniformScale(Vector3 scale, string path)
        {
            RequireFinite(scale, path);
            float tolerance = MatrixTolerance * Mathf.Max(1f, scale.x, scale.y, scale.z);
            if (scale.x <= 0f || scale.y <= 0f || scale.z <= 0f ||
                Mathf.Abs(scale.x - scale.y) > tolerance || Mathf.Abs(scale.x - scale.z) > tolerance)
                throw new InvalidOperationException("握持校正不支持零、负或非均匀缩放，避免静默丢失矩阵剪切：" + path);
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        private static void RequireFinite(Vector3 value, string path)
        {
            if (!Finite(value.x) || !Finite(value.y) || !Finite(value.z))
                throw new InvalidOperationException("骨架或动画存在非有限向量：" + path);
        }

        private static void RequireFinite(Matrix4x4 value, string path)
        {
            for (int i = 0; i < 16; i++)
                if (!Finite(value[i])) throw new InvalidOperationException("骨架或动画存在非有限矩阵：" + path);
        }

        private static Dictionary<(string path, string property), AnimationCurve> ReadCurves(AnimationClip clip, bool rejectUnsupported)
        {
            var curves = new Dictionary<(string, string), AnimationCurve>();
            foreach (EditorCurveBinding binding in AnimationUtility.GetCurveBindings(clip))
            {
                // Euler 编辑辅助曲线不能与最终四元数曲线一起写出。
                if (binding.type == typeof(Transform) &&
                    (binding.propertyName.IndexOf("Euler", StringComparison.OrdinalIgnoreCase) >= 0))
                    continue;
                bool supported = binding.type == typeof(Transform) &&
                    (Array.IndexOf(Position, binding.propertyName) >= 0 ||
                     Array.IndexOf(Rotation, binding.propertyName) >= 0 ||
                     Array.IndexOf(Scale, binding.propertyName) >= 0);
                if (!supported)
                {
                    if (rejectUnsupported)
                        throw new InvalidOperationException($"不支持的后坐力绑定：{binding.path} / {binding.type.Name}.{binding.propertyName}");
                    continue;
                }
                AnimationCurve curve = AnimationUtility.GetEditorCurve(clip, binding);
                if (curve == null || curve.length == 0)
                    throw new InvalidOperationException($"动画曲线为空：{clip.name} / {binding.path} / {binding.propertyName}");
                foreach (Keyframe key in curve.keys)
                    if (!Finite(key.time) || !Finite(key.value) || float.IsNaN(key.inTangent) || float.IsNaN(key.outTangent))
                        throw new InvalidOperationException($"动画曲线含无效时间或数值：{clip.name} / {binding.path} / {binding.propertyName}");
                curves.Add((binding.path, binding.propertyName), curve);
            }
            return curves;
        }

        private static bool HasGroup(Dictionary<(string path, string property), AnimationCurve> curves, string path, string[] properties)
        {
            foreach (string property in properties)
                if (curves.ContainsKey((path, property))) return true;
            return false;
        }

        private static void ValidateGroup(Dictionary<(string path, string property), AnimationCurve> source,
            Dictionary<(string path, string property), AnimationCurve> hold, string path, string[] properties)
        {
            if (!HasGroup(source, path, properties)) return;
            foreach (string property in properties)
            {
                if (!source.ContainsKey((path, property)))
                    throw new InvalidOperationException($"后坐力源缺少完整通道：{path} / {property}");
                if (!hold.ContainsKey((path, property)))
                    throw new InvalidOperationException($"持握基准缺少绑定：{path} / {property}");
            }
        }

        private static Vector3 EvaluateVector(Dictionary<(string path, string property), AnimationCurve> curves,
            string path, string[] properties, float time)
        {
            Vector3 value = new Vector3(curves[(path, properties[0])].Evaluate(time),
                curves[(path, properties[1])].Evaluate(time), curves[(path, properties[2])].Evaluate(time));
            RequireFinite(value, path);
            return value;
        }

        private static Quaternion EvaluateRotation(Dictionary<(string path, string property), AnimationCurve> curves, string path, float time)
        {
            return Normalize(new Quaternion(curves[(path, Rotation[0])].Evaluate(time),
                curves[(path, Rotation[1])].Evaluate(time), curves[(path, Rotation[2])].Evaluate(time),
                curves[(path, Rotation[3])].Evaluate(time)), path);
        }

        private static Quaternion Normalize(Quaternion rotation, string path)
        {
            float squareLength = Quaternion.Dot(rotation, rotation);
            if (float.IsNaN(squareLength) || float.IsInfinity(squareLength) || squareLength <= 0.00000001f)
                throw new InvalidOperationException($"动画存在无效四元数：{path}");
            float inverseLength = 1f / Mathf.Sqrt(squareLength);
            return new Quaternion(rotation.x * inverseLength, rotation.y * inverseLength,
                rotation.z * inverseLength, rotation.w * inverseLength);
        }

        private static void WriteGroup(AnimationClip clip, string path, string[] properties, float[] times, float[][] values)
        {
            bool constant = true;
            for (int i = 1; i < values.Length && constant; i++)
                for (int channel = 0; channel < properties.Length; channel++)
                    if (Mathf.Abs(values[i][channel] - values[0][channel]) > ConstantTolerance)
                    {
                        constant = false;
                        break;
                    }
            for (int channel = 0; channel < properties.Length; channel++)
            {
                var keys = new Keyframe[constant ? 2 : times.Length];
                if (constant)
                {
                    keys[0] = new Keyframe(times[0], values[0][channel]);
                    keys[1] = new Keyframe(times[times.Length - 1], values[0][channel]);
                }
                else
                    for (int i = 0; i < times.Length; i++) keys[i] = new Keyframe(times[i], values[i][channel]);
                var curve = new AnimationCurve(keys);
                for (int i = 0; i < keys.Length; i++)
                {
                    AnimationUtility.SetKeyLeftTangentMode(curve, i, AnimationUtility.TangentMode.Linear);
                    AnimationUtility.SetKeyRightTangentMode(curve, i, AnimationUtility.TangentMode.Linear);
                }
                AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(path, typeof(Transform), properties[channel]), curve);
            }
        }
    }
}
