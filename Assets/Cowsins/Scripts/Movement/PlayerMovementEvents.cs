// ----------------------------------------------------------------------
// PlayerMovementEvents.cs
// 作用：集中声明玩家移动系统对外暴露的全部运行时事件（UnityEvent）。
//       将移动、下蹲、滑铲、跳跃、冲刺、攀爬、跑墙、蹬墙、钩索等状态变化
//       抽象为事件，供 UI、音效、动画等模块订阅，实现移动逻辑与表现层解耦。
//       同时封装了安全调用函数委托（Func<bool>）的辅助方法。
// ----------------------------------------------------------------------
using System;
using UnityEngine;
using UnityEngine.Events;

namespace cowsins
{
    /// <summary>
    /// 玩家移动事件集合类。聚合移动系统各状态对应的事件与布尔判断委托，供外部模块订阅；
    /// 并提供 InvokeAllowSlide / InvokeCanWallBounce 两个封装方法，
    /// 用于在对应委托为空（未订阅）时安全地返回默认值 false。
    /// </summary>
    public class PlayerMovementEvents
    {
        // 重生事件：参数依次为重生位置、朝向，以及两个用于传递额外重生状态的布尔标志。
        public UnityEvent<Vector3, Quaternion, bool, bool> OnRespawn = new UnityEvent<Vector3, Quaternion, bool, bool>();

        // 体力耗尽时触发。
        public UnityEvent OnStaminaDepleted = new UnityEvent();
        // 玩家开始移动时触发。
        public UnityEvent OnMoving = new UnityEvent();
        // 玩家进入待机状态时触发。
        public UnityEvent OnIdle = new UnityEvent();
        // 玩家从待机状态转入移动时触发。
        public UnityEvent OnIdleToMove = new UnityEvent();
        // 玩家从移动状态转入待机时触发。
        public UnityEvent OnMovingToIdle = new UnityEvent();

        // 玩家开始下蹲时触发。
        public UnityEvent OnCrouchStart = new UnityEvent();
        // 玩家保持下蹲状态期间持续触发。
        public UnityEvent OnCrouching = new UnityEvent();
        // 玩家结束下蹲时触发。
        public UnityEvent OnCrouchStop = new UnityEvent();
        // 玩家开始滑铲时触发。
        public UnityEvent OnSlideStart = new UnityEvent();
        // 玩家结束滑铲时触发。
        public UnityEvent OnSlideEnd = new UnityEvent();
        // 判断是否允许滑铲的函数委托；为空时由 InvokeAllowSlide 按 false 处理。
        public Func<bool> AllowSlide;

        // 玩家跳跃时触发。
        public UnityEvent OnJump = new UnityEvent();
        // 玩家落地时触发。
        public UnityEvent OnLand = new UnityEvent();
        // 玩家蹬墙跳时触发。
        public UnityEvent OnWallJump = new UnityEvent();

        // 冲刺次数初始化时触发，参数为当前可用的冲刺次数。
        public UnityEvent<int> OnInitializeDash = new UnityEvent<int>();
        // 消耗一次冲刺时触发，参数为与冲刺次数相关的整型值。
        public UnityEvent<int> OnDashUsed = new UnityEvent<int>();
        // 获得新的冲刺次数时触发，参数为与冲刺次数相关的整型值。
        public UnityEvent<int> OnDashGained = new UnityEvent<int>();
        // 冲刺动作开始时触发。
        public UnityEvent OnDashStart = new UnityEvent();
        // 冲刺进行中持续触发。
        public UnityEvent OnDashing = new UnityEvent();
        // 冲刺结束时触发。
        public UnityEvent OnDashStop = new UnityEvent();

        // 开始攀爬时触发，参数为可空布尔值（用于区分攀爬方向等状态）。
        public UnityEvent<bool?> OnClimbStart = new UnityEvent<bool?>();
        // 结束攀爬时触发，参数为可空布尔值（用于区分攀爬方向等状态）。
        public UnityEvent<bool?> OnClimbStop = new UnityEvent<bool?>();

        // 开始跑墙时触发。
        public UnityEvent OnWallRunStart = new UnityEvent();
        // 结束跑墙时触发。
        public UnityEvent OnWallRunStop = new UnityEvent();

        // 判断是否允许蹬墙的函数委托；为空时由 InvokeCanWallBounce 按 false 处理。
        public Func<bool> CanWallBounce;
        // 开始蹬墙时触发。
        public UnityEvent OnWallBounceStart = new UnityEvent();

        // 钩索开始使用（发射/挂接）时触发。
        public UnityEvent OnGrappleStart = new UnityEvent();

        // 安全调用 AllowSlide 委托：若为空则返回 false，否则返回委托的实际返回值。
        public bool InvokeAllowSlide() => CowsinsUtilities.InvokeFunc(AllowSlide, false);
        // 安全调用 CanWallBounce 委托：若为空则返回 false，否则返回委托的实际返回值。
        public bool InvokeCanWallBounce() => CowsinsUtilities.InvokeFunc(CanWallBounce, false);
    }

}