/// <summary>
/// This script belongs to cowsins™ as a part of the cowsins´ FPS Engine. All rights reserved. 
/// </summary>
// =============================================================================
// WeaponController.cs
// 武器系统中枢：挂载在玩家身上的总控制器，负责武器的装配生命周期（实例化/卸载）、
// 各行为子系统的创建与调度（射击/换弹/瞄准/后坐力/散布/命中检测/重量/配件/特效/快速动作/背包），
// 并通过一系列对外 API 与事件向其它模块暴露武器状态与操作入口。
// =============================================================================
using UnityEngine;
using System.Collections.Generic;

#if UNITY_EDITOR
using UnityEditor.Presets;
#endif

namespace cowsins
{
    /// <summary>
    /// 武器系统中枢控制器：管理武器的实例化/卸载装配生命周期，
    /// 创建并调度射击、换弹、瞄准、后坐力、散布、命中检测、武器重量、配件、特效、快速动作与背包等子行为系统，
    /// 并对外提供武器引用、行为、后坐力与事件等接口供玩家其它模块调用。
    /// </summary>
    [UnityEngine.Scripting.Preserve]
    public class WeaponController : MonoBehaviour, IWeaponReferenceProvider, IWeaponBehaviourProvider, IWeaponRecoilProvider, IWeaponEventsProvider
    {
        #region VARIABLES

        // 武器控制器的可配置设置（准星、散布、后坐力、库存容量等），在 Inspector 中可编辑
        public WeaponControllerSettings settings = new WeaponControllerSettings();

        #endregion

        #region INTERNAL USE

        // 玩家依赖聚合对象，用于获取玩家各模块引用
        private PlayerDependencies playerDependencies;
        // 玩家移动状态提供者（跑步/蹲伏/地面等状态）
        private IPlayerMovementStateProvider playerMovement; // IPlayerMovementStateProvider is implemented in PlayerMovement.cs
        // 玩家移动事件提供者（用于监听蹲下/站起等事件）
        private IPlayerMovementEventsProvider playerMovementEvents; // IPlayerMovementEventsProvider is implemented in PlayerMovement.cs
        // 交互事件提供者（用于监听丢弃武器等事件）
        private IInteractEventsProvider interactEvents; // IInteractEventsProvider is implemented in InteractManager.cs
        // 玩家属性提供者（生命值、护甲等）
        private IPlayerStatsProvider statsProvider; // IPlayerStatsProvider is implemented in PlayerStats.cs
        // 玩家控制提供者（移动/视野等输入控制）
        private IPlayerControlProvider playerControl; // IPlayerControlProvider is implemented in PlayerControl.cs
        // 玩家倍率组件（重量、速度等倍率）
        private PlayerMultipliers playerMultipliers;
        // 武器状态机（管理当前武器状态）
        private WeaponStates weaponStates;
        // 玩家倍率组件引用（与 playerMultipliers 相同组件，用于不同计算场景）
        private PlayerMultipliers multipliers;

        #endregion

        #region INTERFACES
        // 武器系统事件聚合，向外界发布装枪/射击/换弹等事件
        public WeaponControllerEvents Events { get; private set; } = new WeaponControllerEvents();
        // 玩家主摄像机（从设置中读取）
        public Camera MainCamera => settings.mainCamera;
        // 当前装备武器的数据资产（Weapon_SO）
        public Weapon_SO Weapon { get; set; }
        // 当前装备武器实例的标识组件（WeaponIdentification）
        public WeaponIdentification Id { get; set; }
        // 当前武器在库存中的索引
        public int CurrentWeaponIndex { get; set; }
        // 玩家武器库存数组
        public WeaponIdentification[] Inventory { get; set; }
        // 武器系统的音频源
        public AudioSource AudioSource { get; private set; }

        // 当前是否可用近战武器
        public bool IsMeleeAvailable { get; set; }
        // 当前是否处于瞄准状态
        public bool IsAiming { get; set; }
        // 当前是否正在射击
        public bool IsShooting { get; set; }
        // 当前是否正在换弹
        public bool IsReloading { get; set; }
        // 是否启用“备用瞄准”模式（来自设置）
        public bool AlternateAiming => settings.alternateAiming;
        // 瞄准时的镜头抖动倍率（当前固定为 1）
        public float AimingCamShakeMultiplier { get; } = 1;
        // 蹲伏时的镜头抖动倍率
        public float CrouchingCamShakeMultiplier { get; set; } = 1;

