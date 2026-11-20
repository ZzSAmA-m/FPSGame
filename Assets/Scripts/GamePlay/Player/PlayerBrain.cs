using System;
using GamePlay.HoldSystem;
using GamePlay.Player.Combat;
using GamePlay.Player.Presentation;
using GamePlay.Player.Simulation;
using UnityEngine;

namespace GamePlay.Player
{
    /// <summary>玩家实时输入门面：组合组件、分发一拍输入，并按网络身份启用驱动。</summary>
    public sealed class PlayerBrain : MonoBehaviour
    {
        public PlayerBasicDataSO BasicData => Stats != null ? Stats.Config : null;
        public PlayerStatsController Stats { get; private set; }
        public PlayerHitboxController Hitboxes { get; private set; }
        public PlayerCombatPoseController CombatPose { get; private set; }
        public PawnRole Role { get; private set; }
        public PlayerSimulation Simulation { get; private set; }
        public PlayerPrediction Prediction { get; private set; }
        public RemotePoseInterpolator Interpolator { get; private set; }
        public PlayerSightController Sight { get; private set; }
        public PlayerWeaponController Weapon { get; private set; }
        public PlayerAnimatorController Animator { get; private set; }
        public PlayerMotionController Motion { get; private set; }
        public PlayerModelController Model { get; private set; }
        public PlayerCameraController Camera { get; private set; }
        public PlayerCorrectionSmoother CorrectionSmoother { get; private set; }
        public PlayerSimState State => Simulation.State;
        bool _serverStepPending;
        bool _serverAcceptsGameplayInput;
        WeaponSlot? _serverSwitchedSlot;
        WeaponSlot _serverFireSlot;
        internal bool AcceptsGameplayInput => Stats == null || Stats.LifeState != PlayerLifeState.Dead;

        public Vector3 DisplayVelocity =>
            Role == PawnRole.Interpolated ? Interpolator.Velocity : Simulation.State.velocity;

        void Awake()
        {
            Stats = GetComponent<PlayerStatsController>();
            Hitboxes = GetComponent<PlayerHitboxController>();
            CombatPose = GetComponent<PlayerCombatPoseController>();
            Simulation = GetComponent<PlayerSimulation>();
            Sight = GetComponent<PlayerSightController>();
            Weapon = GetComponent<PlayerWeaponController>();
            Animator = GetComponent<PlayerAnimatorController>();
            Motion = GetComponent<PlayerMotionController>();
            Model = GetComponent<PlayerModelController>();
            Camera = GetComponent<PlayerCameraController>();

            CorrectionSmoother = GetComponent<PlayerCorrectionSmoother>();
            if (CorrectionSmoother == null)
                CorrectionSmoother = gameObject.AddComponent<PlayerCorrectionSmoother>();
            CorrectionSmoother.Initialize(Model.FirstPersonRoot, Model.ThirdPersonRoot);

            Prediction = GetComponent<PlayerPrediction>();
            if (Prediction == null)
                Prediction = gameObject.AddComponent<PlayerPrediction>();

            Interpolator = GetComponent<RemotePoseInterpolator>();
            if (Interpolator == null)
                Interpolator = gameObject.AddComponent<RemotePoseInterpolator>();

            Prediction.enabled = false;
            Interpolator.enabled = false;
            CorrectionSmoother.enabled = false;
        }

        /// <summary>服务器实时步进；调用方在此拍全部完成后确认输入并处理网络广播。</summary>
        public PlayerLiveStepResult StepServer(in PlayerInputFrame input)
        {
            BeginServerStep(input);
            return CompleteServerStep(input);
        }

        /// <summary>先完成所有玩家的移动和切栏，再统一推进武器。</summary>
        public void BeginServerStep(in PlayerInputFrame input) => BeginServerStep(input, AcceptsGameplayInput);

        internal void BeginServerStep(in PlayerInputFrame input, bool acceptsGameplayInput)
        {
            ValidateLive(PawnRole.Authoritative);
            if (_serverStepPending)
                throw new InvalidOperationException("上一拍服务端输入尚未完成。");
            Weapon.BeginInputStep();
            _serverStepPending = true;
            _serverAcceptsGameplayInput = acceptsGameplayInput;
            try
            {
                var previous = Weapon.CurrentSlot;
                Motion.Present(Simulation.Step(acceptsGameplayInput ? input : input.WithoutActions()));
                var selected = Weapon.CurrentSlot;
                _serverSwitchedSlot = selected != previous ? selected : null;
                _serverFireSlot = selected.GetValueOrDefault();
            }
            catch
            {
                AbortServerStep();
                throw;
            }
        }

