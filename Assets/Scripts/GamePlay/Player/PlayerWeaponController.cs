using System;
using System.Collections.Generic;
using GamePlay.HoldSystem;
using GamePlay.Player.Simulation;
using UnityEngine;

namespace GamePlay.Player
{
    /// <summary>推进预测和权威武器，转发动作事件并驱动远端表现。</summary>
    public sealed class PlayerWeaponController : MonoBehaviour
    {
        private PlayerBrain _brain;
        private PlayerHolder _holder;
        private Gun _gun;
        private Knife _knife;
        private KnifeState _remoteKnifeState;
        private uint _localTick;
        private uint _serverTick;
        private readonly List<PlayerWeaponAction> _pendingActions = new();
        private bool _processingInput;
        private Gun _remoteActionGun;
        private GunState _remoteAction;
        private double _remoteActionStartTime;
        private double _remoteActionDuration;

        public uint LocalTick => _localTick;
        public uint ServerTick => _serverTick;
        public KnifeState KnifePresentationState => _brain != null && _brain.Role == PawnRole.Interpolated
            ? _remoteKnifeState : _knife?.State ?? KnifeState.None;
        public float KnifeActionProgress => _brain != null && _brain.Role == PawnRole.Interpolated
            ? (_remoteKnifeState is KnifeState.None or KnifeState.Ready ? 0f : RemoteActionProgress)
            : _knife?.ActionProgress ?? 0f;
        public event Action<Knife, KnifeState> OnKnifeStateChanged;
        public WeaponSlot? CurrentSlot => _holder != null ? _holder.CurrentSlot : null;
        /// <summary>远端枪械当前是否处于准备表现阶段。</summary>
        public bool IsRemotePreparing => _remoteActionGun != null && _remoteAction == GunState.Preparing;
        public bool IsRemoteReloading => _remoteActionGun != null && _remoteAction == GunState.Reloading;
        public GunState PresentationState => _brain != null && _brain.Role == PawnRole.Interpolated
            ? _remoteAction : _gun?.GunState ?? GunState.None;
        public float ActionProgress => _brain != null && _brain.Role == PawnRole.Interpolated
            ? RemoteActionProgress : _gun?.ActionProgress ?? 0f;
        private float RemoteActionProgress => _remoteActionDuration > 0
            ? Mathf.Clamp01((float)((Time.unscaledTimeAsDouble - _remoteActionStartTime) / _remoteActionDuration)) : 0f;
        public event Action<Gun> OnShot;
        public event Action<Gun> OnPrepare;
        public event Action<Gun> OnPrepareEnd;
        public event Action<Gun> OnReload;
        public event Action<Gun> OnReloadEnd;
        public event Action<Gun> OnShotCooldownEnd;
        public event Action<Gun, GunState> OnActionCanceled;

        private bool CanForward => isActiveAndEnabled && _brain != null &&
            (_brain.Role == PawnRole.Predicted || _brain.Role == PawnRole.Authoritative) &&
            _holder != null && _gun != null &&
            ReferenceEquals(_holder.CurrentWeapon, _gun);

        private void OnEnable()
        {
            _brain = GetComponent<PlayerBrain>();
            _holder = GetComponent<PlayerHolder>();
            if (_holder != null)
                _holder.OnWeaponChanged += HandleWeaponChanged;
            BindCurrentWeapon();
        }

        private void OnDisable()
        {
            if (_holder != null)
                _holder.OnWeaponChanged -= HandleWeaponChanged;
            UnbindWeapon();
            _pendingActions.Clear();
        }

        private void Update()
        {
            if (_remoteKnifeState is KnifeState.Preparing or KnifeState.LightAttack or KnifeState.HeavyAttack)
            {
                if (RemoteActionProgress >= 1f)
                {
                    _remoteKnifeState = KnifeState.Ready;
                    OnKnifeStateChanged?.Invoke(_knife, KnifeState.Ready);
                }
                return;
            }
            if (!IsRemotePreparing && !IsRemoteReloading)
                return;
            if (_brain == null || _brain.Role != PawnRole.Interpolated || _holder == null ||
                !ReferenceEquals(_holder.CurrentWeapon, _remoteActionGun))
            {
                ClearRemoteAction();
                return;
            }
            if (RemoteActionProgress >= 1f)
                CompleteRemoteAction();
        }

