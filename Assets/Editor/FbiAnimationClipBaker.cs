using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 把 CS2 FBI 动画烘焙成 Animator 可直接播放的 clip：
/// localPosition × 0.01（米 → FBX 骨架 scale 100），并去掉 scale 曲线以免骨架被写成 1。
/// </summary>
public static class FbiAnimationClipBaker
{
    public const string OutputFolder = "Assets/Resources/CS2/Agents/ctm_fbi/Animations/Adapted";
    public const string PlaceholderPath = "Assets/Prefabs/Player/FbiClip.anim";
    public const string ControllerPath = "Assets/Prefabs/Player/Player.controller";
    public const float PositionScale = 0.01f;

    static readonly string[] ResourcePaths =
    {
        "CS2/Agents/ctm_fbi/Animations/ctm_fbi_tp_rifle_move",
        "CS2/Agents/ctm_fbi/Animations/ctm_fbi_tp_pistol_move",
        "CS2/Agents/ctm_fbi/Animations/ctm_fbi_ak47_p250",
        "CS2/Agents/ctm_fbi/Animations/ctm_fbi_tp_knife_move",
        "CS2/Agents/ctm_fbi/Animations/ctm_fbi_knife_butterfly",
    };

    static bool baking;
    static int packIndex;
    static int assetIndex;
    static UnityEngine.Object[] currentPackAssets;
    static HashSet<string> written;
    static int baked;
    static int skippedEmpty;
    static int duplicates;
    static int queuedEstimate;
    static string lastStatus = "idle";

    public static bool IsBaking => baking;
    public static string LastStatus => lastStatus;

    [MenuItem("Tools/FBI/Bake Adapted Animation Clips")]
    public static void BakeFromMenu()
    {
        StartBake();
    }

    public static string StartBake()
    {
        if (baking)
            return lastStatus;

        Directory.CreateDirectory(OutputFolder.Replace('\\', '/'));
        written = new HashSet<string>();
        baked = 0;
        skippedEmpty = 0;
        duplicates = 0;
        packIndex = 0;
        assetIndex = 0;
        currentPackAssets = null;
        queuedEstimate = 0;
        baking = true;
        lastStatus = "baking starting";
        EditorApplication.update -= BakeStep;
        EditorApplication.update += BakeStep;
        return lastStatus;
    }

    static void BakeStep()
    {
        if (!baking)
        {
            EditorApplication.update -= BakeStep;
            return;
        }

        if (currentPackAssets == null)
        {
            if (packIndex == 0)
                EnsurePlaceholderClip();
            if (packIndex >= ResourcePaths.Length)
            {
                FinishBake();
                return;
            }

            currentPackAssets = Resources.LoadAll(ResourcePaths[packIndex], typeof(AnimationClip));
            assetIndex = 0;
            queuedEstimate += currentPackAssets.Length;
            lastStatus = $"pack {packIndex + 1}/{ResourcePaths.Length} loaded={currentPackAssets.Length}";
            return;
        }

        const int perTick = 1;
        for (var n = 0; n < perTick && assetIndex < currentPackAssets.Length; n++, assetIndex++)
        {
            var src = currentPackAssets[assetIndex] as AnimationClip;
            if (src == null || src.name.StartsWith("__preview"))
                continue;

            var shortName = ShortName(src.name);
            if (string.IsNullOrEmpty(shortName))
                continue;
            if (!written.Add(shortName))
            {
                duplicates++;
                continue;
            }

            var dst = Adapt(src);
            if (dst == null)
            {
                skippedEmpty++;
                continue;
            }

            var path = $"{OutputFolder}/{shortName}.anim";
            if (AssetDatabase.LoadAssetAtPath<AnimationClip>(path) != null)
                AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(dst, path);
            baked++;
        }

        lastStatus = $"baking pack {packIndex + 1}/{ResourcePaths.Length} clip {assetIndex}/{currentPackAssets.Length} done={baked}";

        if (assetIndex >= currentPackAssets.Length)
        {
            currentPackAssets = null;
            packIndex++;
            AssetDatabase.SaveAssets();
        }
    }

    static void FinishBake()
    {
        EditorApplication.update -= BakeStep;
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        baking = false;
        lastStatus = $"baked={baked} skippedEmpty={skippedEmpty} duplicates={duplicates} scanned={queuedEstimate} out={OutputFolder}";
    }

    public static string Bake()
    {
        return StartBake();
    }

    public static AnimationClip Adapt(AnimationClip src)
    {
        if (src == null)
            return null;

        var bindings = AnimationUtility.GetCurveBindings(src);
        var dst = new AnimationClip
        {
            name = ShortName(src.name),
            frameRate = src.frameRate > 0f ? src.frameRate : 30f,
            wrapMode = src.wrapMode,
            legacy = false,
        };
        AnimationUtility.SetAnimationClipSettings(dst, AnimationUtility.GetAnimationClipSettings(src));

        var copied = 0;
        foreach (var binding in bindings)
        {
            if (binding.path.IndexOf('/') < 0)
                continue;
            if (binding.propertyName.StartsWith("m_LocalScale"))
                continue;

            var curve = AnimationUtility.GetEditorCurve(src, binding);
            if (curve == null)
                continue;

            if (binding.propertyName.StartsWith("m_LocalPosition"))
            {
                for (var i = 0; i < curve.length; i++)
                {
                    var key = curve[i];
                    key.value *= PositionScale;
                    key.inTangent *= PositionScale;
                    key.outTangent *= PositionScale;
                    curve.MoveKey(i, key);
                }
            }

            AnimationUtility.SetEditorCurve(dst, binding, curve);
            copied++;
        }

        if (copied == 0)
        {
            Object.DestroyImmediate(dst);
            return null;
        }

        dst.EnsureQuaternionContinuity();
        return dst;
    }

    public static void EnsurePlaceholderClip()
    {
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(PlaceholderPath);
        if (clip != null)
            return;

        Directory.CreateDirectory(Path.GetDirectoryName(PlaceholderPath) ?? "Assets/Prefabs/Player");
        clip = new AnimationClip { name = "FbiClip", frameRate = 30f, legacy = false };
        var settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = true;
        AnimationUtility.SetAnimationClipSettings(clip, settings);
        AssetDatabase.CreateAsset(clip, PlaceholderPath);
    }

    static string ShortName(string clipName)
    {
        var bar = clipName.LastIndexOf('|');
        return bar >= 0 && bar < clipName.Length - 1 ? clipName.Substring(bar + 1) : clipName;
    }
}
