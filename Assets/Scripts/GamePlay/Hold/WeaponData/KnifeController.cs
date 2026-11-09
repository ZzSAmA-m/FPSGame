using Core.Utility;
using GamePlay.Player;
using GamePlay.Player.MotionFSM;
using UnityEngine;

namespace GamePlay.HoldSystem
{
    /// <summary>绑定刀具逻辑，并根据角色视角播放刀模型自身的拔刀动画。</summary>
    public sealed class KnifeController : WeaponController
    {
        private static readonly int ThirdPersonDraw = Animator.StringToHash("butterflyKnife_tp_draw_butterfly");
        private static readonly int FirstPersonDraw = Animator.StringToHash("butterflyKnife_fp_draw_butterfly");
        private Animator _animator;
        private PlayerMotionStateMachine _motion;

        public Knife Knife => Weapon as Knife;

        public override bool CanBind(WeaponBase weapon) => weapon is Knife;

        protected override void OnBound()
        {
            _animator = GetComponentInChildren<Animator>(true);
            PlayerMotionController motion = Holder.GetComponent<PlayerMotionController>();
            if (motion != null)
            {
                _motion = motion.StateMachine;
                _motion.OnViewChanged += ApplyView;
            }
            ApplyView(_motion != null ? _motion.View : EPlayerModelType.TP);
        }

        protected override void OnUnbinding()
        {
            if (_motion != null)
                _motion.OnViewChanged -= ApplyView;
            _motion = null;
            _animator = null;
        }

        private void ApplyView(EPlayerModelType view)
        {
            // 编辑模式的隔离装备验证不执行动画播放。
            if (!Application.isPlaying || _animator == null || _animator.runtimeAnimatorController == null)
                return;
            _animator.Play(view == EPlayerModelType.FP ? FirstPersonDraw : ThirdPersonDraw, 0, 0f);
        }
    }
}
