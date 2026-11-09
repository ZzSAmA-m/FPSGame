using System;
using GamePlay.Player.Presentation;
using GamePlay.Player.Simulation;
using UnityEngine;

namespace GamePlay.Player
{
    /// <summary>玩家实时输入门面：组合组件、分发一拍输入，并按网络身份启用驱动。</summary>
    public sealed class PlayerBrain : MonoBehaviour
    {
        [SerializeField] PlayerBasicDataSO _playerBasicData;

        public PlayerBasicDataSO BasicData => _playerBasicData;
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

        public Vector3 DisplayVelocity =>
            Role == PawnRole.Interpolated ? Interpolator.Velocity : Simulation.State.velocity;

        void Awake()
        {
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
        public PlayerLiveStepResult StepServer(in PlayerInputFrame input) => StepLive(input, PawnRole.Authoritative);

        /// <summary>本地实时预测；运动回滚只能调用 IPlayerSimulation，不进入此入口。</summary>
        public PlayerLiveStepResult StepPredicted(in PlayerInputFrame input) => StepLive(input, PawnRole.Predicted);

        private PlayerLiveStepResult StepLive(in PlayerInputFrame input, PawnRole expectedRole)
        {
            // 在任何运动、切栏或武器副作用前拒绝错误的驱动身份。
            if (Role != expectedRole)
                throw new InvalidOperationException($"[{nameof(PlayerBrain)}] {Role} 不能执行 {expectedRole} 的实时输入。");
            if (Simulation == null || Motion == null || Weapon == null)
                throw new InvalidOperationException($"[{nameof(PlayerBrain)}] 实时输入需要完整的运动、动作和武器组件。");
            if (Simulation.IsReplaying)
                throw new InvalidOperationException($"[{nameof(PlayerBrain)}] 运动回放不能进入实时输入门面。");

            PlayerStepResult movement = Simulation.Step(input);
            // 切栏在本拍射击之前完成；起跳通知由动作组件独立分发。
            Motion.Present(movement);
            bool fired = expectedRole == PawnRole.Authoritative
                ? Weapon.StepServer(input.fire)
                : Weapon.StepLocal(input.fire);

            PlayerSimState state = State;
            Vector3 origin = default;
            Vector3 direction = default;
            if (fired)
            {
                origin = Sight != null && Sight.View != null ? Sight.View.position : state.position;
                direction = Quaternion.Euler(state.pitch, state.yaw, 0f) * Vector3.forward;
            }

            return new PlayerLiveStepResult(state, fired, origin, direction);
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
