using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.InputSystem;
using UnityEngine.Playables;

/// <summary>
/// CS2 FBI 探员动画预览。Play 后用屏幕左上角列表或 Inspector 切换片段。
/// 左右方向键上一条/下一条，空格重播，数字键 1-9 跳到常用动作。
/// </summary>
[RequireComponent(typeof(Animator))]
public class FbiAnimPreview : MonoBehaviour
{
    static readonly string[] ResourcePaths =
    {
        "CS2/Agents/ctm_fbi/Animations/ctm_fbi_tp_rifle_move",
        "CS2/Agents/ctm_fbi/Animations/ctm_fbi_tp_pistol_move",
        "CS2/Agents/ctm_fbi/Animations/ctm_fbi_ak47_p250",
        "CS2/Agents/ctm_fbi/Animations/ctm_fbi_tp_knife_move",
        "CS2/Agents/ctm_fbi/Animations/ctm_fbi_knife_butterfly",
    };

    static readonly string[] HotkeyShortNames =
    {
        "tp_idle_rifle",
        "tp_walk_n_rifle",
        "tp_run_n_rifle",
        "tp_idle_crouch_rifle",
        "tp_jump_stand_rifle",
        "tp_idle_knife",
        "tp_walk_n_knife",
        "fp_draw_butterfly",
        "fp_lookat01_butterfly",
    };

    [Serializable]
    public class ClipEntry
    {
        public string shortName;
        public string category;
        public string pack;
        public AnimationClip clip;
    }

    [SerializeField, ReadOnly] Animator animator;
    [SerializeField, ReadOnly] List<ClipEntry> clips = new();

    [BoxGroup("预览"), LabelText("分类")]
    [ValueDropdown(nameof(CategoryDropdown))]
    [OnValueChanged(nameof(OnFilterChanged))]
    [SerializeField] string categoryFilter = "全部";

    [BoxGroup("预览"), LabelText("搜索")]
    [OnValueChanged(nameof(OnFilterChanged))]
    [SerializeField] string search = "";

    [BoxGroup("预览"), LabelText("当前片段")]
    [ValueDropdown(nameof(FilteredClipDropdown))]
    [OnValueChanged(nameof(PlaySelected))]
    [SerializeField] int selectedIndex;

    [BoxGroup("预览"), LabelText("速度"), PropertyRange(0.1f, 2f)]
    [OnValueChanged(nameof(ApplySpeed))]
    [SerializeField] float speed = 1f;

    [BoxGroup("预览"), LabelText("根运动")]
    [OnValueChanged(nameof(ApplyRootMotion))]
    [SerializeField] bool applyRootMotion;

    [BoxGroup("网格"), LabelText("第三人称")]
    [OnValueChanged(nameof(ApplyMeshVisibility))]
    [SerializeField] bool showThirdPerson = true;

    [BoxGroup("网格"), LabelText("第一人称")]
    [OnValueChanged(nameof(ApplyMeshVisibility))]
    [SerializeField] bool showFirstPerson;

    [BoxGroup("网格"), LabelText("拆弹包")]
    [OnValueChanged(nameof(ApplyMeshVisibility))]
    [SerializeField] bool showDefuseKit;

    [BoxGroup("网格"), LabelText("切片段时自动显隐网格")]
    [SerializeField] bool autoToggleMeshes = true;

    PlayableGraph graph;
    AnimationClipPlayable clipPlayable;
    RuntimeAnimatorController boundController;
    Transform armature;
    Vector3 armatureRestScale = Vector3.one;
    Quaternion armatureRestRotation = Quaternion.identity;
    readonly Dictionary<AnimationClip, AnimationClip> adaptedClips = new();
    Vector2 scroll;
    string guiSearch = "";
    readonly List<int> filtered = new();

    [Button("重新加载 Clip"), PropertyOrder(-1)]
    public void ReloadClips()
    {
        LoadClips();
        OnFilterChanged();
        PlaySelected();
    }