        public PlayerLiveStepResult CompleteServerStep(in PlayerInputFrame input, bool? canFire = null)
        {
            if (!_serverStepPending)
                throw new InvalidOperationException("必须先推进服务端移动。");
            try
            {
                ValidateLive(PawnRole.Authoritative);
                bool fired = Weapon.StepServer(input.fire && _serverAcceptsGameplayInput && (canFire ?? true),
                    input.reload && _serverAcceptsGameplayInput, input.lightAttackPressed && _serverAcceptsGameplayInput,
                    input.heavyAttackPressed && _serverAcceptsGameplayInput).Fired;
                var actions = Weapon.ConsumeActions();
                Ray ray = fired ? GetShootRay() : default;
                return new PlayerLiveStepResult(State, fired, ray.origin, ray.direction,
                    _serverFireSlot, _serverSwitchedSlot, actions);
            }
            finally { AbortServerStep(); }
        }

        public void AbortServerStep()
        {
            if (!_serverStepPending) return;
            _serverStepPending = false;
            Weapon?.EndInputStep();
        }

        /// <summary>本地实时预测；运动回滚只能调用 IPlayerSimulation，不进入此入口。</summary>
        public PlayerLiveStepResult StepPredicted(in PlayerInputFrame input)
        {
            Weapon?.BeginInputStep();
            try
            {
                ValidateLive(PawnRole.Predicted);
                return ExecutePredicted(AcceptsGameplayInput ? input : input.WithoutActions());
            }
            finally
            {
                Weapon?.EndInputStep();
            }
        }

        private PlayerLiveStepResult ExecutePredicted(in PlayerInputFrame input)
        {
            PlayerStepResult movement = Simulation.Step(input);
            var previousWeaponSlot = Weapon.CurrentSlot;
            Motion.Present(movement);
            var selectedWeaponSlot = Weapon.CurrentSlot;
            var switchedWeaponSlot = selectedWeaponSlot != previousWeaponSlot ? selectedWeaponSlot : null;
            var fireWeaponSlot = selectedWeaponSlot.GetValueOrDefault();
            PlayerWeaponStepResult weaponResult = Weapon.StepLocal(input.fire, input.reload,
                input.lightAttackPressed, input.heavyAttackPressed);
            bool fired = weaponResult.Fired;
            var weaponActions = Weapon.ConsumeActions();
            Ray ray = fired ? GetShootRay() : default;
            return new PlayerLiveStepResult(State, fired, ray.origin, ray.direction, fireWeaponSlot,
                switchedWeaponSlot, weaponActions);
        }

        void ValidateLive(PawnRole expectedRole)
        {
            // 在任何运动、切栏或武器副作用前拒绝错误的驱动身份。
            if (Role != expectedRole)
                throw new InvalidOperationException($"[{nameof(PlayerBrain)}] {Role} 不能执行 {expectedRole} 的实时输入。");
            if (Simulation == null || Motion == null || Weapon == null)
                throw new InvalidOperationException($"[{nameof(PlayerBrain)}] 实时输入需要完整的运动、动作和武器组件。");
            if (Simulation.IsReplaying)
                throw new InvalidOperationException($"[{nameof(PlayerBrain)}] 运动回放不能进入实时输入门面。");

        }

        internal Ray GetShootRay() => PlayerShootConfig.CreateShootRay(GetViewRay());

        public Ray GetViewRay()
        {
            if (Camera != null && Camera.TryGetViewRay(out Ray cameraRay))
                return cameraRay;
            Vector3 origin = Simulation != null ? State.position : transform.position;
            if (Sight != null && Sight.View != null)
                origin = Sight.View.position;
            Vector3 direction = Simulation != null
                ? Quaternion.Euler(State.pitch, State.yaw, 0f) * Vector3.forward : transform.forward;
            return new Ray(origin, direction);
        }

        public void SetRole(PawnRole role)
        {
            Role = role;
            Prediction.ResetPrediction();
            CorrectionSmoother.ResetOffset();
            Simulation.CaptureFromBody();
            Simulation.SetPhysical(role != PawnRole.Interpolated);
            Prediction.enabled = role == PawnRole.Predicted;
            Interpolator.enabled = role == PawnRole.Interpolated;
            CorrectionSmoother.enabled = role == PawnRole.Predicted;
        }
    }
}