        /// <summary>每个本地实时 Tick 调用一次；时钟独立于移动预测的 Epoch。</summary>
        public bool StepLocal(bool fireRequested) => StepLocal(fireRequested, false).Fired;

        public PlayerWeaponStepResult StepLocal(bool fireRequested, bool reloadRequested) =>
            StepLocal(fireRequested, reloadRequested, false, false);

        public PlayerWeaponStepResult StepLocal(bool fireRequested, bool reloadRequested,
            bool lightAttackPressed, bool heavyAttackPressed)
        {
            if (!isActiveAndEnabled || _brain == null || _brain.Role != PawnRole.Predicted)
                return default;

            return Execute(new LocalWeaponCommand(unchecked(++_localTick), fireRequested, reloadRequested,
                lightAttackPressed, heavyAttackPressed));
        }

        /// <summary>每个服务器实时 Tick 调用一次；只有实际发射并扣弹才返回 true。</summary>
        public bool StepServer(bool fireRequested) => StepServer(fireRequested, false).Fired;

        public PlayerWeaponStepResult StepServer(bool fireRequested, bool reloadRequested) =>
            StepServer(fireRequested, reloadRequested, false, false);

        public PlayerWeaponStepResult StepServer(bool fireRequested, bool reloadRequested,
            bool lightAttackPressed, bool heavyAttackPressed)
        {
            if (!isActiveAndEnabled || _brain == null || _brain.Role != PawnRole.Authoritative)
                return default;

            return Execute(new LocalWeaponCommand(unchecked(++_serverTick), fireRequested, reloadRequested,
                lightAttackPressed, heavyAttackPressed));
        }

        /// <summary>取走按实际产生顺序记录的权威事实，包含两拍之间的 C# 业务调用。</summary>
        public IReadOnlyList<PlayerWeaponAction> ConsumeActions()
        {
            if (_pendingActions.Count == 0)
                return Array.Empty<PlayerWeaponAction>();
            PlayerWeaponAction[] actions = _pendingActions.ToArray();
            _pendingActions.Clear();
            return actions;
        }

        internal void BeginInputStep() => _processingInput = true;
        internal void EndInputStep() => _processingInput = false;

        /// <summary>应用权威业务主动发起的操作，自身预测输入的确认由消息处理器区分。</summary>
        public bool ApplyAuthoritativeAction(PlayerWeaponActionKind kind, WeaponSlot slot, GunState canceledState = GunState.None)
        {
            if (!isActiveAndEnabled || _brain == null || _brain.Role != PawnRole.Predicted ||
                _holder == null || !_holder.InitializeLoadout())
                return false;
            if (kind == PlayerWeaponActionKind.Switched)
                return _holder.SelectSlot(slot);
            if (_holder.CurrentSlot == slot && _knife != null && kind == PlayerWeaponActionKind.PrepareStarted)
                return _knife.State == KnifeState.Preparing || _knife.TryPrepare();
            if (_holder.CurrentSlot != slot || _holder.CurrentWeapon is not Gun gun)
                return false;
            switch (kind)
            {
                case PlayerWeaponActionKind.Canceled:
                    if (gun.GunState == canceledState)
                        return gun.TryReturnToNormal();
                    if (canceledState == GunState.Reloading && _brain.Animator != null &&
                        _brain.Animator.IsReloadAnimationPlaying)
                    {
                        if (_holder.CurrentController is GunModelController model)
                            model.PresentHold();
                        OnActionCanceled?.Invoke(gun, canceledState);
                        return true;
                    }
                    return false;
                case PlayerWeaponActionKind.PrepareStarted:
                    // 新的权威准备事实重新建立动作；本地可能仍在上一轮准备中。
                    if (gun.GunState == GunState.Preparing)
                        gun.Unequip();
                    return gun.TryPrepare();
                case PlayerWeaponActionKind.ReloadStarted:
                    return gun.TryReload();
                default:
                    return false;
            }
        }

