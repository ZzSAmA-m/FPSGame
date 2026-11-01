using GamePlay.Player.Presentation;
using GamePlay.Player.Simulation;
using UnityEngine;

namespace GamePlay.Player
{
    /// <summary>组合玩家组件，并按网络身份启用对应的驱动。</summary>
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
        public PlayerModelController Model { get; private set; }
        public PlayerCameraController Camera { get; private set; }
        public PlayerCorrectionSmoother CorrectionSmoother { get; private set; }

        public Vector3 DisplayVelocity =>
            Role == PawnRole.Interpolated ? Interpolator.Velocity : Simulation.State.velocity;

        void Awake()
        {
            Simulation = GetComponent<PlayerSimulation>();
            Sight = GetComponent<PlayerSightController>();
            Weapon = GetComponent<PlayerWeaponController>();
            Animator = GetComponent<PlayerAnimatorController>();
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
