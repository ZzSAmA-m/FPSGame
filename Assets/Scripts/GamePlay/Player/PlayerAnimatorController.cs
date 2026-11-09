using System.Collections.Generic;
using Core.Utility;
using Core.ZLog;
using GamePlay.HoldSystem;
using GamePlay.Player.MotionFSM;
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
        private Gun _shootingGun;
        private RuntimeAnimatorController _shotController;
        private bool _returnToHold;
        private int _shotStateHash;
        private int _shotLayer;
        private int _lastShotFrame;

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
                _weapon.OnReloadEnd += ApplyActionEnded;
                _weapon.OnActionCanceled += ApplyActionCanceled;
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
            CancelShotAnimation();
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
                _weapon.OnReloadEnd -= ApplyActionEnded;
                _weapon.OnActionCanceled -= ApplyActionCanceled;
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
            if (!_returnToHold)
                return;
            if (!IsCurrentGun(_shootingGun) || _mainAnimator == null ||
                _mainAnimator.runtimeAnimatorController != _shotController ||
                _shotLayer != CurrentWeaponLayer)
            {
                CancelShotAnimation();
                return;
            }

            // 等待 Animator 实际求值；射击冷却结束不代表射击动画结束。
            if (Time.frameCount == _lastShotFrame)
                return;
            AnimatorStateInfo state = _mainAnimator.GetCurrentAnimatorStateInfo(_shotLayer);
            if (state.shortNameHash != _shotStateHash && state.fullPathHash != _shotStateHash)
            {
                CancelShotAnimation();
                return;
            }
            if (state.normalizedTime < 1f || _mainAnimator.IsInTransition(_shotLayer))
                return;

            Gun gun = _shootingGun;
            CancelShotAnimation();
            TryPlayGunAction(gun, WeaponPresentationAction.Hold, 1f, out _, out _);
        }

        private int CurrentWeaponLayer => _motion != null && _motion.View == EPlayerModelType.FP ? FPLayer : UpLayer;

        private bool IsCurrentGun(Gun gun) => isActiveAndEnabled && gun != null &&
            _holder != null && ReferenceEquals(_holder.CurrentWeapon, gun);

        private void ApplyPrepare(Gun gun) => ApplyAction(gun, WeaponPresentationAction.Prepare);

        private void ApplyReload(Gun gun) => ApplyAction(gun, WeaponPresentationAction.Reload);

        private void ApplyAction(Gun gun, WeaponPresentationAction action)
        {
            if (!IsCurrentGun(gun))
                return;
            CancelShotAnimation();
            TryPlayGunAction(gun, action, 0f, out _, out _);
        }

        private void ApplyShot(Gun gun)
        {
            ZLog.Log<PlayerAnimatorController>("ApplyShot", LogChannel.GamePlay);
            if (!IsCurrentGun(gun))
                return;
            CancelShotAnimation();
            if (!TryPlayGunAction(gun, WeaponPresentationAction.Fire, 0f, out int stateHash, out int layer))
                return;

            _shootingGun = gun;
            _shotController = _mainAnimator.runtimeAnimatorController;
            _shotStateHash = stateHash;
            _shotLayer = layer;
            _returnToHold = true;
            _lastShotFrame = Time.frameCount;
        }

        private void ApplyActionEnded(Gun gun)
        {
            if (!IsCurrentGun(gun))
                return;
            CancelShotAnimation();
            TryPlayGunAction(gun, WeaponPresentationAction.Hold, 1f, out _, out _);
        }

        private void ApplyActionCanceled(Gun gun, GunState state) => ApplyActionEnded(gun);

        private bool TryPlayGunAction(Gun gun, WeaponPresentationAction action, float normalizedTime,
            out int stateHash, out int layer)
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
            return TryPlayState(stateHash, stateName, layer, normalizedTime);
        }

        private bool TryPlayState(int stateHash, string stateName, int layer, float normalizedTime)
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

            _mainAnimator.Play(stateHash, layer, normalizedTime);
            return true;
        }

        private void CancelShotAnimation()
        {
            _returnToHold = false;
            _shootingGun = null;
            _shotController = null;
            _shotStateHash = 0;
            _shotLayer = 0;
            _lastShotFrame = -1;
        }

        private void ApplyView(EPlayerModelType view)
        {
            CancelShotAnimation();
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
            CancelShotAnimation();
            if (_motion == null)
                return;
            if (_holder != null && _holder.CurrentWeapon is Gun gun)
            {
                TryPlayGunAction(gun, WeaponPresentationAction.Hold, 1f, out _, out _);
                return;
            }

            // 刀具保留原有角色动作；枪械不使用刀具或某种枪械的硬编码回退。
            bool firstPerson = _motion.View == EPlayerModelType.FP;
            TryPlayState(firstPerson ? PlayerAnimatorName.FP.draw_butterfly : PlayerAnimatorName.draw_butterfly,
                firstPerson ? "fp_draw_butterfly" : "tp_draw_butterfly",
                firstPerson ? FPLayer : UpLayer, 0f);
        }
    }
}
