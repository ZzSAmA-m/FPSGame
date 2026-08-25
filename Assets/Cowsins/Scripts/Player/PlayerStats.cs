// ============================================================================
// PlayerStats.cs —— 玩家属性系统（生命/护盾/坠落伤害/自动回复）。
// 实现 IDamageable、IPlayerStatsProvider 等接口，集中管理玩家生命值与护盾值
// 的扣除、治疗、死亡与复活，处理从高处坠落造成的伤害，以及可选的自动回复
// 逻辑；同时对外暴露事件与访问器供 UI、存档等系统读取。
// ============================================================================
/// <summary>
/// This script belongs to cowsins™ as a part of the cowsins´ FPS Engine. All rights reserved. 
/// </summary>
using UnityEngine;
using UnityEngine.Events;
using System;

namespace cowsins
{
    /// <summary>
    /// 玩家属性组件：管理生命/护盾、坠落伤害与自动回复，并实现可被伤害的接口。
    /// </summary>
    [System.Serializable]
    public class PlayerStats : MonoBehaviour, IDamageable, IPlayerStatsProvider, IFallHeightProvider, IPlayerStatsEventsProvider
    {
        /// <summary>
        /// 玩家属性相关的 Unity 事件集合（死亡、受伤、治疗）。
        /// </summary>
        [System.Serializable]
        public class UserStatsEvents
        {
            public UnityEvent OnDeath, OnDamage, OnHeal; // 死亡、受伤、治疗事件
        }

        #region variables

        [ReadOnly] public float health, shield; // 当前生命值与护盾值（Inspector 只读）

        public float maxHealth, maxShield; // 最大生命值与最大护盾值

        [Tooltip("If enabled, makes the rigidbody kinematic on death. Player loses control either way."), SerializeField] private bool freezePlayerOnDeath; // 死亡时是否将刚体设为运动学

        [Tooltip("Turn on to apply damage on falling from great height"), SerializeField] private bool takesFallDamage; // 是否启用坠落伤害

        [Tooltip("Minimum height ( in units ) the player has to fall from in order to take damage"), SerializeField, Min(1)] private float minimumHeightDifferenceToApplyDamage; // 触发坠落伤害的最小高度差

        [Tooltip("How the damage will increase on landing if the damage on fall is going to be applied"), SerializeField] private float fallDamageMultiplier; // 坠落伤害倍率

        [SerializeField] private bool enableAutoHeal = false; // 是否启用自动回复

        [SerializeField, Min(0)] private float healRate; // 自动回复的间隔时间（秒）

        [SerializeField] private float healAmount; // 每次自动回复的生命值

        [SerializeField] private bool restartAutoHealAfterBeingDamaged = false; // 受伤后是否重置自动回复计时

        [SerializeField] private float restartAutoHealTime; // 受伤后延迟多久重新开始自动回复


        // Internal use

        private float? currentFallHeight = null; // 当前记录的坠落起始高度（可空类型）

        private bool isDead; // 是否已死亡

        private PlayerDependencies playerDependencies; // 玩家依赖集合
        // 玩家移动状态接口引用，用于读取移动状态（是否落地/攀爬/冲刺等）
        private IPlayerMovementStateProvider player; // IPlayerMovementStateProvider is implemented in PlayerMovement.cs
        // 玩家移动事件接口引用，用于订阅移动相关事件（如复活）
        private IPlayerMovementEventsProvider playerEvents; // IPlayerMovementEventsProvider is implemented in PlayerMovement.cs
        // 玩家倍率接口引用，用于获取治疗倍率等数值
        private IPlayerMultipliers multipliers; // IPlayerMultipliers is implemented in PlayerMultipliers.cs

        public UserStatsEvents userEvents; // 对外暴露的用户属性事件

        // Accessors
        public bool IsDead => isDead; // 是否死亡
        public float Health => health; // 当前生命值
        public float MaxHealth => maxHealth; // 最大生命值
        public float Shield => shield; // 当前护盾值
        public float MaxShield => maxShield; // 最大护盾值
        public float? CurrentFallHeight => currentFallHeight; // 当前坠落起始高度
        public bool FreezePlayerOnDeath => freezePlayerOnDeath; // 死亡时是否冻结玩家

        public bool TakesFallDamage => takesFallDamage; // 是否启用坠落伤害
        public bool EnableAutoHeal => enableAutoHeal; // 是否启用自动回复
        public bool RestartAutoHealAfterBeingDamaged => restartAutoHealAfterBeingDamaged; // 受伤后是否重置自动回复

