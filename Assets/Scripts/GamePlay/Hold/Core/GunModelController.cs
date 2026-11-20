using System.Collections.Generic;
using GamePlay.Player;
using GamePlay.Player.Simulation;
using UnityEngine;

namespace GamePlay.HoldSystem
{
    /// <summary>使用绑定条目的通用资源播放枪械模型动画，不依赖具体枪种。</summary>
    public sealed class GunModelController : WeaponController
    {
        [SerializeField] private Animator _animator;

        private readonly HashSet<(RuntimeAnimatorController controller, int state)> _missingStates = new();
        private Gun _gun;
        private RuntimeAnimatorController _actionController;
        private WeaponPresentationAction _playingAction;
        private bool _returnToHold;
        private int _actionStateHash;
        private int _lastActionFrame;
        private double _actionStartTime;
        private bool _reloadPaused;
        private float _pausedReloadSeconds;

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
                _gun.OnActionCanceled -= ApplyActionCanceled;
            }
            CancelActionAnimation();
            _gun = null;
        }

        private void OnEnable()
        {
            if (_gun == null)
                return;
            PlayerWeaponController controller = Holder != null ? Holder.GetComponent<PlayerWeaponController>() : null;
            PlayerAnimatorController actor = Holder != null ? Holder.GetComponent<PlayerAnimatorController>() : null;
            GunState state = controller != null ? controller.PresentationState : _gun.GunState;
            if (state != GunState.None && state != GunState.Preparing && state != GunState.Shooting &&
                actor != null && actor.TryGetReloadAnimationTime(out float seconds))
            {
                PlayTrackedAction(WeaponPresentationAction.Reload, seconds);
                return;
            }

            if (state != GunState.None && state != GunState.Preparing && state != GunState.Shooting &&
                (actor == null || !actor.isActiveAndEnabled) &&
                _reloadPaused && _playingAction == WeaponPresentationAction.Reload)
            {
                PlayTrackedAction(WeaponPresentationAction.Reload, _pausedReloadSeconds);
                return;
            }

            float progress = controller != null ? controller.ActionProgress : _gun.ActionProgress;
            if (state == GunState.Reloading)
                PlayTrackedAction(WeaponPresentationAction.Reload, progress * _gun.Config.ReloadTicks * SimTime.StepSeconds);
            else if (state == GunState.Preparing)
            {
                CancelActionAnimation();
                if (!TryPlay(WeaponPresentationAction.Prepare, progress, out _))
                    TryPlay(WeaponPresentationAction.Hold, 1f, out _);
            }
            else
                ApplyHold();
        }

        private void OnDisable()
        {
            if (TryGetReloadAnimationTime(out float seconds))
            {
                _reloadPaused = true;
                _pausedReloadSeconds = seconds;
            }
            else
                CancelActionAnimation();
        }

        private bool TryGetReloadAnimationTime(out float seconds)
        {
            seconds = 0f;
            if (!_returnToHold || _playingAction != WeaponPresentationAction.Reload || _gun == null ||
                _animator == null || _animator.runtimeAnimatorController != _actionController)
                return false;
            if (_reloadPaused)
            {
                seconds = _pausedReloadSeconds;
                return true;
            }
            if (Time.frameCount == _lastActionFrame)
            {
                seconds = Mathf.Max(0f, (float)(Time.timeAsDouble - _actionStartTime));
                return true;
            }

            AnimatorStateInfo state = _animator.GetCurrentAnimatorStateInfo(0);
            if ((state.shortNameHash != _actionStateHash && state.fullPathHash != _actionStateHash) ||
                (state.normalizedTime >= 1f && !_animator.IsInTransition(0)))
                return false;
            seconds = Mathf.Max(0f, state.normalizedTime * state.length);
            return true;
        }

        // 在 Animator 求值后观察新动作，避免下一帧 Update 读到上一帧的持握状态。
        private void LateUpdate()
        {
            if (!_returnToHold || _reloadPaused)
                return;
            if (!CanAnimate || _animator == null || _animator.runtimeAnimatorController != _actionController)
            {
                CancelActionAnimation();
                return;
            }
            if (Time.frameCount == _lastActionFrame)
                return;

            AnimatorStateInfo state = _animator.GetCurrentAnimatorStateInfo(0);
            if (state.shortNameHash != _actionStateHash && state.fullPathHash != _actionStateHash)
            {
                CancelActionAnimation();
                return;
            }
            if (state.normalizedTime < 1f || _animator.IsInTransition(0))
                return;

            CancelActionAnimation();
            TryPlay(WeaponPresentationAction.Hold, 1f, out _);
        }

        private bool CanAnimate => Application.isPlaying && isActiveAndEnabled &&
            _gun != null && ReferenceEquals(Weapon, _gun);

        private void ApplyPrepare() => ApplyAction(WeaponPresentationAction.Prepare);

        private void ApplyReload() => PlayTrackedAction(WeaponPresentationAction.Reload);

        private void ApplyAction(WeaponPresentationAction action)
        {
            CancelActionAnimation();
            if (!TryPlay(action, 0f, out _) && action == WeaponPresentationAction.Prepare)
                TryPlay(WeaponPresentationAction.Hold, 1f, out _);
        }

        private void ApplyShot() => PlayTrackedAction(WeaponPresentationAction.Fire);

        private void PlayTrackedAction(WeaponPresentationAction action, float seconds = 0f)
        {
            CancelActionAnimation();
            if (!TryPlay(action, seconds, out int stateHash, true))
                return;

            _actionController = _animator.runtimeAnimatorController;
            _playingAction = action;
            _actionStateHash = stateHash;
            _returnToHold = true;
            _lastActionFrame = Time.frameCount;
            _actionStartTime = Time.timeAsDouble - seconds;
        }

        /// <summary>播放服务器确认的远端开火表现。</summary>
        public void PresentFire() => ApplyShot();

        /// <summary>播放远端切枪准备表现。</summary>
        public void PresentPrepare() => ApplyPrepare();

        /// <summary>播放服务器确认的远端换弹表现。</summary>
        public void PresentReload() => ApplyReload();

        /// <summary>将远端枪械模型恢复到持握表现。</summary>
        public void PresentHold() => ApplyHold();

        private void ApplyHold()
        {
            CancelActionAnimation();
            TryPlay(WeaponPresentationAction.Hold, 1f, out _);
        }

        private void ApplyActionCanceled(GunState state) => ApplyHold();

        private bool TryPlay(WeaponPresentationAction action, float time, out int stateHash, bool fixedTime = false)
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

            if (fixedTime)
                _animator.PlayInFixedTime(stateHash, 0, time);
            else
                _animator.Play(stateHash, 0, time);
            return true;
        }

        private void CancelActionAnimation()
        {
            _returnToHold = false;
            _actionController = null;
            _actionStateHash = 0;
            _lastActionFrame = -1;
            _actionStartTime = 0;
            _reloadPaused = false;
            _pausedReloadSeconds = 0f;
        }
    }
}
