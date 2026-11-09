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
        private static readonly string[] Position = { "m_LocalPosition.x", "m_LocalPosition.y", "m_LocalPosition.z" };
        private static readonly string[] Rotation = { "m_LocalRotation.x", "m_LocalRotation.y", "m_LocalRotation.z", "m_LocalRotation.w" };
        private static readonly string[] Scale = { "m_LocalScale.x", "m_LocalScale.y", "m_LocalScale.z" };

        public static AnimationClip Bake(AnimationClip source, AnimationClip hold, string name)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (hold == null) throw new ArgumentNullException(nameof(hold));
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("需要输出动画名称。", nameof(name));
            if (source.length <= 0f || source.frameRate <= 0f)
                throw new ArgumentException("后坐力源动画必须具有正时长和帧率。", nameof(source));

            var sourceCurves = ReadCurves(source, true);
            var holdCurves = ReadCurves(hold, false);
            if (sourceCurves.Count == 0)
                throw new ArgumentException("后坐力源动画没有 Transform 曲线。", nameof(source));

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
                return result;
            }
            catch
            {
                UnityEngine.Object.DestroyImmediate(result);
                throw;
            }
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
            return new Vector3(curves[(path, properties[0])].Evaluate(time),
                curves[(path, properties[1])].Evaluate(time), curves[(path, properties[2])].Evaluate(time));
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
