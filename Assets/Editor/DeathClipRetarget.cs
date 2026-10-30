using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class DeathClipRetarget
{
    const string SourceFolder = "Assets/Resources/CS2/Agents/Shared/Death";
    const string OutputFolder = "Assets/Resources/CS2/Agents/Shared/Death/ForFbi";
    const string ReferenceModel = "Assets/Resources/CS2/Agents/ctm_fbi_meter/ctm_fbi.fbx";

    [MenuItem("Tools/CS2/Retarget FBI Death Clips")]
    public static void Retarget()
    {
        var reference = AssetDatabase.LoadAssetAtPath<GameObject>(ReferenceModel);
        Directory.CreateDirectory(OutputFolder);
        int count = 0;
        foreach (var guid in AssetDatabase.FindAssets("death_", new[] { SourceFolder }))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            if (!path.EndsWith(".glb") || path.Contains("/ForFbi/"))
                continue;

            var source = LoadClip(path);
            var glb = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (source == null || glb == null || reference == null)
                continue;

            var fileName = Path.GetFileNameWithoutExtension(path);
            var outputPath = OutputFolder + "/" + fileName + ".anim";
            var baked = Bake(source, glb, reference, fileName);
            if (AssetDatabase.LoadAssetAtPath<AnimationClip>(outputPath) != null)
                AssetDatabase.DeleteAsset(outputPath);
            AssetDatabase.CreateAsset(baked, outputPath);
            count++;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("已按 FBI 骨架重烘 " + count + " 条死亡动画到 " + OutputFolder);
    }

    static AnimationClip LoadClip(string path)
    {
        foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(path))
        {
            if (asset is AnimationClip clip && !clip.name.StartsWith("__preview"))
                return clip;
        }

        return null;
    }

    static AnimationClip Bake(AnimationClip source, GameObject glbPrefab, GameObject fbxPrefab, string clipName)
    {
        var glb = Object.Instantiate(glbPrefab);
        var fbx = Object.Instantiate(fbxPrefab);
        glb.hideFlags = HideFlags.HideAndDontSave;
        fbx.hideFlags = HideFlags.HideAndDontSave;
        try
        {
            var bones = CollectBones(glb, fbx);
            var times = CollectTimes(source);
            var samples = new Dictionary<string, List<BoneSample>>();
            foreach (var bone in bones)
                samples[bone.Name] = new List<BoneSample>();

            AnimationMode.StartAnimationMode();
            foreach (var time in times)
            {
                AnimationMode.BeginSampling();
                AnimationMode.SampleAnimationClip(glb, source, time);
                AnimationMode.EndSampling();
                foreach (var bone in bones)
                {
                    var localRot = Quaternion.Inverse(bone.Target.parent.rotation) * bone.Source.rotation;
                    var localPos = bone.Target.parent.InverseTransformPoint(bone.Source.position);
                    bone.Target.localRotation = localRot;
                    bone.Target.localPosition = localPos;
                    samples[bone.Name].Add(new BoneSample { Time = time, Position = localPos, Rotation = localRot });
                }
            }

            AnimationMode.StopAnimationMode();

            var dest = new AnimationClip
            {
                name = clipName,
                frameRate = source.frameRate,
                legacy = false,
                wrapMode = WrapMode.ClampForever,
            };
            var settings = AnimationUtility.GetAnimationClipSettings(source);
            settings.loopTime = false;
            AnimationUtility.SetAnimationClipSettings(dest, settings);

            foreach (var bone in bones)
            {
                var path = RelativePath(bone.Target, fbx.transform);
                WriteVector(dest, path, "m_LocalPosition", samples[bone.Name], sample => sample.Position);
                WriteQuaternion(dest, path, samples[bone.Name]);
            }

            if (clipName.Contains("poses"))
                FlattenToLastPose(dest);
            return dest;
        }
        finally
        {
            if (AnimationMode.InAnimationMode())
                AnimationMode.StopAnimationMode();
            Object.DestroyImmediate(glb);
            Object.DestroyImmediate(fbx);
        }
    }

    static List<BonePair> CollectBones(GameObject glb, GameObject fbx)
    {
        var list = new List<BonePair>();
        foreach (var source in glb.GetComponentsInChildren<Transform>(true))
        {
            if (source.GetComponent<Renderer>() != null || source.parent == null)
                continue;
            if (source.name.Contains("camera") || source.name.Contains("wpn") || source.name.Contains("attach"))
                continue;
            Transform target = null;
            foreach (var candidate in fbx.GetComponentsInChildren<Transform>(true))
            {
                if (candidate.name == source.name && candidate.GetComponent<Renderer>() == null && candidate.parent != null)
                {
                    target = candidate;
                    break;
                }
            }

            if (target == null)
                continue;
            list.Add(new BonePair { Name = source.name, Source = source, Target = target });
        }

        list.Sort((a, b) => Depth(a.Target).CompareTo(Depth(b.Target)));
        return list;
    }

    static int Depth(Transform t)
    {
        int depth = 0;
        while (t.parent != null)
        {
            depth++;
            t = t.parent;
        }

        return depth;
    }

    static List<float> CollectTimes(AnimationClip clip)
    {
        var times = new SortedSet<float> { 0f, clip.length };
        float step = 1f / 60f;
        for (float time = 0f; time < clip.length; time += step)
            times.Add(time);
        foreach (var binding in AnimationUtility.GetCurveBindings(clip))
        {
            var curve = AnimationUtility.GetEditorCurve(clip, binding);
            if (curve == null)
                continue;
            foreach (var key in curve.keys)
                times.Add(key.time);
        }

        return new List<float>(times);
    }

    static string RelativePath(Transform bone, Transform root)
    {
        var names = new List<string>();
        var current = bone;
        while (current != null && current != root)
        {
            names.Add(current.name);
            current = current.parent;
        }

        names.Reverse();
        return string.Join("/", names);
    }

    static void WriteVector(AnimationClip clip, string path, string property, List<BoneSample> samples, System.Func<BoneSample, Vector3> select)
    {
        var x = new AnimationCurve();
        var y = new AnimationCurve();
        var z = new AnimationCurve();
        foreach (var sample in samples)
        {
            var value = select(sample);
            x.AddKey(new Keyframe(sample.Time, value.x));
            y.AddKey(new Keyframe(sample.Time, value.y));
            z.AddKey(new Keyframe(sample.Time, value.z));
        }

        SetLinear(x);
        SetLinear(y);
        SetLinear(z);
        AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(path, typeof(Transform), property + ".x"), x);
        AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(path, typeof(Transform), property + ".y"), y);
        AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(path, typeof(Transform), property + ".z"), z);
    }

    static void WriteQuaternion(AnimationClip clip, string path, List<BoneSample> samples)
    {
        var x = new AnimationCurve();
        var y = new AnimationCurve();
        var z = new AnimationCurve();
        var w = new AnimationCurve();
        var previous = Quaternion.identity;
        var hasPrevious = false;
        foreach (var sample in samples)
        {
            var value = sample.Rotation;
            if (hasPrevious && Quaternion.Dot(previous, value) < 0f)
                value = new Quaternion(-value.x, -value.y, -value.z, -value.w);
            previous = value;
            hasPrevious = true;
            x.AddKey(new Keyframe(sample.Time, value.x));
            y.AddKey(new Keyframe(sample.Time, value.y));
            z.AddKey(new Keyframe(sample.Time, value.z));
            w.AddKey(new Keyframe(sample.Time, value.w));
        }

        AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(path, typeof(Transform), "m_LocalRotation.x"), x);
        AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(path, typeof(Transform), "m_LocalRotation.y"), y);
        AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(path, typeof(Transform), "m_LocalRotation.z"), z);
        AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(path, typeof(Transform), "m_LocalRotation.w"), w);
    }

    static void FlattenToLastPose(AnimationClip clip)
    {
        foreach (var binding in AnimationUtility.GetCurveBindings(clip))
        {
            var curve = AnimationUtility.GetEditorCurve(clip, binding);
            if (curve == null || curve.length == 0)
                continue;
            float value = curve.keys[curve.length - 1].value;
            var held = new AnimationCurve(
                new Keyframe(0f, value),
                new Keyframe(Mathf.Max(clip.length, 0.01f), value));
            AnimationUtility.SetEditorCurve(clip, binding, held);
        }
    }

    static void SetLinear(AnimationCurve curve)
    {
        for (int i = 0; i < curve.length; i++)
        {
            AnimationUtility.SetKeyLeftTangentMode(curve, i, AnimationUtility.TangentMode.Linear);
            AnimationUtility.SetKeyRightTangentMode(curve, i, AnimationUtility.TangentMode.Linear);
        }
    }

    struct BonePair
    {
        public string Name;
        public Transform Source;
        public Transform Target;
    }

    struct BoneSample
    {
        public float Time;
        public Vector3 Position;
        public Quaternion Rotation;
    }
}
