using System;
using Core.ZLog;
using GamePlay.HoldSystem;
using GamePlay.Player.Simulation;
using UnityEngine;

namespace GamePlay.Player
{
    /// <summary>驱动本地预测或服务器权威枪械，并转发生命周期事实；移动回滚不执行此链路。</summary>
    public sealed class PlayerWeaponController : MonoBehaviour
    {
        private PlayerBrain _brain;
        private PlayerHolder _holder;
        private Gun _gun;
        private uint _localTick;
        private uint _serverTick;

        public uint LocalTick => _localTick;
        public uint ServerTick => _serverTick;
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
                _holder.OnWeaponChanged += BindCurrentGun;
            BindCurrentGun();
        }

        private void OnDisable()
        {
            if (_holder != null)
                _holder.OnWeaponChanged -= BindCurrentGun;
            UnbindGun();
        }

        /// <summary>每个本地实时 Tick 调用一次；时钟独立于移动预测的 Epoch。</summary>
        public bool StepLocal(bool fireRequested)
        {
            if (!isActiveAndEnabled || _brain == null || _brain.Role != PawnRole.Predicted)
                return false;

            var command = new LocalWeaponCommand(unchecked(++_localTick), fireRequested);
            return Execute(command.Tick, command.FireRequested);
        }

        /// <summary>每个服务器实时 Tick 调用一次；只有实际发射并扣弹才返回 true。</summary>
        public bool StepServer(bool fireRequested)
        {
            if (!isActiveAndEnabled || _brain == null || _brain.Role != PawnRole.Authoritative)
                return false;

            return Execute(unchecked(++_serverTick), fireRequested);
        }

        private bool Execute(uint tick, bool fireRequested)
        {
            Gun gun = _gun;
            if (gun == null || _holder == null || !ReferenceEquals(_holder.CurrentWeapon, gun))
                return false;

            gun.Tick(tick);
            if (!ReferenceEquals(gun, _gun))
                return false;
            if (gun.GunState == GunState.None)
                gun.TryPrepare();
            return fireRequested && ReferenceEquals(gun, _gun) && gun.TryFire();
        }

        private void BindCurrentGun()
        {
            Gun next = _holder != null ? _holder.CurrentWeapon as Gun : null;
            if (ReferenceEquals(next, _gun))
                return;
            UnbindGun();
            _gun = next;
            if (_gun != null)
            {
                _gun.OnShot += ForwardShot;
                _gun.OnPrepare += ForwardPrepare;
                _gun.OnPrepareCompleted += ForwardPrepareEnd;
                _gun.OnReload += ForwardReload;
                _gun.OnReloadCompleted += ForwardReloadEnd;
                _gun.OnShotCooldownEnd += ForwardShotCooldownEnd;
                _gun.OnActionCanceled += ForwardActionCanceled;
            }
        }

        private void UnbindGun()
        {
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

        private void ForwardShot()
        {
            if (CanForward)
                OnShot?.Invoke(_gun);
        }

        private void ForwardPrepare()
        {
            if (CanForward)
                OnPrepare?.Invoke(_gun);
        }

        private void ForwardPrepareEnd()
        {
            if (CanForward)
                OnPrepareEnd?.Invoke(_gun);
        }

        private void ForwardReload()
        {
            if (CanForward)
                OnReload?.Invoke(_gun);
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
                OnActionCanceled?.Invoke(_gun, state);
        }

    }
}
