// ============================================================
// DashBehaviour（冲刺行为）
// 负责玩家的冲刺：根据冲刺模式计算方向并施加冲量，管理冲刺次数与
// 冷却回复（协程），并清空垂直速度以保持水平冲刺。
// 支持无限冲刺或有限次数（可冷却恢复）两种模式。
// ============================================================
using cowsins;
using UnityEngine;
using System.Collections;

/// <summary>处理玩家冲刺：方向计算、冲量施加、次数管理与冷却回复。</summary>
public class DashBehaviour 
{
    // 移动上下文：聚合刚体、依赖与设置等共享数据。
    private MovementContext context;
    // 玩家刚体，冲刺时施加冲量并读写速度。
    private Rigidbody rb;
    // 玩家移动状态提供者：读写冲刺标记并暴露朝向。
    private IPlayerMovementStateProvider playerMovement;
    // 移动事件提供者：派发冲刺开始/使用/恢复等事件。
    private IPlayerMovementEventsProvider playerEvents;
    // 玩家控制提供者：判断当前是否可控制。
    private IPlayerControlProvider playerControl;

    // 玩家朝向：提供 Forward 向量用于“始终向前”的冲刺模式。
    private PlayerOrientation orientation => playerMovement.Orientation;

    // 移动设置：冲刺力度、次数、冷却、模式等参数来源。
    private PlayerMovementSettings playerSettings;
    // 当前剩余冲刺次数（公开供 UI 等读取）。
    public int currentDashes;

    // 本次冲刺的输入方向（用于基于输入的冲刺模式）。
    private Vector2 input;
    // 相机 Transform，用于基于相机的冲刺方向。
    private Transform playerCam;

    // 用于启动协程的 MonoBehaviour（冲刺冷却回复计时）。
    private MonoBehaviour CoroutineRunner;

    /// <summary>构造函数：从移动上下文缓存依赖引用，并在非无限冲刺时初始化冲刺次数。</summary>
    /// <param name="context">聚合刚体、依赖与设置的移动上下文。</param>
    public DashBehaviour(MovementContext context)
    {
        this.context = context;
        this.rb = context.Rigidbody;
        this.playerMovement = context.Dependencies.PlayerMovementState;
        this.playerEvents = context.Dependencies.PlayerMovementEvents;
        this.playerControl = context.Dependencies.PlayerControl;  
        this.CoroutineRunner = context.Transform.GetComponent<MonoBehaviour>();

        this.playerSettings = context.Settings;
        this.playerCam = context.Camera;

        if (this.playerSettings.canDash && !this.playerSettings.infiniteDashes)
        {
            playerEvents.Events.OnInitializeDash?.Invoke(this.playerSettings.amountOfDashes);
            currentDashes = this.playerSettings.amountOfDashes;
        }
    }
    /// <summary>进入冲刺：标记冲刺状态、消耗次数并启动冷却回复，播放音效。</summary>
    /// <param name="input">本次冲刺的输入方向。</param>
    public void Enter(Vector2 input)
    {
        this.input = input;

        playerMovement.IsDashing = true;
        rb.useGravity = true;

        // 非无限冲刺时消耗一次次数并启动冷却回复。
        if (!playerSettings.infiniteDashes)
        {
            currentDashes--;
            playerEvents.Events.OnDashUsed?.Invoke(currentDashes);
            CallRegainDash();
        }

        playerEvents.Events.OnDashStart?.Invoke();
        SoundManager.Instance.PlaySound(playerSettings.sounds.dashSFX, 0, 0, false);
    }
    /// <summary>每帧施加冲刺力并清空垂直速度，保持水平冲刺。</summary>
    public void Tick() 
    {
        Vector3 dir = GetProperDirection();
        //player.HandleStairs(dir);
        // 沿冲刺方向施加冲量。
        rb.AddForce(dir * playerSettings.dashForce * Time.deltaTime, ForceMode.Impulse);

        // Remove not wanted Y velocity after slope
        // 清空垂直速度，保持纯水平冲刺。
        Vector3 vel = rb.linearVelocity;
        vel.y = 0;
        rb.linearVelocity = vel;

        playerEvents.Events.OnDashing?.Invoke(); 
    }

    /// <summary>退出冲刺：取消标记、恢复重力并派发结束事件。</summary>
    public void Exit()
    {
        playerMovement.IsDashing = false;
        rb.useGravity = true;
        playerSettings.events.OnEndDash?.Invoke();
    }

    /// <summary>判断是否可冲刺（可控、允许冲刺且次数足够）。</summary>
    public bool CanExecute()
    {
        return playerControl.IsControllable && playerSettings.canDash && (playerSettings.infiniteDashes || currentDashes > 0 && !playerSettings.infiniteDashes);
    }

    // 根据冲刺模式确定冲刺方向：始终向前、相机朝向或基于输入。
    private Vector3 GetProperDirection()
    {
        Vector3 direction = Vector3.zero;
        switch (playerSettings.dashMethod)
        {
            case PlayerMovementSettings.DashMethod.ForwardAlways:
                direction = orientation.Forward;
                break;
            case PlayerMovementSettings.DashMethod.Free:
                direction = playerCam.forward;
                break;
            case PlayerMovementSettings.DashMethod.InputBased:
                direction = (input.x == 0 && input.y == 0) ? orientation.Forward : CameraBasedInput();
                break;
        }
        return direction;
    }


    // 基于相机朝向与输入向量计算水平冲刺方向。
    private Vector3 CameraBasedInput()
    {
        Vector3 forward = playerCam.transform.forward;
        Vector3 right = playerCam.transform.right;

        forward.y = 0f;
        right.y = 0f;
        forward.Normalize();
        right.Normalize();

        Vector3 dir = forward * input.y + right * input.x;

        return dir;
    }
    /// <summary>启动冲刺冷却协程，延迟后回复一次冲刺。</summary>
    public void CallRegainDash() => CoroutineRunner.StartCoroutine(RegainDashAfterDelay(playerSettings.dashCooldown));

    // 协程：等待冷却时间后回复一次冲刺次数。
    private IEnumerator RegainDashAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        RegainDash();
    }

    // 增加一次冲刺次数并派发事件。
    private void RegainDash()
    {
        // Gain a dash
        currentDashes += 1;
        playerEvents.Events.OnDashGained?.Invoke(currentDashes);
    }

    /// <summary>将冲刺次数重置为初始值。</summary>
    public void ResetDashes()
    {
        currentDashes = playerSettings.amountOfDashes;
    }
}