    [ButtonGroup("导航"), Button("上一条")]
    void InspectorPrev() => Step(-1);

    [ButtonGroup("导航"), Button("下一条")]
    void InspectorNext() => Step(1);

    [ButtonGroup("导航"), Button("重播")]
    void InspectorReplay() => PlaySelected();

    void Awake()
    {
        ResolveAnimator();
        EnsureArmaturePath();
        CaptureArmatureRest();
        if (animator != null)
        {
            animator.applyRootMotion = applyRootMotion;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            boundController = animator.runtimeAnimatorController;
        }

        LoadClips();
        OnFilterChanged();
        ApplyMeshVisibility();
    }

    // Model 空壳上常有无 Avatar 的 Animator；真正的 ctm_fbiAvatar 在子物体上。
    void ResolveAnimator()
    {
        animator = GetComponent<Animator>();
        if (animator == null || animator.avatar == null || !animator.avatar.isValid)
        {
            Animator best = null;
            foreach (var candidate in GetComponentsInChildren<Animator>(true))
            {
                if (candidate.avatar == null || !candidate.avatar.isValid)
                    continue;
                best = candidate;
                break;
            }

            if (best != null)
                animator = best;
        }

        if (animator == null)
            animator = GetComponentInChildren<Animator>(true);

        foreach (var extra in GetComponentsInChildren<Animator>(true))
        {
            if (extra != animator)
                extra.enabled = false;
        }

        if (animator != null)
            animator.enabled = true;
    }

    // 动画曲线里骨架名用反斜杠；FBX 导入后物体名是正斜杠，对不上就会停在 bind pose。
    void EnsureArmaturePath()
    {
        const string forward = "agents/models/ctm_fbi/ctm_fbi.vmdl_c";
        const string backslash = "agents\\models\\ctm_fbi\\ctm_fbi.vmdl_c";

        // 优先在驱动用的 Animator 下找，兼容挂在 Model / ctm_fbi 两种层级。
        var searchRoot = animator != null ? animator.transform : transform;
        armature = FindArmature(searchRoot, forward, backslash);
        if (armature == null && searchRoot != transform)
            armature = FindArmature(transform, forward, backslash);
    }

    static Transform FindArmature(Transform root, string forward, string backslash)
    {
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
        {
            var n = t.name;
            if (n == backslash || n == forward || n.Replace('\\', '/') == forward)
            {
                // 只要骨骼根，不要同名的 SkinnedMesh。
                if (t.GetComponent<Renderer>() != null)
                    continue;
                if (n != backslash)
                    t.name = backslash;
                return t;
            }
        }

        return null;
    }

    void CaptureArmatureRest()
    {
        if (armature == null)
            return;
        armatureRestScale = armature.localScale;
        armatureRestRotation = armature.localRotation;
        // 导入缩放若已被动画写成 1，强制回到 FBX 的 100。
        if (armatureRestScale.x < 10f)
            armatureRestScale = new Vector3(100f, 100f, 100f);
    }

    void LateUpdate()
    {
        if (graph.IsValid())
            graph.Evaluate(Time.deltaTime);
        RestoreArmatureRest();
    }

    void OnEnable()
    {
        if (animator == null || animator.avatar == null || !animator.avatar.isValid)
            ResolveAnimator();
        if (armature == null)
        {
            EnsureArmaturePath();
            CaptureArmatureRest();
        }

        if (clips.Count == 0)
            LoadClips();
        OnFilterChanged();
        JumpToShortName("tp_idle_rifle");
    }

    void OnDisable()
    {
        DestroyGraph();
        if (animator != null && boundController != null)
            animator.runtimeAnimatorController = boundController;
    }