        #endregion

        #region INITIALIZATION

        // 后坐力系统：处理射击后坐力的叠加与回弹
        public RecoilSystem recoilSystem { get; private set; }
        // 散布系统：处理子弹散射
        public SpreadSystem spreadSystem { get; private set; }
        // 命中检测系统：处理射线/抛射物命中判定
        public HitDetectionSystem hitDetectionSystem { get; private set; }
        // 武器重量系统：处理武器重量对移动的影响
        public WeaponWeightSystem weaponWeightSystem { get; private set; }
        // 配件系统：处理武器配件（瞄具/枪管等）的装卸
        public AttachmentsSystem attachmentsSystem { get; private set; }
        // 武器特效系统：处理枪口火焰、弹壳等视觉特效
        public WeaponEffectsSystem weaponEffectsSystem { get; private set; }
        // 武器上下文对象，供各子系统共享引用与状态
        public WeaponContext weaponContext { get; private set; }
        // 瞄准行为：处理开镜/收镜逻辑
        public AimBehaviour aimBehaviour { get; private set; }
        // 换弹行为：处理换弹流程
        public ReloadBehaviour reloadBehaviour { get; private set; }
        // 射击行为：处理射击触发与节奏
        public ShootBehaviour shootBehaviour { get; private set; }
        // 快速动作行为：处理检视等快速动作
        public QuickActionBehaviour quickActionBehaviour { get; private set; }
        // 武器库存系统：处理武器拾取/切换/丢弃与弹药
        public WeaponInventorySystem weaponInventoryBehaviour { get; private set; }
        
        // 启动入口：获取引用 → 构建武器上下文 → 初始化各行为子系统 → 触发初始化事件，
        // 加载初始武器 → 应用初始设置 → 订阅丢弃武器与蹲伏事件。
        private void Start()
        {
            GetReferences();
            weaponContext = new WeaponContext
            {
                Transform = this.transform,
                InputManager = playerDependencies.InputManager,
                Dependencies = playerDependencies,
                WeaponHolder = settings.weaponHolder,
                CoroutineRunner = this.GetComponent<MonoBehaviour>(),
                Settings = settings,
                AudioSource = AudioSource,
            };

            InitializeBehaviours();
            Events.OnInitializeWeaponSystem?.Invoke(settings.inventorySize);
            weaponInventoryBehaviour.GetInitialWeapons();

            InitialSettings();

            interactEvents.Events.OnDrop.AddListener(ReleaseCurrentWeapon);

            playerMovementEvents.Events.OnCrouchStart.AddListener(SetCrouchCamShakeMultiplier);
            playerMovementEvents.Events.OnCrouchStop.AddListener(ResetCrouchCamShakeMultiplier);
        }


        // 组件禁用时：取消订阅配件 UI 点击事件，避免悬挂引用导致问题。
        private void OnDisable()
        {
            // Unsubscribe from the method to avoid issues
            if (attachmentsSystem != null)
                UIEvents.onAttachmentUIElementClickedNewAttachment -= attachmentsSystem.AssignNewAttachment;
        }

        // 销毁时：移除对丢弃武器与蹲伏事件的监听，避免内存泄漏。
        private void OnDestroy()
        {
            if (interactEvents != null && interactEvents.Events != null)
                interactEvents.Events.OnDrop.RemoveListener(ReleaseCurrentWeapon);

            if (playerMovementEvents != null && playerMovementEvents.Events != null)
            {
                playerMovementEvents.Events.OnCrouchStart.RemoveListener(SetCrouchCamShakeMultiplier);
                playerMovementEvents.Events.OnCrouchStop.RemoveListener(ResetCrouchCamShakeMultiplier);
            }
        }

