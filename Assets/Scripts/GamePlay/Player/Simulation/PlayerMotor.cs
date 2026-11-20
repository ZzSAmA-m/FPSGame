using UnityEngine;

namespace GamePlay.Player.Simulation
{
    /// <summary>
    /// 不碰场景物体的视角与竖直积分。水平速度、位移和碰撞由移动控制器处理。
    /// </summary>
    public static class PlayerMotor
    {
        public static PlayerStepResult Integrate(
            ref PlayerSimState state,
            in PlayerInputFrame input,
            in PlayerMotorConfig config,
            float dt)
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