    void Update()
    {
        var kb = Keyboard.current;
        if (kb == null)
            return;

        if (kb.leftArrowKey.wasPressedThisFrame)
            Step(-1);
        if (kb.rightArrowKey.wasPressedThisFrame)
            Step(1);
        if (kb.spaceKey.wasPressedThisFrame)
            PlaySelected();

        for (var i = 0; i < HotkeyShortNames.Length; i++)
        {
            var key = kb[(Key)((int)Key.Digit1 + i)];
            if (key != null && key.wasPressedThisFrame)
                JumpToShortName(HotkeyShortNames[i]);
        }
    }

    void OnGUI()
    {
        const float width = 420f;
        GUILayout.BeginArea(new Rect(12f, 12f, width, Screen.height - 24f), GUI.skin.box);
        GUILayout.Label("FBI 动画预览  " + (clips.Count > 0 ? $"{selectedIndex + 1}/{clips.Count}" : "无 Clip"));
        if (clips.Count > 0 && selectedIndex >= 0 && selectedIndex < clips.Count)
        {
            var cur = clips[selectedIndex];
            GUILayout.Label(cur.category + "  |  " + cur.shortName);
            var len = cur.clip != null ? cur.clip.length : 0f;
            var t = clipPlayable.IsValid() ? (float)clipPlayable.GetTime() : 0f;
            GUILayout.Label(len <= 0.001f ? "单帧姿势" : $"{t:0.00} / {len:0.00}s  x{speed:0.0}");
        }

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("上一条", GUILayout.Height(28f)))
            Step(-1);
        if (GUILayout.Button("重播", GUILayout.Height(28f)))
            PlaySelected();
        if (GUILayout.Button("下一条", GUILayout.Height(28f)))
            Step(1);
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        GUILayout.Label("搜索", GUILayout.Width(36f));
        var nextSearch = GUILayout.TextField(guiSearch);
        if (nextSearch != guiSearch)
        {
            guiSearch = nextSearch;
            search = guiSearch;
            OnFilterChanged();
        }
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        DrawCategoryButton("全部");
        DrawCategoryButton("步枪移动");
        DrawCategoryButton("手枪移动");
        DrawCategoryButton("刀具移动");
        GUILayout.EndHorizontal();
        GUILayout.BeginHorizontal();
        DrawCategoryButton("第一人称");
        DrawCategoryButton("第三人称持枪");
        DrawCategoryButton("第三人称持刀");
        GUILayout.EndHorizontal();

        var nextTp = GUILayout.Toggle(showThirdPerson, "第三人称网格");
        var nextFp = GUILayout.Toggle(showFirstPerson, "第一人称网格");
        var nextKit = GUILayout.Toggle(showDefuseKit, "拆弹包");
        var nextRoot = GUILayout.Toggle(applyRootMotion, "根运动（会位移）");
        if (nextTp != showThirdPerson || nextFp != showFirstPerson || nextKit != showDefuseKit)
        {
            showThirdPerson = nextTp;
            showFirstPerson = nextFp;
            showDefuseKit = nextKit;
            autoToggleMeshes = false;
            ApplyMeshVisibility();
        }
        if (nextRoot != applyRootMotion)
        {
            applyRootMotion = nextRoot;
            ApplyRootMotion();
        }

        GUILayout.Label("1 idle步枪  2 走  3 跑  4 蹲  5 跳  6 idle手枪  7 FP idle  8 射击  9 换弹");
        GUILayout.Label("← → 切换    空格重播    走跑片段自带位移");

