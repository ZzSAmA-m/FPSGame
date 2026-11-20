using System.Collections.Generic;
using Core.Utility;
using Core.ZLog;
using GamePlay.HoldSystem;
using GamePlay.Player.MotionFSM;
using GamePlay.Player.Simulation;
using UnityEngine;

namespace GamePlay.Player
{
    /// <summary>订阅动作语义事件，通过当前装备的资源接口播放角色动画。</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(PlayerMotionController))]
    public sealed class PlayerAnimatorController : MonoBehaviour
    {
        [SerializeField] private Animator _mainAnimator;
        public float switchAnimTime = 0.2f;

        public const int DownLayer = 0;
        public const int UpLayer = 1;
        public const int FPLayer = 2;

        private readonly HashSet<(RuntimeAnimatorController controller, int state)> _missingStates = new();
        private PlayerMotionStateMachine _motion;
        private PlayerWeaponController _weapon;
        private PlayerHolder _holder;
        private Gun _playingGun;
        private WeaponPresentationAction _playingAction;
        private RuntimeAnimatorController _actionController;
        private bool _returnToHold;
        private int _actionStateHash;
        private int _actionLayer;
        private int _lastActionFrame;
        private double _actionStartTime;
        private bool _reloadPaused;
        private float _pausedReloadSeconds;
        private bool _serverDriven;

        /// <summary>换弹动画仍在播放，包括逻辑已经装填完成后的收尾。</summary>
        public bool IsReloadAnimationPlaying => isActiveAndEnabled && TryGetReloadAnimationTime(out _);

        /// <summary>读取换弹动画已经播放的秒数，供切换视角和枪模型恢复同一动作。</summary>
        public bool TryGetReloadAnimationTime(out float seconds)
        {
            seconds = 0f;
            if (!_returnToHold || _playingAction != WeaponPresentationAction.Reload || _playingGun == null ||
                _mainAnimator == null || _mainAnimator.runtimeAnimatorController != _actionController)
                return false;
            PlayerHolder holder = _holder != null ? _holder : GetComponent<PlayerHolder>();
            if (holder == null || !ReferenceEquals(holder.CurrentWeapon, _playingGun))
                return false;
            if (_reloadPaused)
            {
                seconds = _pausedReloadSeconds;
                return true;
            }
            if (!_serverDriven && Time.frameCount == _lastActionFrame)
            {
                seconds = Mathf.Max(0f, (float)(Time.timeAsDouble - _actionStartTime));
                return true;
            }

            AnimatorStateInfo state = _mainAnimator.GetCurrentAnimatorStateInfo(_actionLayer);
            if ((state.shortNameHash != _actionStateHash && state.fullPathHash != _actionStateHash) ||
                (state.normalizedTime >= 1f && !_mainAnimator.IsInTransition(_actionLayer)))
                return false;
            seconds = Mathf.Max(0f, state.normalizedTime * state.length);
            return true;
        }

        private void OnEnable()
        {
            if (_mainAnimator == null)
                _mainAnimator = GetComponentInChildren<Animator>(true);
            if (_mainAnimator == null)
            {
                Debug.LogError($"[{nameof(PlayerAnimatorController)}] 未配置角色 Animator。", this);
                return;
            }

            _holder = GetComponent<PlayerHolder>();
            _weapon = GetComponent<PlayerWeaponController>();
            _motion = GetComponent<PlayerMotionController>().StateMachine;
            _motion.OnViewChanged += ApplyView;
            _motion.OnLocomotionChanged += ApplyLocomotion;
            _motion.OnHoldPoseChanged += ApplyHoldPose;
            if (_weapon != null)
            {
                _weapon.OnPrepare += ApplyPrepare;
                _weapon.OnPrepareEnd += ApplyActionEnded;
                _weapon.OnShot += ApplyShot;
                _weapon.OnReload += ApplyReload;
                _weapon.OnActionCanceled += ApplyActionCanceled;
                _weapon.OnKnifeStateChanged += ApplyKnifeState;
            }
            if (_holder != null)
                _holder.OnWeaponChanged += RefreshHoldPose;
            if (_motion.IsStarted)
            {
                ApplyView(_motion.View);
                ApplyLocomotion(_motion.Locomotion);
            }
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
            if (_motion != null)
            {
                _motion.OnViewChanged -= ApplyView;
                _motion.OnLocomotionChanged -= ApplyLocomotion;
                _motion.OnHoldPoseChanged -= ApplyHoldPose;
            }
            if (_weapon != null)
            {
                _weapon.OnPrepare -= ApplyPrepare;
                _weapon.OnPrepareEnd -= ApplyActionEnded;
                _weapon.OnShot -= ApplyShot;
                _weapon.OnReload -= ApplyReload;
                _weapon.OnActionCanceled -= ApplyActionCanceled;
                _weapon.OnKnifeStateChanged -= ApplyKnifeState;
            }
            if (_holder != null)
                _holder.OnWeaponChanged -= RefreshHoldPose;
            _motion = null;
            _weapon = null;
            _holder = null;
        }

