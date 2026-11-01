using GamePlay.Physics;
using UnityEngine;

namespace GamePlay.Player.Simulation
{
    /// <summary>角色运动的统一入口，供服务器、预测和重放调用。</summary>
    [RequireComponent(typeof(CharacterController))]
    public sealed class PlayerSimulation : MonoBehaviour, IPlayerSimulation
    {
        [SerializeField] PlayerMovementDataSO _playerMovementDataSO;
        [SerializeField] float _gravityScale = 1f;
        [SerializeField] float _groundedForce;

        CharacterController _body;
        PlayerSightController _sight;
        PlayerSimState _state;

        public PlayerSimState State => _state;
        public bool IsReplaying { get; private set; }

        void Awake()
        {
            _body = GetComponent<CharacterController>();
            _sight = GetComponent<PlayerSightController>();
        }

        public void CaptureFromBody()
        {
            _state.position = transform.position;
            _state.yaw = transform.eulerAngles.y;
            _state.pitch = ReadPitch();
            _state.verticalVelocity = 0f;
            _state.velocity = Vector3.zero;
            _state.grounded = true;
        }

        public void SetPhysical(bool enabled)
        {
            _body.enabled = enabled;
        }

        /// <summary>恢复整份运动状态，不清零速度或重算接地状态。</summary>
        public void RestoreState(in PlayerSimState state)
        {
            bool physical = _body.enabled;
            if (physical)
                _body.enabled = false;

            _state = state;
            transform.SetPositionAndRotation(state.position, Quaternion.Euler(0f, state.yaw, 0f));

            if (physical)
                _body.enabled = true;
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
            Vector3 before = transform.position;
            PlayerStepResult result = PlayerMotor.Integrate(
                ref _state,
                input,
                BuildConfig(),
                SimTime.StepSeconds,
                out Vector3 displacement);

            _body.Move(displacement);
            _state.position = transform.position;
            _state.velocity = (transform.position - before) / SimTime.StepSeconds;
            _state.grounded = _body.isGrounded;
            transform.rotation = Quaternion.Euler(0f, _state.yaw, 0f);
            if (!IsReplaying)
                _sight.ApplyAngles(_state.yaw, _state.pitch);
            return result;
        }

        PlayerMotorConfig BuildConfig()
        {
            return new PlayerMotorConfig
            {
                moveSpeed = _playerMovementDataSO.MoveSpeed,
                jumpSpeed = _playerMovementDataSO.JumpSpeed,
                gravity = GForce.globalGForce * _gravityScale,
                groundedForce = _groundedForce,
                lookSensitivity = _sight.Sensitivity,
                minPitch = _sight.MinPitch,
                maxPitch = _sight.MaxPitch
            };
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
