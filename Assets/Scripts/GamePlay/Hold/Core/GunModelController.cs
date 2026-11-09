using System.Collections.Generic;
using UnityEngine;

namespace GamePlay.HoldSystem
{
    /// <summary>使用绑定条目的通用资源播放枪械模型动画，不依赖具体枪种。</summary>
    public sealed class GunModelController : WeaponController
    {
        [SerializeField] private Animator _animator;

        private readonly HashSet<(RuntimeAnimatorController controller, int state)> _missingStates = new();
        private Gun _gun;
        private RuntimeAnimatorController _shotController;
        private bool _returnToHold;
        private int _shotStateHash;
        private int _lastShotFrame;

        public Gun Gun => Weapon as Gun;
        public Animator ModelAnimator => _animator;

        public override bool CanBind(WeaponBase weapon) => weapon is Gun;

        protected override void OnBound()
        {
            _gun = (Gun)Weapon;
            if (_animator == null)
                _animator = GetComponentInChildren<Animator>(true);
            _gun.OnPrepare += ApplyPrepare;
            _gun.OnPrepareCompleted += ApplyHold;
            _gun.OnShot += ApplyShot;
            _gun.OnReload += ApplyReload;
            _gun.OnReloadCompleted += ApplyHold;
            _gun.OnActionCanceled += ApplyActionCanceled;

            // Bind 发生在 Holder 提交 CurrentEntry 之前，资源只能取自绑定的 Entry。
            ApplyHold();
        }

        protected override void OnUnbinding()
        {
            if (_gun != null)
            {
                _gun.OnPrepare -= ApplyPrepare;
                _gun.OnPrepareCompleted -= ApplyHold;
                _gun.OnShot -= ApplyShot;
                _gun.OnReload -= ApplyReload;
                _gun.OnReloadCompleted -= ApplyHold;
                _gun.OnActionCanceled -= ApplyActionCanceled;
            }
            CancelShotAnimation();
            _gun = null;
        }

        private void OnEnable()
        {
            if (_gun != null)
                ApplyHold();
        }

        private void OnDisable() => CancelShotAnimation();

        // 在 Animator 求值后观察新动作，避免下一帧 Update 读到上一帧的持握状态。
        private void LateUpdate()
        {
            if (!_returnToHold)
                return;
            if (!CanAnimate || _animator == null || _animator.runtimeAnimatorController != _shotController)
            {
                CancelShotAnimation();
                return;
            }
            if (Time.frameCount == _lastShotFrame)
                return;

            AnimatorStateInfo state = _animator.GetCurrentAnimatorStateInfo(0);
            if (state.shortNameHash != _shotStateHash && state.fullPathHash != _shotStateHash)
            {
                CancelShotAnimation();
                return;
            }
            if (state.normalizedTime < 1f || _animator.IsInTransition(0))
                return;

            CancelShotAnimation();
            TryPlay(WeaponPresentationAction.Hold, 1f, out _);
        }

        private bool CanAnimate => Application.isPlaying && isActiveAndEnabled &&
            _gun != null && ReferenceEquals(Weapon, _gun);

        private void ApplyPrepare() => ApplyAction(WeaponPresentationAction.Prepare);

        private void ApplyReload() => ApplyAction(WeaponPresentationAction.Reload);

        private void ApplyAction(WeaponPresentationAction action)
        {
            CancelShotAnimation();
            TryPlay(action, 0f, out _);
        }

        private void ApplyShot()
        {
            CancelShotAnimation();
            if (!TryPlay(WeaponPresentationAction.Fire, 0f, out int stateHash))
                return;

            _shotController = _animator.runtimeAnimatorController;
            _shotStateHash = stateHash;
            _returnToHold = true;
            _lastShotFrame = Time.frameCount;
        }

        private void ApplyHold()
        {
            CancelShotAnimation();
            TryPlay(WeaponPresentationAction.Hold, 1f, out _);
        }

        private void ApplyActionCanceled(GunState state) => ApplyHold();

        private bool TryPlay(WeaponPresentationAction action, float normalizedTime, out int stateHash)
        {
            stateHash = 0;
            if (!CanAnimate)
                return false;
            IWeaponPresentationResources resources = PresentationResources;
            if (resources == null || !resources.TryGetAnimation(action, WeaponAnimationTarget.Model, out string stateName) ||
                string.IsNullOrWhiteSpace(stateName))
                return false;

            stateHash = Animator.StringToHash(stateName);
            RuntimeAnimatorController controller = _animator != null ? _animator.runtimeAnimatorController : null;
            if (_animator == null || controller == null || _animator.layerCount == 0 || !_animator.HasState(0, stateHash))
            {
                if (_missingStates.Add((controller, stateHash)))
                    Debug.LogWarning($"[{nameof(GunModelController)}] 枪械模型 Animator 没有配置状态 {stateName}。", this);
                return false;
            }

            _animator.Play(stateHash, 0, normalizedTime);
            return true;
        }

        private void CancelShotAnimation()
        {
            _returnToHold = false;
            _shotController = null;
            _shotStateHash = 0;
            _lastShotFrame = -1;
        }
    }
}