        // 获取引用：从 PlayerDependencies 拉取玩家各模块引用，获取/添加 AudioSource，
        // 并按设置配置输入管理器的武器输入模式。
        private void GetReferences()
        {
            playerDependencies = GetComponent<PlayerDependencies>();
            playerMovement = playerDependencies.PlayerMovementState;
            playerMovementEvents = playerDependencies.PlayerMovementEvents;
            interactEvents = playerDependencies.InteractEvents;
            playerControl = playerDependencies.PlayerControl;
            statsProvider = playerDependencies.PlayerStats;
            playerMultipliers = GetComponent<PlayerMultipliers>();
            weaponStates = GetComponent<WeaponStates>();
            multipliers = GetComponent<PlayerMultipliers>();

            AudioSource = GetComponent<AudioSource>();
            if (AudioSource == null) AudioSource = gameObject.AddComponent<AudioSource>();

            playerDependencies.InputManager.SetWeaponInputModes(settings);
        }

        // 初始设置：将当前武器索引重置为 0。
        private void InitialSettings()
        {
            CurrentWeaponIndex = 0;
        }

        // 初始化各行为子系统：按依赖顺序创建命中检测、散布、配件、特效、后坐力、重量、
        // 库存以及瞄准、换弹、射击、快速动作等行为对象。
        private void InitializeBehaviours()
        {
            hitDetectionSystem = new HitDetectionSystem(weaponContext, settings);
            spreadSystem = new SpreadSystem(weaponContext);
            attachmentsSystem = new AttachmentsSystem(weaponContext, settings);
            weaponEffectsSystem = new WeaponEffectsSystem(weaponContext, settings);
            recoilSystem = new RecoilSystem(weaponContext);
            weaponWeightSystem = new WeaponWeightSystem(weaponContext);
            weaponInventoryBehaviour = new WeaponInventorySystem(weaponContext, settings);

            aimBehaviour = new AimBehaviour(weaponContext);
            reloadBehaviour = new ReloadBehaviour(weaponContext);
            shootBehaviour = new ShootBehaviour(weaponContext);
            quickActionBehaviour = new QuickActionBehaviour(weaponContext);
        }
        #endregion

        // 每帧更新：刷新准星索敌检测、后坐力 Tick 与过热比例处理。
        private void Update()
        {
            HandleCrosshairEnemySpot();
            recoilSystem?.Tick();
            reloadBehaviour?.HandleHeatRatio();
        }

        #region WEAPON UNHOLSTER & SET-UP 
        /// <summary>将指定武器实例化到库存指定索引（最简重载，弹匣与备弹使用默认值）。</summary>
        /// <param name="newWeapon">要装备的武器数据资产</param>
        /// <param name="inventoryIndex">放入库存的索引位置</param>
        public void InstantiateWeapon(Weapon_SO newWeapon, int inventoryIndex) => InstantiateWeapon(newWeapon, inventoryIndex, null, null, null);

        /// <summary>将指定武器实例化到库存指定索引，并指定弹匣内剩余子弹数与备弹总数。</summary>
        /// <param name="newWeapon">要装备的武器数据资产</param>
        /// <param name="inventoryIndex">放入库存的索引位置</param>
        /// <param name="_bulletsLeftInMagazine">弹匣内剩余子弹数</param>
        /// <param name="_totalBullets">备弹总数</param>
        public void InstantiateWeapon(Weapon_SO newWeapon, int inventoryIndex, int? _bulletsLeftInMagazine, int? _totalBullets) 
            => InstantiateWeapon(newWeapon, inventoryIndex, _bulletsLeftInMagazine, _totalBullets, null);

        /// <summary>将指定武器实例化到库存指定索引，并指定弹药与配件（完整重载）。</summary>
        /// <param name="newWeapon">要装备的武器数据资产</param>
        /// <param name="inventoryIndex">放入库存的索引位置</param>
        /// <param name="_bulletsLeftInMagazine">弹匣内剩余子弹数</param>
        /// <param name="_totalBullets">备弹总数</param>
        /// <param name="attachmentsToAssign">要装备到该武器的配件列表</param>
        public void InstantiateWeapon(Weapon_SO newWeapon, int inventoryIndex, int? _bulletsLeftInMagazine, int? _totalBullets, List<AttachmentIdentifier_SO> attachmentsToAssign)
        {
            weaponInventoryBehaviour.InstantiateWeapon(newWeapon, inventoryIndex, _bulletsLeftInMagazine, _totalBullets, attachmentsToAssign);
        }