        /// <summary>按服务器指定栏位切换远端装备，准备由独立的开始事件驱动。</summary>
        public bool PresentRemoteWeaponSwitch(WeaponSlot slot)
        {
            if (!isActiveAndEnabled || _brain == null || _brain.Role != PawnRole.Interpolated ||
                _holder == null || !_holder.InitializeLoadout())
                return false;

            WeaponEntry entry = slot switch
            {
                WeaponSlot.Rifle => _holder.Rifle,
                WeaponSlot.Pistol => _holder.Pistol,
                WeaponSlot.Knife => _holder.Knife,
                _ => null
            };
            if (entry == null)
                return false;
            return _holder.SelectSlot(slot);
        }

        public bool PresentRemotePrepare(WeaponSlot slot) => slot == WeaponSlot.Knife
            ? StartRemoteKnifeAction(slot, KnifeState.Preparing) : StartRemoteAction(slot, GunState.Preparing);

        public bool PresentRemoteKnifeAttack(WeaponSlot slot, KnifeAttackKind kind) => kind switch
        {
            KnifeAttackKind.Light => StartRemoteKnifeAction(slot, KnifeState.LightAttack),
            KnifeAttackKind.Heavy => StartRemoteKnifeAction(slot, KnifeState.HeavyAttack),
            _ => false
        };

        private bool StartRemoteKnifeAction(WeaponSlot slot, KnifeState state)
        {
            if (slot != WeaponSlot.Knife || !PresentRemoteWeaponSwitch(slot) || _knife == null)
                return false;

            _remoteKnifeState = state;
            _remoteActionStartTime = Time.unscaledTimeAsDouble;
            uint ticks = _knife.Config.GetDuration(state);
            _remoteActionDuration = ticks * (double)SimTime.StepSeconds;
            OnKnifeStateChanged?.Invoke(_knife, state);
            return true;
        }

        public bool PresentRemoteReload(WeaponSlot slot) => StartRemoteAction(slot, GunState.Reloading);

        /// <summary>按服务器指定栏位播放远端开火，复用现有角色、音效及枪械模型表现。</summary>
        public bool PresentRemoteFire(WeaponSlot slot)
        {
            if (!TryGetRemoteGun(slot, out Gun gun, out GunModelController model))
                return false;

            if (IsRemotePreparing || (IsRemoteReloading && RemoteActionProgress >= 1f))
                CompleteRemoteAction();
            else if (IsRemoteReloading)
                CancelRemoteAction();
            else
                ClearRemoteAction();
            _remoteActionGun = gun;
            _remoteAction = GunState.Shooting;
            model.PresentFire();
            OnShot?.Invoke(gun);
            return true;
        }

        public bool PresentRemoteActionCanceled(WeaponSlot slot, GunState state)
        {
            if (!isActiveAndEnabled || _brain == null || _brain.Role != PawnRole.Interpolated ||
                _holder == null || _holder.CurrentSlot != slot)
                return false;
            bool reloadStillPlaying = state == GunState.Reloading && _brain.Animator != null &&
                _brain.Animator.IsReloadAnimationPlaying;
            if (_remoteAction != state && !reloadStillPlaying)
                return false;
            CancelRemoteAction(_holder.CurrentWeapon as Gun, state);
            return true;
        }

        private bool TryGetRemoteGun(WeaponSlot slot, out Gun gun, out GunModelController model)
        {
            gun = null;
            model = null;
            if (!isActiveAndEnabled || _brain == null || _brain.Role != PawnRole.Interpolated ||
                _holder == null || !_holder.InitializeLoadout())
                return false;
            WeaponEntry entry = slot switch
            {
                WeaponSlot.Rifle => _holder.Rifle,
                WeaponSlot.Pistol => _holder.Pistol,
                _ => null
            };
            if (entry?.Weapon is not Gun selectedGun || !_holder.SelectSlot(slot) ||
                !ReferenceEquals(_holder.CurrentEntry, entry) ||
                _holder.CurrentController is not GunModelController selectedModel || !selectedModel.isActiveAndEnabled)
                return false;
            gun = selectedGun;
            model = selectedModel;
            return true;
        }

