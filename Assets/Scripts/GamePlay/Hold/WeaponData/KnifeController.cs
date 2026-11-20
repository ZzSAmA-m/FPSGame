using GamePlay.Player.Simulation;
using UnityEngine;

namespace GamePlay.HoldSystem
{
    /// <summary>为同一次持刀统一播放角色和刀模型的动作。</summary>
    public sealed class KnifeController : WeaponController
    {
        private const string ThirdPersonDraw = "tp_draw_butterfly";
        private const string FirstPersonDraw = "fp_draw_butterfly";
        private const string FirstPersonDraw2 = "fp_draw2_butterfly";
        private const string ModelThirdPersonDraw = "butterflyKnife_tp_draw_butterfly";
        private const string ModelFirstPersonDraw = "butterflyKnife_fp_draw_butterfly";
        private const string ModelClipPrefix = "butterflyKnife|";
        private static readonly int ActionSpeed = Animator.StringToHash("KnifeActionSpeed");

        private Animator _modelAnimator;
        private AnimationClip[] _modelClips;
        private RuntimeAnimatorController _actorController;
        private AnimationClip[] _actorClips;
        private bool _useSecondDraw;

        public Knife Knife => Weapon as Knife;

        public override bool CanBind(WeaponBase weapon) => weapon is Knife;

        protected override void OnBound()
        {
            _modelAnimator = GetComponentInChildren<Animator>(true);
            _modelClips = _modelAnimator?.runtimeAnimatorController?.animationClips;
            // 装备时模型只绑定一次，视角变化和动作刷新沿用这个选择。
            _useSecondDraw = Application.isPlaying && Random.Range(0, 2) == 1;
        }

        internal void Present(Animator actor, int layer, bool firstPerson, KnifeState state, float progress)
        {
            if (!Application.isPlaying || state == KnifeState.None || actor == null)
                return;
            if (_actorController != actor.runtimeAnimatorController)
            {
                _actorController = actor.runtimeAnimatorController;
                _actorClips = _actorController?.animationClips;
            }

            (string actorState, string modelState) = (state, firstPerson) switch
            {
                (KnifeState.Preparing, true) => _useSecondDraw
                    ? (FirstPersonDraw2, ModelClipPrefix + FirstPersonDraw2)
                    : (FirstPersonDraw, ModelFirstPersonDraw),
                (KnifeState.LightAttack, true) => ("fp_light_miss1_but", ModelClipPrefix + "fp_light_miss1_butterfly"),
                (KnifeState.HeavyAttack, true) => ("fp_heavy_miss1_but", ModelClipPrefix + "fp_heavy_miss1_butterfly"),
                (KnifeState.Ready, true) => ("fp_idle1_butterfly", ModelClipPrefix + "fp_idle1_butterfly"),
                (KnifeState.LightAttack, false) => ("tp_frontswing_knife", ModelThirdPersonDraw),
                (KnifeState.HeavyAttack, false) => ("tp_frontstab_knife", ModelThirdPersonDraw),
                _ => (ThirdPersonDraw, ModelThirdPersonDraw)
            };

            float actorTime = progress;
            if (state == KnifeState.Ready)
                actorTime = firstPerson ? 0f : 1f;
            // TP 挥刀由角色带动，刀刃保持完全展开的姿势。
            float modelTime = !firstPerson && state != KnifeState.Preparing ? 1f : actorTime;
            uint ticks = Knife.Config.GetDuration(state);
            Play(actor, layer, _actorClips, actorState, actorTime, ticks);
            Play(_modelAnimator, 0, _modelClips, modelState, modelTime, ticks);
        }

        private static void Play(Animator animator, int layer, AnimationClip[] clips, string state, float progress, uint ticks)
        {
            if (animator == null || clips == null || layer >= animator.layerCount)
                return;
            foreach (AnimationClip clip in clips)
            {
                if (clip.name != state) continue;
                // 只缩放刀动作状态，TP 下半身的移动层继续按原速播放。
                animator.SetFloat(ActionSpeed, ticks == 0 ? 1f : clip.length / (ticks * SimTime.StepSeconds));
                animator.Play(Animator.StringToHash(state), layer, Mathf.Clamp01(progress));
                return;
            }
            Debug.LogWarning($"[{nameof(KnifeController)}] {animator.name} 缺少刀动作 {state}。", animator);
        }
    }
}