        // 实时枪械在网络 LateUpdate 中发出 Play；这里必须在 Animator 求值后检查完成状态。
        private void LateUpdate()
        {
            if (_serverDriven)
                return;
            CompleteTrackedAction(false);
        }

        /// <summary>服务器按固定 Tick 求值后检查动作完成，不受 Unity 帧编号限制。</summary>
        public bool CompleteServerPoseStep() => CompleteTrackedAction(true);

        public void SetServerDriven(bool serverDriven) => _serverDriven = serverDriven;

        private bool CompleteTrackedAction(bool evaluated)
        {
            if (!_returnToHold || _reloadPaused)
                return false;
            if (!IsCurrentGun(_playingGun) || _mainAnimator == null ||
                _mainAnimator.runtimeAnimatorController != _actionController ||
                _actionLayer != CurrentWeaponLayer)
            {
                CancelActionAnimation();
                return false;
            }

            // 等待 Animator 实际求值；逻辑动作完成后仍保留动画收尾。
            if (!evaluated && Time.frameCount == _lastActionFrame)
                return false;
            AnimatorStateInfo state = _mainAnimator.GetCurrentAnimatorStateInfo(_actionLayer);
            if (state.shortNameHash != _actionStateHash && state.fullPathHash != _actionStateHash)
            {
                CancelActionAnimation();
                return false;
            }
            if (state.normalizedTime < 1f || _mainAnimator.IsInTransition(_actionLayer))
                return false;

            Gun gun = _playingGun;
            CancelActionAnimation();
            return TryPlayGunAction(gun, WeaponPresentationAction.Hold, 1f, out _, out _);
        }

        private int CurrentWeaponLayer => _motion != null && _motion.View == EPlayerModelType.FP ? FPLayer : UpLayer;

        private bool IsCurrentGun(Gun gun) => isActiveAndEnabled && gun != null &&
            _holder != null && ReferenceEquals(_holder.CurrentWeapon, gun);

        private void ApplyPrepare(Gun gun) => ApplyAction(gun, WeaponPresentationAction.Prepare);

        private void ApplyReload(Gun gun) => PlayTrackedAction(gun, WeaponPresentationAction.Reload);

        private void ApplyAction(Gun gun, WeaponPresentationAction action)
        {
            if (!IsCurrentGun(gun))
                return;
            CancelActionAnimation();
            TryPlayGunAction(gun, action, 0f, out _, out _);
        }

        private void ApplyShot(Gun gun)
        {
            ZLog.Log<PlayerAnimatorController>("ApplyShot", LogChannel.GamePlay);
            PlayTrackedAction(gun, WeaponPresentationAction.Fire);
        }

        private void PlayTrackedAction(Gun gun, WeaponPresentationAction action, float seconds = 0f)
        {
            if (!IsCurrentGun(gun))
                return;
            CancelActionAnimation();
            if (!TryPlayGunAction(gun, action, seconds, out int stateHash, out int layer, true))
                return;

            _playingGun = gun;
            _playingAction = action;
            _actionController = _mainAnimator.runtimeAnimatorController;
            _actionStateHash = stateHash;
            _actionLayer = layer;
            _returnToHold = true;
            _lastActionFrame = Time.frameCount;
            _actionStartTime = Time.timeAsDouble - seconds;
        }

        private void ApplyActionEnded(Gun gun)
        {
            if (!IsCurrentGun(gun))
                return;
            CancelActionAnimation();
            TryPlayGunAction(gun, WeaponPresentationAction.Hold, 1f, out _, out _);
        }

        private void ApplyActionCanceled(Gun gun, GunState state) => ApplyActionEnded(gun);

        private bool TryPlayGunAction(Gun gun, WeaponPresentationAction action, float time,
            out int stateHash, out int layer, bool fixedTime = false)
        {
            stateHash = 0;
            layer = CurrentWeaponLayer;
            if (!IsCurrentGun(gun) || _motion == null)
                return false;
            IWeaponPresentationResources resources = _holder.CurrentPresentationResources;
            WeaponAnimationTarget target = _motion.View == EPlayerModelType.FP
                ? WeaponAnimationTarget.FirstPerson : WeaponAnimationTarget.ThirdPerson;
            if (resources == null || !resources.TryGetAnimation(action, target, out string stateName) ||
                string.IsNullOrWhiteSpace(stateName))
                return false;

            stateHash = Animator.StringToHash(stateName);
            return TryPlayState(stateHash, stateName, layer, time, fixedTime);
        }

