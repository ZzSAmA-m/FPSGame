using UnityEngine;

namespace GamePlay.Player.Simulation
{
    /// <summary>
    /// 不碰场景物体的运动积分。碰撞由调用方把 <paramref name="displacement"/> 应用到角色控制器后回写位置。
    /// </summary>
    public static class PlayerMotor
    {
        public static PlayerStepResult Integrate(
            ref PlayerSimState state,
            in PlayerInputFrame input,
            in PlayerMotorConfig config,
            float dt,
            out Vector3 displacement)
        {
            state.yaw += input.look.x * config.lookSensitivity;
            state.pitch = Mathf.Clamp(
                state.pitch - input.look.y * config.lookSensitivity,
                config.minPitch,
                config.maxPitch);

            if (state.grounded && state.verticalVelocity < 0f)
                state.verticalVelocity = config.groundedForce;

            state.verticalVelocity += config.gravity * dt;

            bool jumped = false;
            if (input.jump && state.grounded)
            {
                state.verticalVelocity = config.jumpSpeed;
                state.grounded = false;
                jumped = true;
            }

            Vector3 wish = new Vector3(input.move.x, 0f, input.move.y);
            if (wish.sqrMagnitude > 1f)
                wish.Normalize();

            Vector3 horizontal = Quaternion.Euler(0f, state.yaw, 0f) * wish * config.moveSpeed;
            state.velocity = new Vector3(horizontal.x, state.verticalVelocity, horizontal.z);
            displacement = state.velocity * dt;

            return new PlayerStepResult
            {
                jumped = jumped,
                fired = input.fire,
                key1 = input.key1,
                key2 = input.key2,
                key3 = input.key3
            };
        }
    }
}