        scroll = GUILayout.BeginScrollView(scroll);
        for (var i = 0; i < filtered.Count; i++)
        {
            var idx = filtered[i];
            var entry = clips[idx];
            var label = entry.shortName;
            if (idx == selectedIndex)
                label = "> " + label;
            if (GUILayout.Button(label, idx == selectedIndex ? GUI.skin.box : GUI.skin.button))
            {
                selectedIndex = idx;
                PlaySelected();
            }
        }
        GUILayout.EndScrollView();
        GUILayout.EndArea();
    }

    void DrawCategoryButton(string category)
    {
        var on = categoryFilter == category;
        if (GUILayout.Toggle(on, category, GUI.skin.button) && !on)
        {
            categoryFilter = category;
            OnFilterChanged();
        }
    }

    void LoadClips()
    {
        clips.Clear();
        foreach (var path in ResourcePaths)
        {
            var pack = path.Substring(path.LastIndexOf('/') + 1);
            var assets = Resources.LoadAll(path, typeof(AnimationClip));
            foreach (var asset in assets)
            {
                var clip = asset as AnimationClip;
                if (clip == null || clip.name.StartsWith("__preview", StringComparison.Ordinal))
                    continue;

                var shortName = ShortName(clip.name);
                clips.Add(new ClipEntry
                {
                    shortName = shortName,
                    category = CategoryOf(shortName, pack),
                    pack = pack,
                    clip = clip,
                });
            }
        }

        clips.Sort((a, b) =>
        {
            var c = string.CompareOrdinal(a.category, b.category);
            return c != 0 ? c : string.CompareOrdinal(a.shortName, b.shortName);
        });
    }

    static string ShortName(string clipName)
    {
        var bar = clipName.LastIndexOf('|');
        return bar >= 0 && bar < clipName.Length - 1 ? clipName.Substring(bar + 1) : clipName;
    }

    static string CategoryOf(string shortName, string pack)
    {
        if (shortName.StartsWith("fp_", StringComparison.Ordinal))
            return "第一人称";
        if (pack.Contains("rifle_move"))
            return "步枪移动";
        if (pack.Contains("pistol_move"))
            return "手枪移动";
        if (pack.Contains("knife_move"))
            return "刀具移动";
        if (pack.Contains("knife_butterfly") || shortName.IndexOf("knife", StringComparison.OrdinalIgnoreCase) >= 0
            || shortName.IndexOf("butterfly", StringComparison.OrdinalIgnoreCase) >= 0)
            return "第三人称持刀";
        if (shortName.StartsWith("tp_", StringComparison.Ordinal))
            return "第三人称持枪";
        return "其它";
    }

    void OnFilterChanged()
    {
        filtered.Clear();
        var q = (search ?? "").Trim();
        if (string.IsNullOrEmpty(q))
            q = (guiSearch ?? "").Trim();

        for (var i = 0; i < clips.Count; i++)
        {
            var e = clips[i];
            if (categoryFilter != "全部" && e.category != categoryFilter)
                continue;
            if (q.Length > 0 && e.shortName.IndexOf(q, StringComparison.OrdinalIgnoreCase) < 0
                && e.category.IndexOf(q, StringComparison.OrdinalIgnoreCase) < 0)
                continue;
            filtered.Add(i);
        }

        if (filtered.Count == 0)
            return;
        if (!filtered.Contains(selectedIndex))
            selectedIndex = filtered[0];
    }

    IEnumerable<string> CategoryDropdown()
    {
        yield return "全部";
        yield return "步枪移动";
        yield return "手枪移动";
        yield return "刀具移动";
        yield return "第一人称";
        yield return "第三人称持枪";
        yield return "第三人称持刀";
    }

    IEnumerable<ValueDropdownItem<int>> FilteredClipDropdown()
    {
        if (filtered.Count == 0)
            OnFilterChanged();
        foreach (var i in filtered)
            yield return new ValueDropdownItem<int>(clips[i].category + " / " + clips[i].shortName, i);
    }

    void Step(int delta)
    {
        if (filtered.Count == 0)
        {
            OnFilterChanged();
            if (filtered.Count == 0)
                return;
        }

        var pos = filtered.IndexOf(selectedIndex);
        if (pos < 0)
            pos = 0;
        pos = (pos + delta + filtered.Count) % filtered.Count;
        selectedIndex = filtered[pos];
        PlaySelected();
    }

    public void JumpToShortName(string shortName)
    {
        for (var i = 0; i < clips.Count; i++)
        {
            if (clips[i].shortName != shortName)
                continue;
            selectedIndex = i;
            categoryFilter = "全部";
            search = "";
            guiSearch = "";
            OnFilterChanged();
            PlaySelected();
            return;
        }
    }

    void PlaySelected()
    {
        if (selectedIndex < 0 || selectedIndex >= clips.Count)
            return;

        var entry = clips[selectedIndex];
        if (entry.clip == null || animator == null)
            return;

        if (autoToggleMeshes)
        {
            var isFp = entry.shortName.StartsWith("fp_", StringComparison.Ordinal);
            showFirstPerson = isFp;
            showThirdPerson = !isFp;
            ApplyMeshVisibility();
        }

        DestroyGraph();
        if (animator.runtimeAnimatorController != null)
            animator.runtimeAnimatorController = null;

        var playClip = AdaptClipForFbxScale(entry.clip);
        graph = PlayableGraph.Create("FbiAnimPreview");
        graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
        var output = AnimationPlayableOutput.Create(graph, "Anim", animator);
        clipPlayable = AnimationClipPlayable.Create(graph, playClip);
        clipPlayable.SetSpeed(speed);
        output.SetSourcePlayable(clipPlayable);
        graph.Play();
        graph.Evaluate();
        RestoreArmatureRest();
    }

    // FBX 骨架根是 100 倍缩放，Blender 动画位移是米。不缩放就会把人拉成蜘蛛网。
    AnimationClip AdaptClipForFbxScale(AnimationClip src)
    {
        if (src == null)
            return null;
        if (adaptedClips.TryGetValue(src, out var cached) && cached != null)
            return cached;

#if UNITY_EDITOR
        const float posScale = 0.01f;
        var dst = new AnimationClip
        {
            name = src.name,
            frameRate = src.frameRate,
            wrapMode = src.wrapMode,
            legacy = false,
        };
        var settings = UnityEditor.AnimationUtility.GetAnimationClipSettings(src);
        UnityEditor.AnimationUtility.SetAnimationClipSettings(dst, settings);

        foreach (var binding in UnityEditor.AnimationUtility.GetCurveBindings(src))
        {
            if (binding.path.IndexOf('/') < 0)
                continue;
            var curve = UnityEditor.AnimationUtility.GetEditorCurve(src, binding);
            if (curve == null)
                continue;
            if (binding.propertyName.StartsWith("m_LocalPosition"))
            {
                for (var i = 0; i < curve.length; i++)
                {
                    var key = curve[i];
                    key.value *= posScale;
                    key.inTangent *= posScale;
                    key.outTangent *= posScale;
                    curve.MoveKey(i, key);
                }
            }
            UnityEditor.AnimationUtility.SetEditorCurve(dst, binding, curve);
        }

        dst.EnsureQuaternionContinuity();
        adaptedClips[src] = dst;
        return dst;
#else
        adaptedClips[src] = src;
        return src;
#endif
    }

    void RestoreArmatureRest()
    {
        if (armature == null)
            return;
        armature.localScale = armatureRestScale;
        armature.localRotation = armatureRestRotation;
    }

    void ApplySpeed()
    {
        if (clipPlayable.IsValid())
            clipPlayable.SetSpeed(speed);
    }

    void ApplyRootMotion()
    {
        if (animator != null)
            animator.applyRootMotion = applyRootMotion;
    }

    void ApplyMeshVisibility()
    {
        foreach (var renderer in GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            var n = renderer.name;
            var fp = n.IndexOf("firstperson", StringComparison.OrdinalIgnoreCase) >= 0;
            var kit = n.IndexOf("defuse", StringComparison.OrdinalIgnoreCase) >= 0;
            var tp = !fp && !kit;
            renderer.gameObject.SetActive((fp && showFirstPerson) || (tp && showThirdPerson) || (kit && showDefuseKit));
        }
    }

    void DestroyGraph()
    {
        if (graph.IsValid())
            graph.Destroy();
    }
}
