using UnityEditor;
using UnityEngine;

/// <summary>
/// 删掉 CS2 动画里骨架 dummy 根的 pos/rot/scale。
/// 位移单位对齐在预览脚本里做，避免导入后再缩放一次。
/// </summary>
public class Cs2AnimDummyRootFix : AssetPostprocessor
{
    void OnPostprocessAnimation(GameObject root, AnimationClip clip)
    {
        var path = assetPath.Replace('\\', '/');
        if (path.IndexOf("/CS2/Agents/", System.StringComparison.OrdinalIgnoreCase) < 0)
            return;
        if (clip == null || clip.name.StartsWith("__preview"))
            return;

        foreach (var binding in AnimationUtility.GetCurveBindings(clip))
        {
            if (binding.path.IndexOf('/') >= 0)
                continue;
            AnimationUtility.SetEditorCurve(clip, binding, null);
        }
        clip.EnsureQuaternionContinuity();
    }
}