        public PlayerStatsEvents Events { get; } = new PlayerStatsEvents(); // 属性事件集合（供 UI 订阅）

        public event Action OnDie; // 死亡回调事件
        public void AddOnDieListener(Action listener) => OnDie += listener; // 添加死亡监听
        public void RemoveOnDieListener(Action listener) => OnDie -= listener; // 移除死亡监听

        #endregion

        // 初始化：获取引用，将生命/护盾设为最大值，并按需启动自动回复。
        private void Start()
        {
            GetAllReferences();

            // Apply basic settings 
            health = maxHealth;
            shield = maxShield;
            Events.OnInitializeHealth?.Invoke(this); // 广播初始化生命值事件

            if (enableAutoHeal)
                StartAutoHeal();
        }

        // 每帧更新：处理坠落伤害（未启用坠落伤害、攀爬中或已死亡时跳过）。
        private void Update()
        {
            // Manage fall damage
            if (!takesFallDamage || player.IsClimbing || IsDead) return;
            ManageFallDamage();
        }
        /// <summary>
        /// Our Player Stats is IDamageable, which means it can be damaged
        /// If so, call this method to damage the player
        /// 对玩家造成伤害：优先扣除护盾，剩余伤害扣除生命；处理死亡与自动回复重置。
        /// </summary>
        /// <param name="_damage">伤害值（会被取绝对值）</param>
        /// <param name="isHeadshot">是否为爆头伤害（当前逻辑未直接使用）</param>
        public void Damage(float _damage, bool isHeadshot)
        {
            if (player == null)
            {
                player = playerDependencies.PlayerMovementState;
            }
            // Early return if player is dashing with damage protection
            // 若玩家正在冲刺且拥有冲刺减伤保护，则直接返回不受伤害
            if (player != null && player.IsDashing && player.DamageProtectionWhileDashing)
                return;

            // Ensure damage is a positive value
            float damage = Mathf.Abs(_damage); // 确保伤害值为正

            // Trigger damage event
            userEvents.OnDamage.Invoke(); // 触发受伤事件

            // Apply damage to shield first
            if (damage <= shield)
            {
                shield -= damage; // 伤害全部由护盾吸收
            }
            else
            {
                // Apply remaining damage to health
                damage -= shield; // 扣除护盾后剩余伤害
                shield = 0;
                health -= damage; // 剩余伤害扣除生命
            }

            // Notify UI about the health change
            Events.OnHealthChanged?.Invoke(health, shield, true); // 通知 UI 生命/护盾变化（受伤）

            // 生命耗尽且未死亡时触发死亡
            if (health <= 0 && !IsDead) Die(); // Die in case we run out of health   

            // Handle auto-healing
            // 若开启自动回复且受伤后需重置，则取消原有自动回复并延迟重新启动
            if (enableAutoHeal && restartAutoHealAfterBeingDamaged)
            {
                CancelInvoke(nameof(AutoHeal));
                InvokeRepeating(nameof(AutoHeal), restartAutoHealTime, healRate);
            }
        }


        /// <summary>
        /// 治疗玩家：先回复生命，溢出部分再回复护盾（受治疗倍率影响）。
        /// </summary>
        /// <param name="healAmount">治疗量（会被取绝对值并乘以治疗倍率）</param>
        public void Heal(float healAmount)
        {
            float adjustedHealAmount = Mathf.Abs(healAmount * multipliers.HealMultiplier.Value); // 应用治疗倍率并取绝对值

            // If we are at full health and shield, do not heal
            // 若生命与护盾均已满，则无需治疗
            if ((maxShield > 0 && shield == maxShield) || (maxShield == 0 && health == maxHealth))
            {
                return;
            }

            // Trigger heal event
            userEvents.OnHeal.Invoke(); // 触发治疗事件

            // Calculate effective healing for health
            float effectiveHealForHealth = Mathf.Min(adjustedHealAmount, maxHealth - health); // 生命部分实际回复量
            health += effectiveHealForHealth;

            // Calculate remaining heal amount after health is full
            float remainingHeal = adjustedHealAmount - effectiveHealForHealth; // 生命回满后的剩余治疗量

            // Apply remaining heal to shield if applicable
            if (remainingHeal > 0 && maxShield > 0)
            {
                shield = Mathf.Min(shield + remainingHeal, maxShield); // 剩余治疗量加到护盾
            }

            // Notify UI about the health change
            Events.OnHealthChanged?.Invoke(health, shield, false); // 通知 UI 生命/护盾变化（治疗）
        }