        private bool TryPlayState(int stateHash, string stateName, int layer, float time, bool fixedTime = false)
        {
            if (!Application.isPlaying || _mainAnimator == null)
                return false;
            RuntimeAnimatorController controller = _mainAnimator.runtimeAnimatorController;
            if (controller == null || layer < 0 || layer >= _mainAnimator.layerCount ||
                !_mainAnimator.HasState(layer, stateHash))
            {
                if (_missingStates.Add((controller, stateHash)))
                    Debug.LogWarning($"[{nameof(PlayerAnimatorController)}] 角色 Animator 的第 {layer} 层没有配置状态 {stateName}。", this);
                return false;
            }

            if (fixedTime)
                _mainAnimator.PlayInFixedTime(stateHash, layer, time);
            else
                _mainAnimator.Play(stateHash, layer, time);
            return true;
        }

        private void CancelActionAnimation()
        {
            _returnToHold = false;
            _playingGun = null;
            _actionController = null;
            _actionStateHash = 0;
            _actionLayer = 0;
            _lastActionFrame = -1;
            _actionStartTime = 0;
            _reloadPaused = false;
            _pausedReloadSeconds = 0f;
        }

        private void ApplyView(EPlayerModelType view)
        {
            if (Application.isPlaying && _mainAnimator != null && _mainAnimator.runtimeAnimatorController != null)
            {
                if (view == EPlayerModelType.TP)
                {
                    if (_mainAnimator.layerCount > DownLayer)
                        _mainAnimator.SetLayerWeight(DownLayer, 1f);
                    if (_mainAnimator.layerCount > UpLayer)
                        _mainAnimator.SetLayerWeight(UpLayer, 1f);
                    if (_mainAnimator.layerCount > FPLayer)
                        _mainAnimator.SetLayerWeight(FPLayer, 0f);
                }
                else if (_mainAnimator.layerCount > FPLayer)
                {
                    _mainAnimator.SetLayerWeight(FPLayer, 1f);
                }
            }
            RefreshHoldPose();
        }

        private void ApplyLocomotion(EStandRunState direction)
        {
            if (!Application.isPlaying || _motion.View != EPlayerModelType.TP)
                return;
            int animation = direction switch
            {
                EStandRunState.N => PlayerAnimatorName.run_n,
                EStandRunState.S => PlayerAnimatorName.run_s,
                EStandRunState.E => PlayerAnimatorName.run_e,
                EStandRunState.W => PlayerAnimatorName.run_w,
                _ => PlayerAnimatorName.idle
            };
            _mainAnimator.CrossFade(animation, switchAnimTime, DownLayer);
        }

        private void ApplyHoldPose(PlayerHoldPose pose) => RefreshHoldPose();

        private void RefreshHoldPose()
        {
            if (_motion == null)
                return;
            if (_holder != null && _holder.CurrentWeapon is Gun gun)
            {
                GunState state = _weapon != null ? _weapon.PresentationState : gun.GunState;
                if (state != GunState.None && state != GunState.Preparing && state != GunState.Shooting &&
                    TryGetReloadAnimationTime(out float seconds))
                {
                    PlayTrackedAction(gun, WeaponPresentationAction.Reload, seconds);
                    return;
                }

                CancelActionAnimation();
                float progress = _weapon != null ? _weapon.ActionProgress : gun.ActionProgress;
                if (state == GunState.Reloading)
                    PlayTrackedAction(gun, WeaponPresentationAction.Reload,
                        progress * gun.Config.ReloadTicks * SimTime.StepSeconds);
                else if (state == GunState.Preparing)
                {
                    TryPlayGunAction(gun, WeaponPresentationAction.Prepare, progress, out _, out _);
                }
                else
                    TryPlayGunAction(gun, WeaponPresentationAction.Hold, 1f, out _, out _);
                return;
            }

            if (_holder != null && _holder.CurrentWeapon is Knife knife && _weapon != null)
                ApplyKnifeState(knife, _weapon.KnifePresentationState);
            else
                CancelActionAnimation();
        }

        private void ApplyKnifeState(Knife knife, KnifeState state)
        {
            if (_motion == null || _weapon == null || _holder == null ||
                !ReferenceEquals(_holder.CurrentWeapon, knife) || _holder.CurrentController is not KnifeController model)
                return;
            CancelActionAnimation();
            model.Present(_mainAnimator, CurrentWeaponLayer, _motion.View == EPlayerModelType.FP,
                state, _weapon.KnifeActionProgress);
        }
    }
}
