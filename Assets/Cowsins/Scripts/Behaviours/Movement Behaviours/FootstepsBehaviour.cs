// ============================================================
// FootstepsBehaviour（脚步声行为）
// 负责根据玩家移动播放脚步声：按移动速度推进计时器，检测脚下/墙面
// 的地面层，并从对应表面音效集中随机播放脚步声，且拥有独立音源。
// 移动、冲刺时触发，静止或滑铲时不播放。
// ============================================================
using cowsins;
using UnityEngine;

/// <summary>按移动状态检测地面并随机播放对应表面材质的脚步声。</summary>
public class FootstepsBehaviour
{
    // 移动上下文：提供刚体、Transform、地面掩码等。
    private MovementContext movementContext;
    // 玩家刚体（通过上下文延迟获取）。
    private Rigidbody rb => movementContext.Rigidbody;
    // 玩家移动状态提供者：暴露接地、墙跑、滑铲等状态。
    private IPlayerMovementStateProvider playerMovement;
    // 移动事件提供者：订阅墙跑开始等事件。
    private IPlayerMovementEventsProvider playerEvents;
    // 玩家朝向：提供 Right 向量用于墙跑脚步检测方向。
    private PlayerOrientation orientation => playerMovement?.Orientation;

    // 脚步计时器，归零时播放一步。
    private float stepTimer;
    // 移动设置：脚步速度、音量、表面音效集等参数来源。
    private PlayerMovementSettings playerSettings;
    // 专用于脚步声的独立音源，避免与武器音效互相干扰。
    private AudioSource audioSource;

    /// <summary>构造函数：缓存依赖引用，准备/复用独立音源，订阅墙跑事件并缓存层索引。</summary>
    /// <param name="context">聚合刚体、依赖与设置的移动上下文。</param>
    public FootstepsBehaviour(MovementContext context)
    {
        playerMovement = context.Dependencies.PlayerMovementState;
        playerEvents = context.Dependencies.PlayerMovementEvents;
        this.playerSettings = context.Settings;
        
        // Ensure footsteps have their own dedicated AudioSource to prevent interference with weapons
        var sources = context.Transform.GetComponents<AudioSource>();
        AudioSource mainSource = context.Transform.GetComponent<AudioSource>();

        if (sources.Length > 1) this.audioSource = sources[1]; 
        else 
        {
            this.audioSource = context.Transform.gameObject.AddComponent<AudioSource>();
            // Copy basic settings from the main source so it sounds consistent
            if (mainSource != null)
            {
                this.audioSource.spatialBlend = mainSource.spatialBlend;
                this.audioSource.outputAudioMixerGroup = mainSource.outputAudioMixerGroup;
            }
        }

        movementContext = context;
        playerEvents.Events.OnWallRunStart.AddListener(ResetFootsteps);

        // Cache layer indices for better performance
        CacheLayerIndices();
    }

    /// <summary>释放资源：移除墙跑开始事件的监听。</summary>
    public void Dispose()
    {
        playerEvents.Events.OnWallRunStart.RemoveListener(ResetFootsteps);
    }

    // 缓存各表面音效对应的层索引，避免每步重复按名称查找。
    private void CacheLayerIndices()
    {
        foreach (var entry in playerSettings.footstepSounds.surfaceSounds)
        {
            if (entry.cachedLayerIndex == -1)
                entry.cachedLayerIndex = LayerMask.NameToLayer(entry.layerName);
        }
    }

    /// <summary>判断当前是否应播放脚步（接地或墙跑、非静止、非滑铲）。</summary>
    public bool CanExecute()
    {
        if (!playerMovement.Grounded && !playerMovement.IsWallRunning || playerMovement.IsIdle || playerMovement.IsSliding)
        {
            stepTimer = 1 - playerSettings.footstepSpeed;
            return false;
        }
        return true;
    }

    /// <summary>推进脚步计时并按需检测地面播放随机脚步音。</summary>
    public void FootSteps()
    {
        if (!CanExecute()) return;

        // 按移动速度推进计时，速度越快脚步越密集。
        stepTimer -= Time.deltaTime * playerMovement.CurrentSpeed / 15;

        if (stepTimer <= 0)
        {
            stepTimer = 1 - playerSettings.footstepSpeed;
            audioSource.pitch = UnityEngine.Random.Range(.7f, 1.3f);

            // 地面时向下检测，墙跑时朝墙面一侧检测。
            Vector3 footstepCheckDirection = !playerMovement.IsWallRunning ? Vector3.down :
                (movementContext.WallLeft ? -orientation.Right : orientation.Right) * 2;

            // 命中地面层时，按该层的表面音效播放脚步。
            if (Physics.Raycast(movementContext.Transform.position, footstepCheckDirection, out RaycastHit hit, 2.5f, movementContext.WhatIsGround))
            {
                PlayFootstepSound(hit.transform.gameObject.layer);
            }
        }
    }

    // 根据层索引取对应音效集并随机播放一步。
    private void PlayFootstepSound(int layer)
    {
        AudioClip[] sounds = playerSettings.footstepSounds.GetSoundsForLayer(layer);

        if (sounds.Length > 0)
        {
            int randomIndex = UnityEngine.Random.Range(0, sounds.Length);
            audioSource.PlayOneShot(sounds[randomIndex], playerSettings.footstepVolume);
        }
    }

    /// <summary>重置脚步计时器（如墙跑开始时立即触发一步）。</summary>
    public void ResetFootsteps()
    {
        stepTimer = 0;
    }
}