        private bool StartRemoteAction(WeaponSlot slot, GunState state)
        {
            if (!TryGetRemoteGun(slot, out Gun gun, out GunModelController model))
                return false;
            if (IsRemotePreparing || IsRemoteReloading)
                CompleteRemoteAction();
            else
                ClearRemoteAction();
            _remoteActionGun = gun;
            _remoteAction = state;
            _remoteActionStartTime = Time.unscaledTimeAsDouble;
            uint ticks = state == GunState.Preparing ? gun.Config.PrepareTicks : gun.Config.ReloadTicks;
            _remoteActionDuration = ticks * (double)SimTime.StepSeconds;
            if (state == GunState.Preparing)
            {
                model.PresentPrepare();
                OnPrepare?.Invoke(gun);
            }
            else
            {
                model.PresentReload();
                OnReload?.Invoke(gun);
            }
            return true;
        }

        private void CompleteRemoteAction()
        {
            Gun gun = _remoteActionGun;
            GunState state = _remoteAction;
            ClearRemoteAction();
            if (gun == null || _holder == null || !ReferenceEquals(_holder.CurrentWeapon, gun))
                return;
            if (state == GunState.Preparing)
            {
                if (_holder.CurrentController is GunModelController model && model.isActiveAndEnabled)
                    model.PresentHold();
                OnPrepareEnd?.Invoke(gun);
            }
            else if (state == GunState.Reloading)
                OnReloadEnd?.Invoke(gun);
        }

        private void CancelRemoteAction(Gun canceledGun = null, GunState? canceledState = null)
        {
            Gun gun = canceledGun ?? _remoteActionGun;
            GunState state = canceledState ?? _remoteAction;
            ClearRemoteAction();
            if (gun == null || _holder == null || !ReferenceEquals(_holder.CurrentWeapon, gun))
                return;
            if (_holder.CurrentController is GunModelController model && model.isActiveAndEnabled)
                model.PresentHold();
            OnActionCanceled?.Invoke(gun, state);
        }

        private void ClearRemoteAction()
        {
            _remoteActionGun = null;
            _remoteAction = GunState.Normal;
            _remoteActionStartTime = 0;
            _remoteActionDuration = 0;
        }

        private PlayerWeaponStepResult Execute(in LocalWeaponCommand command)
        {
            Knife knife = _knife;
            if (knife != null && ReferenceEquals(knife, _holder.CurrentWeapon))
            {
                knife.Tick(command.Tick);
                if (!ReferenceEquals(knife, _knife)) return default;
                if (knife.State == KnifeState.None)
                    knife.TryPrepare();
                else if (command.LightAttackPressed)
                    knife.TryAttack(KnifeAttackKind.Light);
                else if (command.HeavyAttackPressed)
                    knife.TryAttack(KnifeAttackKind.Heavy);
                return default;
            }
            Gun gun = _gun;
            if (gun == null || _holder == null || !ReferenceEquals(_holder.CurrentWeapon, gun))
                return default;

            gun.Tick(command.Tick);
            if (!ReferenceEquals(gun, _gun))
                return default;
            if (gun.GunState == GunState.None)
                gun.TryPrepare();
            bool fired = command.FireRequested && ReferenceEquals(gun, _gun) && gun.TryFire();
            bool reloaded = !fired && command.ReloadRequested && ReferenceEquals(gun, _gun) && gun.TryReload();
            return new PlayerWeaponStepResult(fired, reloaded);
        }

        private void HandleWeaponChanged()
        {
            BindCurrentWeapon();
            RecordAction(PlayerWeaponActionKind.Switched);
        }

        private void RecordAction(PlayerWeaponActionKind kind, GunState canceledState = GunState.None,
            KnifeAttackKind knifeAttackKind = default)
        {
            if (isActiveAndEnabled && _brain != null && _brain.Role == PawnRole.Authoritative &&
                _holder != null && _holder.CurrentSlot.HasValue)
            {
                Ray ray = kind == PlayerWeaponActionKind.Shot ? _brain.GetShootRay() : default;
                _pendingActions.Add(new PlayerWeaponAction(kind, _holder.CurrentSlot.Value, canceledState,
                    ray.origin, ray.direction, !_processingInput, knifeAttackKind));
            }
        }

