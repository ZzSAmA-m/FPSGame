using System;
using System.Collections.Generic;
using UnityEngine;

namespace GamePlay.Player
{
    /// <summary>
    /// 把 AnimationClip 拖到 Inspector 的「动画」槽，点编辑器 Play 即可。
    /// 会自动换成已烘焙的 clip，避免细长骨架；真正驱动的是子物体 ctm_fbi 上的 Animator。
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(10000)]
    public class FbiAnimator : MonoBehaviour
    {
        public const string StateName = "Clip";
        public const string OverrideKey = "FbiClip";
        public const string AdaptedResourceFolder = "CS2/Agents/ctm_fbi/Animations/Adapted";

        [SerializeField] Animator animator;
        [SerializeField] RuntimeAnimatorController controller;
        [Tooltip("从 Project 把 AnimationClip 拖到这里，再点编辑器顶部 Play。")]
        [SerializeField] AnimationClip clip;
        [SerializeField] bool applyRootMotion;
        [SerializeField] string defaultClip = "tp_breathing";

        AnimatorOverrideController overrideController;
        Transform armature;
        Vector3 armatureRestScale = new Vector3(100f, 100f, 100f);
        Quaternion armatureRestRotation = Quaternion.identity;
        TwistChain[] twistChains;

        const float TwistWeight = 0.5f;
        const float Twist1Weight = 1f;

        struct TwistChain
        {
            public Transform lower;
            public Transform hand;
            public Transform upperTwist;
            public Transform upperTwist1;
            public Transform lowerTwist;
            public Transform lowerTwist1;
            public Quaternion lowerRest;
            public Quaternion handRest;
            public Quaternion upperTwistRest;
            public Quaternion upperTwist1Rest;
            public Quaternion lowerTwistRest;
            public Quaternion lowerTwist1Rest;
        }

        public Animator Animator => animator;

        void Awake()
        {
            Bind();
        }

        void Start()
        {
            PlayAssigned();
        }

        void OnEnable()
        {
            if (twistChains == null)
                CaptureTwistRests();
        }

        void LateUpdate()
        {
            RestoreArmatureRest();
            if (twistChains == null)
                CaptureTwistRests();
            ApplyArmTwists();
        }

        public bool PlayAssigned()
        {
            var dropped = clip != null ? clip : FindClipDroppedOnPlayer();
            if (dropped != null)
                return Play(dropped);
            if (!string.IsNullOrEmpty(defaultClip))
                return Play(defaultClip);
            return false;
        }

        public bool Play(string shortName)
        {
            if (string.IsNullOrEmpty(shortName))
                return false;

            var adapted = Resources.Load<AnimationClip>(AdaptedResourceFolder + "/" + shortName);
            if (adapted == null)
            {
                Debug.LogWarning($"FbiAnimator: 找不到烘焙片段 '{shortName}'。请拖 Adapted 目录里的 clip，或先执行 Tools/FBI/Bake Adapted Animation Clips。", this);
                return false;
            }

            return PlayClipRaw(adapted);
        }

        public bool Play(AnimationClip source)
        {
            var adapted = ResolveAdapted(source);
            if (adapted == null)
            {
                Debug.LogWarning("FbiAnimator: 这个 clip 没有对应的烘焙文件，直接播会变成细长骨架。请用 Animations/Adapted 里的同名片段。", this);
                return false;
            }

            return PlayClipRaw(adapted);
        }

        bool PlayClipRaw(AnimationClip adapted)
        {
            if (adapted == null || animator == null || overrideController == null)
                return false;

            ApplyOverride(adapted);
            animator.enabled = true;
            animator.Play(StateName, 0, 0f);
            animator.Update(0f);
            RestoreArmatureRest();
            ApplyArmTwists();
            return true;
        }

        void ApplyOverride(AnimationClip adapted)
        {
            var pairs = new List<KeyValuePair<AnimationClip, AnimationClip>>(overrideController.overridesCount);
            overrideController.GetOverrides(pairs);
            if (pairs.Count > 0)
            {
                overrideController[pairs[0].Key] = adapted;
                return;
            }

            overrideController[OverrideKey] = adapted;
        }

        static AnimationClip ResolveAdapted(AnimationClip source)
        {
            if (source == null)
                return null;

            var shortName = ShortName(source.name);
            if (string.IsNullOrEmpty(shortName))
                return null;

            var adapted = Resources.Load<AnimationClip>(AdaptedResourceFolder + "/" + shortName);
            if (adapted != null)
                return adapted;

            // 已经是烘焙资源（名字就是 shortName）时可以直接用。
            if (source.name == shortName)
                return source;

            return null;
        }

        static string ShortName(string clipName)
        {
            var bar = clipName.LastIndexOf('|');
            return bar >= 0 && bar < clipName.Length - 1 ? clipName.Substring(bar + 1) : clipName;
        }

        AnimationClip FindClipDroppedOnPlayer()
        {
            foreach (var extra in GetComponentsInChildren<Animator>(true))
            {
                if (extra == animator || extra.runtimeAnimatorController == null)
                    continue;
                var clips = extra.runtimeAnimatorController.animationClips;
                if (clips == null || clips.Length == 0)
                    continue;
                for (var i = clips.Length - 1; i >= 0; i--)
                {
                    if (clips[i] != null && clips[i].name != OverrideKey)
                        return clips[i];
                }
            }

            var legacy = GetComponent<Animation>();
            if (legacy != null && legacy.clip != null)
                return legacy.clip;

            return null;
        }

        void Bind()
        {
            ResolveAnimator();
            EnsureArmaturePath();
            CaptureArmatureRest();
            CaptureTwistRests();

            if (animator == null)
                return;

            animator.applyRootMotion = applyRootMotion;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            var baseController = controller != null ? controller : animator.runtimeAnimatorController;
            if (baseController == null)
            {
                Debug.LogWarning("FbiAnimator: 没有 Animator Controller。", this);
                return;
            }

            overrideController = new AnimatorOverrideController(baseController)
            {
                name = "FbiAnimatorOverride",
            };
            animator.runtimeAnimatorController = overrideController;
        }

        void ResolveAnimator()
        {
            Animator best = animator;
            if (best == null || best.avatar == null || !best.avatar.isValid)
            {
                best = null;
                foreach (var candidate in GetComponentsInChildren<Animator>(true))
                {
                    if (candidate.avatar == null || !candidate.avatar.isValid)
                        continue;
                    best = candidate;
                    break;
                }

                if (best == null)
                    best = GetComponentInChildren<Animator>(true);
            }

            animator = best;
            DisableExtraAnimators();
        }

        void DisableExtraAnimators()
        {
            foreach (var extra in GetComponentsInChildren<Animator>(true))
            {
                extra.enabled = extra == animator;
            }
        }

        void EnsureArmaturePath()
        {
            const string forward = "agents/models/ctm_fbi/ctm_fbi.vmdl_c";
            const string backslash = "agents\\models\\ctm_fbi\\ctm_fbi.vmdl_c";
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
                if (n != backslash && n != forward && n.Replace('\\', '/') != forward)
                    continue;
                if (t.GetComponent<Renderer>() != null)
                    continue;
                if (n != backslash)
                    t.name = backslash;
                return t;
            }

            return null;
        }

        void CaptureArmatureRest()
        {
            if (armature == null)
                return;
            armatureRestScale = armature.localScale;
            armatureRestRotation = armature.localRotation;
            if (armatureRestScale.x < 10f)
                armatureRestScale = new Vector3(100f, 100f, 100f);
        }

        void RestoreArmatureRest()
        {
            if (armature == null)
                return;
            armature.localScale = armatureRestScale;
            armature.localRotation = armatureRestRotation;
        }

        void CaptureTwistRests()
        {
            var root = animator != null ? animator.transform : transform;
            twistChains = new[]
            {
                MakeTwistChain(root, "R"),
                MakeTwistChain(root, "L"),
            };
        }

        static TwistChain MakeTwistChain(Transform root, string side)
        {
            var lower = FindNamed(root, "arm_lower_" + side);
            var hand = FindNamed(root, "hand_" + side);
            var upperTwist = FindNamed(root, "arm_upper_" + side + "_TWIST");
            var upperTwist1 = FindNamed(root, "arm_upper_" + side + "_TWIST1");
            var lowerTwist = FindNamed(root, "arm_lower_" + side + "_TWIST");
            var lowerTwist1 = FindNamed(root, "arm_lower_" + side + "_TWIST1");
            return new TwistChain
            {
                lower = lower,
                hand = hand,
                upperTwist = upperTwist,
                upperTwist1 = upperTwist1,
                lowerTwist = lowerTwist,
                lowerTwist1 = lowerTwist1,
                lowerRest = BindLocalRotation(root, lower),
                handRest = BindLocalRotation(root, hand),
                upperTwistRest = BindLocalRotation(root, upperTwist),
                upperTwist1Rest = BindLocalRotation(root, upperTwist1),
                lowerTwistRest = BindLocalRotation(root, lowerTwist),
                lowerTwist1Rest = BindLocalRotation(root, lowerTwist1),
            };
        }

        static Quaternion BindLocalRotation(Transform root, Transform bone)
        {
            if (bone == null)
                return Quaternion.identity;
            if (bone.parent == null)
                return bone.localRotation;

            foreach (var skin in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (skin.sharedMesh == null || skin.bones == null)
                    continue;

                var bones = skin.bones;
                var bind = skin.sharedMesh.bindposes;
                if (bind == null)
                    continue;

                var i = -1;
                var p = -1;
                for (var b = 0; b < bones.Length; b++)
                {
                    if (bones[b] == bone)
                        i = b;
                    if (bones[b] == bone.parent)
                        p = b;
                    if (i >= 0 && p >= 0)
                        break;
                }

                if (i < 0 || p < 0 || i >= bind.Length || p >= bind.Length)
                    continue;

                return (bind[p] * bind[i].inverse).rotation;
            }

            return bone.localRotation;
        }

        static Transform FindNamed(Transform root, string name)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == name)
                    return t;
            }

            return null;
        }

        bool ThirdPersonArmsVisible()
        {
            var root = animator != null ? animator.transform : transform;
            foreach (var skin in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (!skin.enabled || !skin.gameObject.activeInHierarchy)
                    continue;
                if (skin.name.IndexOf("thirdperson", StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }

            return false;
        }

        void ApplyArmTwists()
        {
            if (twistChains == null)
                return;

            var copyHandTwist = ThirdPersonArmsVisible();
            for (int i = 0; i < twistChains.Length; i++)
            {
                var chain = twistChains[i];
                if (chain.lower == null)
                    continue;

                var lowerTwist = Mathf.DeltaAngle(
                    TwistAngleX(chain.lowerRest),
                    TwistAngleX(chain.lower.localRotation));
                var lowerQ = Quaternion.AngleAxis(lowerTwist, Vector3.right);
                var cancelQ = Quaternion.AngleAxis(-lowerTwist, Vector3.right);

                if (chain.upperTwist != null)
                    chain.upperTwist.localRotation = chain.upperTwistRest * Quaternion.Slerp(Quaternion.identity, lowerQ, TwistWeight);
                if (chain.upperTwist1 != null)
                    chain.upperTwist1.localRotation = chain.upperTwist1Rest * Quaternion.Slerp(Quaternion.identity, lowerQ, Twist1Weight);

                var handQ = Quaternion.identity;
                if (copyHandTwist && chain.hand != null)
                {
                    var handTwist = Mathf.DeltaAngle(
                        TwistAngleX(chain.handRest),
                        TwistAngleX(chain.hand.localRotation));
                    handQ = Quaternion.AngleAxis(handTwist, Vector3.right);
                }

                if (chain.lowerTwist != null)
                    chain.lowerTwist.localRotation = cancelQ * chain.lowerTwistRest * Quaternion.Slerp(Quaternion.identity, handQ, TwistWeight);
                if (chain.lowerTwist1 != null)
                    chain.lowerTwist1.localRotation = cancelQ * chain.lowerTwist1Rest * Quaternion.Slerp(Quaternion.identity, handQ, Twist1Weight);
            }
        }

        static float TwistAngleX(Quaternion q)
        {
            if (q.w < 0f)
                q = new Quaternion(-q.x, -q.y, -q.z, -q.w);
            var mag = Mathf.Sqrt(q.x * q.x + q.w * q.w);
            if (mag < 1e-8f)
                return 0f;
            return 2f * Mathf.Atan2(q.x / mag, q.w / mag) * Mathf.Rad2Deg;
        }
    }
}