        /// <summary>
        /// 将生命与护盾回满。
        /// </summary>
        public void HealFull()
        {
            if (IsFullyHealed()) return; // 已满则不处理

            health = maxHealth;
            shield = maxShield;
            Events.OnHealthChanged?.Invoke(health, shield, false);
        }

        /// <summary>
        /// 判断玩家是否已完全回复（生命满，且若有护盾则护盾也满）。
        /// </summary>
        /// <returns>已完全回复返回 true，否则 false</returns>
        public bool IsFullyHealed()
        {
            if (MaxShield > 0)
            {
                return shield >= MaxShield && health >= MaxHealth;
            }
            else
            {
                return health >= MaxHealth;
            }
        }


        /// <summary>
        /// Perform any actions On death
        /// 执行死亡相关动作：标记死亡、触发死亡回调与自定义事件。
        /// </summary>
        private void Die()
        {
            isDead = true;
            OnDie?.Invoke();
            userEvents.OnDeath.Invoke(); // Invoke a custom event
        }
        /// <summary>
        /// Basically find everything the script needs to work
        /// 获取脚本工作所需的全部引用（依赖集合、移动状态、移动事件、倍率）。
        /// </summary>
        private void GetAllReferences()
        {
            playerDependencies = GetComponent<PlayerDependencies>();
            player = playerDependencies.PlayerMovementState;
            playerEvents = playerDependencies.PlayerMovementEvents;
            multipliers = playerDependencies.PlayerMultipliers;
        }
        /// <summary>
        /// While airborne, if you exceed a certain time, damage on fall will be applied
        /// 管理坠落伤害：记录空中的最高起始高度，落地时根据高度差判断是否造成伤害。
        /// </summary>
        private void ManageFallDamage()
        {
            // Grab current player height
            // 在空中且高度升高（或尚未记录）时，更新坠落起始高度
            if (!player.Grounded && transform.position.y > currentFallHeight || !player.Grounded && currentFallHeight == null) currentFallHeight = transform.position.y;

            // Check if we landed, as well if our current height is lower than the original height. If so, check if we should apply damage
            // 落地且当前高度低于记录的起始高度时，判断是否应造成坠落伤害
            if (player.Grounded && currentFallHeight != null && transform.position.y < currentFallHeight)
            {
                float currentHeight = transform.position.y;

                // Transform nullable variable into a non nullable float for later operations
                float noNullHeight = currentFallHeight ?? default(float); // 将可空类型转为普通 float

                float heightDifference = noNullHeight - currentHeight; // 计算高度差

                // If the height difference is enough, apply damage
                // 高度差足够大时，按倍率造成坠落伤害
                if (heightDifference > minimumHeightDifferenceToApplyDamage) Damage(heightDifference * fallDamageMultiplier, false);

                // Reset height
                currentFallHeight = null; // 重置记录的高度
            }
        }

        // 设置坠落起始高度（供外部系统调用）。
        public void SetFallHeight(float newFallHeight) => currentFallHeight = newFallHeight;

        // 启动自动回复：按 healRate 间隔重复调用 AutoHeal。
        private void StartAutoHeal()
        {
            InvokeRepeating(nameof(AutoHeal), healRate, healRate);
        }

        // 自动回复逻辑：生命与护盾未满时执行一次治疗。
        private void AutoHeal()
        {
            if (shield >= maxShield && health >= maxHealth) return;

            Heal(healAmount);
        }

        /// <summary>
        /// 复活玩家：清除死亡标记、回满生命/护盾，并广播复活事件。
        /// </summary>
        /// <param name="respawnPosition">重生位置</param>
        public void Respawn(Vector3 respawnPosition)
        {
            isDead = false;
            HealFull();
            playerEvents.Events.OnRespawn?.Invoke(respawnPosition, player.Orientation.Rotation, true, true);
        }

        // This method is used to set the Player Health values when the game loads, only if the Save & Load Add-On is available.
        // 在游戏加载时覆盖玩家生命/护盾值（仅在安装存档扩展时使用）。
        public void OverrideHealth(float health, float maxHealth, float shield, float maxShield)
        {
            this.health = health;
            this.maxHealth = maxHealth;
            this.shield = shield;
            this.maxShield = maxShield;
        }
    }
}