        private void BindCurrentWeapon()
        {
            Gun next = _holder != null ? _holder.CurrentWeapon as Gun : null;
            Knife nextKnife = _holder != null ? _holder.CurrentWeapon as Knife : null;
            if (ReferenceEquals(next, _gun) && ReferenceEquals(nextKnife, _knife))
                return;
            UnbindWeapon();
            _gun = next;
            _knife = nextKnife;
            if (_knife != null)
            {
                if (_brain != null && _brain.Role == PawnRole.Authoritative)
                    _knife.Tick(_serverTick);
                else if (_brain != null && _brain.Role == PawnRole.Predicted)
                    _knife.Tick(_localTick);
                _knife.OnStateChanged += ForwardKnifeState;
            }
            if (_gun != null)
            {
                // 切回长期收起的枪时，先对齐当前武器时钟，再接受拍间业务请求。
                if (_brain != null && _brain.Role == PawnRole.Authoritative)
                    _gun.Tick(_serverTick);
                else if (_brain != null && _brain.Role == PawnRole.Predicted)
                    _gun.Tick(_localTick);
                _gun.OnShot += ForwardShot;
                _gun.OnPrepare += ForwardPrepare;
                _gun.OnPrepareCompleted += ForwardPrepareEnd;
                _gun.OnReload += ForwardReload;
                _gun.OnReloadCompleted += ForwardReloadEnd;
                _gun.OnShotCooldownEnd += ForwardShotCooldownEnd;
                _gun.OnActionCanceled += ForwardActionCanceled;
            }
        }

        private void UnbindWeapon()
        {
            ClearRemoteAction();
            _remoteKnifeState = KnifeState.None;
            if (_knife != null)
                _knife.OnStateChanged -= ForwardKnifeState;
            _knife = null;
            if (_gun != null)
            {
                _gun.OnShot -= ForwardShot;
                _gun.OnPrepare -= ForwardPrepare;
                _gun.OnPrepareCompleted -= ForwardPrepareEnd;
                _gun.OnReload -= ForwardReload;
                _gun.OnReloadCompleted -= ForwardReloadEnd;
                _gun.OnShotCooldownEnd -= ForwardShotCooldownEnd;
                _gun.OnActionCanceled -= ForwardActionCanceled;
            }
            _gun = null;
        }

        private void ForwardKnifeState(KnifeState state)
        {
            if (!isActiveAndEnabled || _brain == null ||
                (_brain.Role != PawnRole.Predicted && _brain.Role != PawnRole.Authoritative) ||
                _holder == null || !ReferenceEquals(_holder.CurrentWeapon, _knife))
                return;
            if (state == KnifeState.Preparing)
                RecordAction(PlayerWeaponActionKind.PrepareStarted);
            else if (state is KnifeState.LightAttack or KnifeState.HeavyAttack)
                RecordAction(PlayerWeaponActionKind.KnifeAttack, knifeAttackKind: state == KnifeState.LightAttack
                    ? KnifeAttackKind.Light : KnifeAttackKind.Heavy);
            OnKnifeStateChanged?.Invoke(_knife, state);
        }

        private void ForwardShot()
        {
            if (CanForward)
            {
                RecordAction(PlayerWeaponActionKind.Shot);
                OnShot?.Invoke(_gun);
            }
        }

        private void ForwardPrepare()
        {
            if (CanForward)
            {
                RecordAction(PlayerWeaponActionKind.PrepareStarted);
                OnPrepare?.Invoke(_gun);
            }
        }

        private void ForwardPrepareEnd()
        {
            if (CanForward)
                OnPrepareEnd?.Invoke(_gun);
        }

        private void ForwardReload()
        {
            if (CanForward)
            {
                RecordAction(PlayerWeaponActionKind.ReloadStarted);
                OnReload?.Invoke(_gun);
            }
        }

        private void ForwardReloadEnd()
        {
            if (CanForward)
                OnReloadEnd?.Invoke(_gun);
        }

        private void ForwardShotCooldownEnd()
        {
            if (CanForward)
                OnShotCooldownEnd?.Invoke(_gun);
        }

        private void ForwardActionCanceled(GunState state)
        {
            if (CanForward)
            {
                RecordAction(PlayerWeaponActionKind.Canceled, state);
                OnActionCanceled?.Invoke(_gun, state);
            }
        }

    }
}
