// 文件：IPlayerMovementProvider.cs
// 作用：定义玩家移动状态与事件的两个接口：IPlayerMovementStateProvider 暴露移动相关状态/参数，
//       IPlayerMovementEventsProvider 暴露移动事件。PlayerMovement 实现这些接口，
//       供 PlayerDependencies 及其它模块以接口形式解耦访问。
using System;
using UnityEngine;
using UnityEngine.Events;

namespace cowsins
{
    /// <summary>
    /// 玩家移动状态提供者接口：暴露方向、速度、是否着地/蹲伏/滑铲/攀爬/墙跑/冲刺等状态与相关参数。
    /// </summary>
    // Implemented by PlayerMovement and required by PlayerDependencies
    public interface IPlayerMovementStateProvider
    {
        // 玩家朝向（位置 + 旋转）。
        PlayerOrientation Orientation { get; }
        // 是否处于空闲（水平速度极小）。
        bool IsIdle { get; }
        // 当前速度。
        float CurrentSpeed { get; set; }
        // 奔跑速度。
        float RunSpeed { get; }
        // 走路速度。
        float WalkSpeed { get; }
        // 蹲伏移动速度。
        float CrouchSpeed { get; }
        // 是否着地。
        bool Grounded { get; set; }
        // 是否蹲伏。
        bool IsCrouching { get; set; }
        // 是否滑铲。
        bool IsSliding { get; set; }
        // 是否攀爬。
        bool IsClimbing { get; set; }
        // 是否墙跑。
        bool IsWallRunning { get; set; }
        // 是否冲刺。
        bool IsDashing { get; set; }
        // 冲刺期间是否可射击。
        bool CanShootWhileDashing { get; }
        // 冲刺期间是否有伤害保护。
        bool DamageProtectionWhileDashing { get; }
        // 正常视野角度。
        float NormalFOV { get; }
        // 墙跑视野角度。
        float WallRunningFOV { get; }
        // 奔跑视野角度。
        float RunningFOV { get; }
        // 视野渐变增量。
        float FadeFOVAmount { get; }
    }

    /// <summary>
    /// 玩家移动事件提供者接口：暴露玩家移动事件集合。
    /// </summary>
    public interface IPlayerMovementEventsProvider
    {
        // 玩家移动事件集合。
        PlayerMovementEvents Events { get; }
    }
}