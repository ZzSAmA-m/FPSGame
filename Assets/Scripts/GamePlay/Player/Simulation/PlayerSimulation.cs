using UnityEngine;

namespace GamePlay.Player.Simulation
{
    /// <summary>角色运动的统一入口，供服务器、预测和重放调用。</summary>
    [RequireComponent(typeof(PlayerMovementController))]
    public sealed class PlayerSimulation : MonoBehaviour, IPlayerSimulation
    {
        PlayerMovementController _movement;
        PlayerSightController _sight;
        PlayerSimState _state;

        public PlayerSimState State => _state;
        public bool IsReplaying { get; private set; }

        void Awake()
        {
            _movement = GetComponent<PlayerMovementController>();
            _sight = GetComponent<PlayerSightController>();
        }

        public void CaptureFromBody()
        {
            _movement.CaptureFromBody(ref _state);
            _state.pitch = ReadPitch();
        }

        public void SetPhysical(bool enabled)
        {
            _movement.SetPhysical(enabled);
        }

        /// <summary>恢复整份运动状态，不清零速度或重算接地状态。</summary>
        public void RestoreState(in PlayerSimState state)
        {
            var brain = GetComponent<PlayerBrain>();
            if (brain != null && brain.Role == PawnRole.Authoritative && brain.Hitboxes != null &&
                brain.Hitboxes.IsInitialized)
                brain.Hitboxes.BreakContinuity();
            _state = state;
            _movement.RestoreBody(state);
            if (!IsReplaying)
                _sight.ApplyAngles(state.yaw, state.pitch);
        }

        /// <summary>重放中只更新物理状态，结束后统一提交视角。</summary>
        public void SetReplayMode(bool replaying)
        {
            IsReplaying = replaying;
            if (!replaying)
                _sight.ApplyAngles(_state.yaw, _state.pitch);
        }

        public PlayerStepResult Step(in PlayerInputFrame input)
        {
            PlayerMotorConfig config = _movement.BuildConfig(_sight.Sensitivity, _sight.MinPitch, _sight.MaxPitch);
            PlayerStepResult result = _movement.Step(
                ref _state,
                input,
                config,
                SimTime.StepSeconds);
            if (!IsReplaying)
                _sight.ApplyAngles(_state.yaw, _state.pitch);
            return result;
        }

        float ReadPitch()
        {
            if (_sight.View == null)
                return 0f;

            float pitch = _sight.View.localEulerAngles.x;
            if (pitch > 180f)
                pitch -= 360f;
            return pitch;
        }
    }
}