        /// <summary>丢弃当前装备的武器（按当前武器索引调用 ReleaseWeapon）。</summary>
        public void ReleaseCurrentWeapon() => ReleaseWeapon(CurrentWeaponIndex);

        /// <summary>丢弃库存中指定索引位置的武器。</summary>
        /// <param name="index">要丢弃的武器在库存中的索引</param>
        public void ReleaseWeapon(int index) => weaponInventoryBehaviour.ReleaseWeapon(index);

        #endregion

        #region MODIFIERS
   
        // 设置蹲伏镜头抖动倍率：有武器则取其蹲伏抖动倍率，否则回退为 1。
        private void SetCrouchCamShakeMultiplier()
        {
            CrouchingCamShakeMultiplier = Weapon ? Weapon.camShakeCrouchMultiplier : 1;
        }

        // 重置蹲伏镜头抖动倍率为 1（恢复默认）。
        private void ResetCrouchCamShakeMultiplier()
        {
            CrouchingCamShakeMultiplier = 1;
        }
        #endregion

        #region UI
        // 准星索敌检测：从主摄像机沿视线前方发射射线，命中 Enemy/Critical 标签时触发
        // OnEnemySpotted(true)，否则触发 OnEnemySpotted(false)；未持有武器时直接上报 false。
        private void HandleCrosshairEnemySpot()
        {
            // If we dont own a weapon yet, do not continue
            // If we dont use a crosshair stop right here
            if (Weapon == null)
            {
                Events.OnEnemySpotted?.Invoke(false);
                return;
            }

            // Detect enemies on aiming
            RaycastHit hit;

            if (Physics.Raycast(MainCamera.transform.position, MainCamera.transform.forward, out hit, Weapon.bulletRange))
            {
                if(hit.collider.CompareTag("Enemy") || hit.collider.CompareTag("Critical"))
                {
                    Events.OnEnemySpotted?.Invoke(true);
                    return;
                }
            }

            Events.OnEnemySpotted?.Invoke(false);
        }

        #endregion

        #region RECOIL

        // 当前后坐力的俯仰（Pitch）偏移量
        public float RecoilPitchOffset => recoilSystem.recoilPitchOffset;
        // 当前后坐力的偏航（Yaw）偏移量
        public float RecoilYawOffset => recoilSystem.recoilYawOffset;

        #endregion

        #region UTILS
        /// <summary>为已持有的同款武器补充弹药。</summary>
        /// <param name="amount">要补充的弹药数量</param>
        /// <returns>补充成功返回 true，否则返回 false</returns>
        public bool AddDuplicateWeaponAmmo(int amount)
        {
            return weaponInventoryBehaviour.AddDuplicateWeaponAmmo(amount);
        }

        /// <summary>尝试将武器加入库存（含当前弹匣子弹数与备弹、配件）。</summary>
        /// <param name="weapon">要加入的武器数据资产</param>
        /// <param name="currentBullets">弹匣内当前子弹数</param>
        /// <param name="totalBullets">备弹总数</param>
        /// <param name="attachments">武器配件列表</param>
        /// <returns>成功加入返回 true，否则返回 false</returns>
        public bool TryToAddWeapons(Weapon_SO weapon, int currentBullets, int totalBullets, List<AttachmentIdentifier_SO> attachments)
        {
            return weaponInventoryBehaviour.TryToAddWeapons(weapon, currentBullets, totalBullets, attachments);
        }

        /// <summary>将当前武器与指定武器交换，返回被换下的旧武器数据（武器资产、弹匣子弹数、备弹数）。</summary>
        /// <param name="weapon">要换入的武器数据资产</param>
        /// <param name="currentBullets">换入武器的弹匣子弹数</param>
        /// <param name="totalBullets">换入武器的备弹数</param>
        /// <param name="attachments">换入武器的配件列表</param>
        /// <returns>被换下的旧武器（武器资产、弹匣子弹数、备弹数）三元组</returns>
        public (Weapon_SO, int, int) SwapWeapons(Weapon_SO weapon, int currentBullets, int totalBullets, List<AttachmentIdentifier_SO> attachments)
        {
            return weaponInventoryBehaviour.SwapWeapons(weapon, currentBullets, totalBullets, attachments);
        }

        #endregion
    }
}