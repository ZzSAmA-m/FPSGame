// 文件：MovementContext.cs
// 作用：玩家移动上下文数据类：打包玩家移动所需的全部引用、依赖、配置与全局状态，
//       使各移动行为（Behaviours）只需接收这个上下文即可访问所需数据，减少参数传递。
using cowsins;
using UnityEngine;

/// <summary>
/// Bundles all player contexts and states, so we can only pass
/// the interfaces or states that specific behaviours need.
/// </summary>
public class MovementContext
{
    // ---------------- CORE REFS ----------------
    // 玩家 Transform。
    public Transform Transform { get; set; }
    // 玩家刚体。
    public Rigidbody Rigidbody { get; set; }
    // 玩家胶囊碰撞体。
    public CapsuleCollider Capsule { get; set; }
    // 玩家相机 Transform。
    public Transform Camera { get; set; }
    // 地面检测图层掩码。
    public LayerMask WhatIsGround { get; set; }

    // ---------------- DEPENDENCIES / CONFIG ----------------
    // 输入管理器。
    public InputManager InputManager { get; set; }
    // 移动设置。
    public PlayerMovementSettings Settings { get; set; }
    // 玩家依赖组件集合。
    public PlayerDependencies Dependencies { get; set; }

    // ---------------- GLOBAL STATE ----------------
    // 玩家是否处于斜坡上。
    public bool IsPlayerOnSlope;

    // Last ground hit info
    // 最近一次地面射线命中信息（用于斜坡检测）。
    public RaycastHit SlopeHit;

    // 是否已跳跃。
    public bool HasJumped;
    // 土狼时间计时器（离地后仍可跳跃的缓冲时间）。
    public float CoyoteTimer;
    // 土狼时间上限。
    public float CoyoteJumpTime;

    // 左侧是否有墙（用于墙跑检测）。
    public bool WallLeft;

    // 是否有足够体力奔跑。
    public bool EnoughStaminaToRun = true;
    // 是否有足够体力跳跃。
    public bool EnoughStaminaToJump = true;
}