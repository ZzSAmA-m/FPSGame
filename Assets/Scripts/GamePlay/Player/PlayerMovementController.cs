using GamePlay.Physics;
using GamePlay.Player.Simulation;
using UnityEngine;

namespace GamePlay.Player
{
    /// <summary>执行一拍角色移动和碰撞；输入、步长及唯一的运动状态由模拟入口传入。</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterController))]
    public sealed class PlayerMovementController : MonoBehaviour
    {
        [SerializeField] PlayerMovementDataSO _playerMovementDataSO;
        [SerializeField] float _gravityScale = 1f;
        [SerializeField] float _groundedForce;

        CharacterController _body;

        // 不依赖组件 Awake 的执行顺序，隔离场景中的显式模拟也可直接调用。
        CharacterController Body
        {
            get
            {
                if (_body == null)
                    _body = GetComponent<CharacterController>();
                return _body;
            }
        }

        /// <summary>每拍读取移动配置，并组合模拟入口注入的视角参数。</summary>
        public PlayerMotorConfig BuildConfig(float lookSensitivity, float minPitch, float maxPitch)
        {
            return new PlayerMotorConfig
            {
                moveSpeed = _playerMovementDataSO.MoveSpeed,
                jumpSpeed = _playerMovementDataSO.JumpSpeed,
                groundAcceleration = _playerMovementDataSO.GroundAcceleration,
                groundFriction = _playerMovementDataSO.GroundFriction,
                stopSpeed = _playerMovementDataSO.StopSpeed,
                surfaceFriction = _playerMovementDataSO.SurfaceFriction,
                gravity = GForce.globalGForce * _gravityScale,
                groundedForce = _groundedForce,
                lookSensitivity = lookSensitivity,
                minPitch = minPitch,
                maxPitch = maxPitch
            };
        }

        /// <summary>从角色现场初始化运动状态；俯仰由模拟入口读取视角后补齐。</summary>
        public void CaptureFromBody(ref PlayerSimState state)
        {
            state.position = transform.position;
            state.yaw = transform.eulerAngles.y;
            state.verticalVelocity = 0f;
            state.velocity = Vector3.zero;
            state.grounded = true;
        }

        public void SetPhysical(bool enabled) => Body.enabled = enabled;

        /// <summary>只恢复物理现场，保留原来的物理启停状态，不重新计算运动状态。</summary>
        public void RestoreBody(in PlayerSimState state)
        {
            CharacterController body = Body;
            bool physical = body.enabled;
            if (physical)
                body.enabled = false;

            transform.SetPositionAndRotation(state.position, Quaternion.Euler(0f, state.yaw, 0f));

            if (physical)
                body.enabled = true;
        }

        /// <summary>积分后执行碰撞移动，并将实际位置、速度和接地结果写回调用方状态。</summary>
        public PlayerStepResult Step(
            ref PlayerSimState state,
            in PlayerInputFrame input,
            in PlayerMotorConfig config,
            float dt)
        {
            Vector3 before = transform.position;
            PlayerStepResult result = PlayerMotor.Integrate(ref state, input, config, dt);

            Vector3 wish = Vector3.ClampMagnitude(new Vector3(input.move.x, 0f, input.move.y), 1f);
            float inputMagnitude = wish.magnitude;
            Vector3 wishDirection = inputMagnitude > 0f
                ? Quaternion.Euler(0f, state.yaw, 0f) * (wish / inputMagnitude)
                : Vector3.zero;
            float wishSpeed = config.moveSpeed * inputMagnitude;

            Vector3 horizontal;
            if (state.grounded)
            {
                // 使用上一拍碰撞后的世界空间速度；转向只改变本拍的期望方向。
                horizontal = new Vector3(state.velocity.x, 0f, state.velocity.z);
                horizontal = ApplyGroundFriction(horizontal, config, dt);
                horizontal = AccelerateGround(horizontal, wishDirection, wishSpeed, config, dt);
            }
            else
            {
                // 起跳成功当拍也使用原有空中规则，空中加速留待后续实现。
                horizontal = wishDirection * wishSpeed;
            }

            state.velocity = new Vector3(horizontal.x, state.verticalVelocity, horizontal.z);
            Vector3 displacement = state.velocity * dt;

            CharacterController body = Body;
            body.Move(displacement);
            state.position = transform.position;
            state.velocity = (state.position - before) / dt;
            state.grounded = body.isGrounded;
            transform.rotation = Quaternion.Euler(0f, state.yaw, 0f);
            return result;
        }

        static Vector3 ApplyGroundFriction(Vector3 horizontal, in PlayerMotorConfig config, float dt)
        {
            float speed = horizontal.magnitude;
            if (speed == 0f)
                return Vector3.zero;

            float controlSpeed = Mathf.Max(speed, config.stopSpeed);
            float drop = controlSpeed * config.groundFriction * config.surfaceFriction * dt;
            float newSpeed = Mathf.Max(0f, speed - drop);
            return horizontal * (newSpeed / speed);
        }

        static Vector3 AccelerateGround(
            Vector3 horizontal,
            Vector3 wishDirection,
            float wishSpeed,
            in PlayerMotorConfig config,
            float dt)
        {
            float addSpeed = wishSpeed - Vector3.Dot(horizontal, wishDirection);
            if (addSpeed <= 0f)
                return horizontal;

            float acceleration = config.groundAcceleration * wishSpeed * config.surfaceFriction * dt;
            return horizontal + wishDirection * Mathf.Min(addSpeed, acceleration);
        }
    }
}
