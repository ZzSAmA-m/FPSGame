/// <summary>
/// This script belongs to cowsins� as a part of the cowsins� FPS Engine. All rights reserved. 
/// </summary>

// =============================================================================
// Weapon_SO.cs
// 武器数据资产（ScriptableObject）：定义一把武器的全部可配置属性——射击方式、弹药与换弹、
// 散布与后坐力、伤害与穿透、瞄准、音效、视觉特效与粒子、相机反馈、投射物与近战参数等；
// 由 WeaponController 与各武器行为子系统在运行时读取，并可在编辑器自定义 Inspector 中配置。
// =============================================================================
using UnityEngine;
using System.Collections.Generic;
using System;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Presets;
#endif

#if INVENTORY_PRO_ADD_ON
using cowsins.Inventory;
#endif

namespace cowsins
{
    #region enum
    // 射击方式枚举：决定武器开火时采用即时命中、投射物、近战还是自定义逻辑
    /// <summary>
    /// SHOOTING STYLE ENUMERATOR
    /// </summary>
    public enum ShootStyle
    {
        // 即时命中（Hitscan）：开火瞬间按射线判定命中
        Hitscan,
        // 投射物（Projectile）：生成在场景中飞行的子弹
        Projectile,
        // 近战（Melee）：刀、剑等近距离攻击
        Melee,
        // 自定义（Custom）：由用户自定义射击逻辑
        Custom
    };

    /// <summary>
    /// 射击输入方式枚举：定义“按下/按住”如何触发开火。
    /// </summary>
    public enum ShootingMethod
    {
        // 按下即开火（按开火节奏触发）
        Press,
        // 按住持续开火
        PressAndHold,
        // 按住后松开才开火
        HoldAndRelease,
        // 按住直到进度条充满才开火
        HoldUntilReady
    };

    /// <summary>
    /// 换弹方式枚举：默认换弹 / 过热机制。
    /// </summary>
    public enum ReloadingStyle
    {
        // 默认换弹（消耗弹匣）
        defaultReload,
        // 过热机制（以热量代替弹匣）
        Overheat
    };

    #endregion

    #region others
    /// <summary>音效集合：保存武器各动作的音效片段（射击音效数组 + 收枪/掏枪/换弹/空仓换弹/空仓射击）。</summary>
    [System.Serializable]
    public class AudioClips
    {
        // 射击音效数组（每次射击随机选取其一播放）
        public AudioClip[] shooting; 
        // 收枪、掏枪、换弹、空仓换弹、空仓射击音效
        public AudioClip holster, unholster, reload, emptyMagReload, emptyMagShoot;
    }
    /// <summary>弹孔/命中特效按图层（Layer）映射的条目。</summary>
    [System.Serializable]
    public class BulletHoleImpactEntry
    {
        // 图层名称
        public string layerName;
        // 该图层对应的弹孔/命中特效预制体
        public GameObject bulletHoleImpact;
        // 缓存的图层索引（运行时使用，避免每帧按名称查找）
        [HideInInspector] public int cachedLayerIndex = -1;
    }

    /// <summary>弹孔特效集合：默认弹孔 + 按图层区分的弹孔条目列表。</summary>
    [System.Serializable]
    public class BulletHoleImpact
    {
        // 默认弹孔/命中特效（无特定图层匹配时使用）
        public GameObject defaultImpact;
        // 按图层区分的弹孔特效条目列表
        public List<BulletHoleImpactEntry> bulletHoleImpact = new List<BulletHoleImpactEntry>();
    }


    /// <summary>移动过渡速度覆盖：允许为单把武器覆盖全局的移动动画过渡速度。</summary>
    [System.Serializable]
    public class LocomotionTransitionOverrides
    {
        // 是否启用本武器的移动过渡速度覆盖
        public bool overrideTransitions;

        // 待机→走路 过渡速度
        [Range(0.1f, 10f)] public float idleToWalkSpeed = 2f;

        // 走路→待机 过渡速度
        [Range(0.1f, 10f)] public float walkToIdleSpeed = 2f;

        // 走路→跑步 过渡速度
        [Range(0.1f, 10f)] public float walkToRunSpeed = 2f;

        // 跑步→走路 过渡速度
        [Range(0.1f, 10f)] public float runToWalkSpeed = 2f;
    }

    #endregion

    #region weaponScriptableObject
    /// <summary>
    /// 武器数据资产：定义一把武器的全部可配置属性（射击/弹药/散布后坐力/伤害穿透/瞄准/
    /// 音效/视觉粒子/相机反馈/投射物/近战等），供 WeaponController 与各行为子系统在运行时读取。
    /// </summary>
    [CreateAssetMenu(fileName = "NewWeapon", menuName = "COWSINS/New Weapon", order = 1)]
    public class Weapon_SO : Item_SO
    {
        // ── 基础设置 ─────────────────────────────────────────────
        // 武器预制体（装备时实例化到屏幕上的第一人称武器模型）
        [Tooltip("Attach your weapon prefab here. This weapon prefab will be instantiated on your screen when you equip this weapon.")] public WeaponIdentification weaponObject;

        // 射击方式（即时命中/投射物/近战/自定义）
        [Tooltip("Type of shooting. Hitscan = Instant hit on shooting." +
            "Projectile = spawn a bullet that travels through the world." +
            "Melee: Close range weapons such as swords or knives.")]
        public ShootStyle shootStyle;

        // 射击输入方式（按下/按住/松开发射/蓄力发射）
        [Tooltip("Defines how Input is processed to shoot.")]
        public ShootingMethod shootMethod;

        // 蓄力进度增长速度（仅 HoldUntilReady/HoldAndRelease 使用，到达 100% 才可射击）
        [Tooltip("In order to shoot, you need to have a 100% progress value. Define how fast you want to reach 100% here.")] public float holdProgressSpeed;

        // ── 换弹与弹药类型 ───────────────────────────────────────
        // 换弹方式（默认换弹/过热）
        [Tooltip("The way the weapon reloads.")] public ReloadingStyle reloadStyle;
        // 是否允许任意动作（射击/切换/丢弃）打断换弹过程
        [Tooltip("If true, any action (shoot, switch, drop) will cancel the reload process")] public bool allowCancelReload;

        // 武器使用的子弹类型（用于子弹类型识别/拾取对应弹药）
        [Tooltip("Type of ammo weapon will use.")] public BulletTypeIdentifier_SO bulletTypeIdentifier;

        // ── 投射物（Projectile）─────────────────────────────────
        // 投射物预制体（子弹物体）
        [Tooltip("Your bullet objects")] public GameObject projectile;

        // 子弹是否受重力影响（为 true 时按抛物线飞行）
        [Tooltip(" While true, the bullet will draw a parabola. ")] public bool projectileUsesGravity;

        // 按下射击到实例化投射物的延迟（0 表示立即射击）
        [Tooltip("time to instantiate the projectile sicne you press shoot. Keep 0 if you want shooting to be instantly"), Min(0)] public float shootDelay;

        // 子弹存活时间（生命周期，到时间自动销毁）
        [Tooltip("Lifetime of the bullet")] public float bulletDuration;

        // 子弹飞行速度
        [Tooltip("Bullet velocity")] public float speed;

        // 命中目标时是否爆炸（爆炸伤害随命中点与目标距离变化）
        [Tooltip("Does it explode when hits a target? If so, damage will depend on the distance between the target and the hit point")] public bool explosionOnHit;

        // 是否允许伤害自己
        [Tooltip("Toggles the capacity of hurting youtself")] public bool hurtsPlayer;

        // 爆炸最大作用半径
        [Tooltip("Max reach of the explosion")] public float explosionRadius;

        // 爆炸对刚体施加的力
        [Min(0), Tooltip("Force applied to rigidbodies on explosion")] public float explosionForce;

        // 爆炸视觉特效（VFX）
        [Tooltip("VFX on explosion")] public GameObject explosionVFX;

        // ── 射击与弹药 ───────────────────────────────────────────
        // 是否连续射击（自定义射击模式专用）
        public bool continuousFire;

        // 射击间隔（每发之间的时间，决定射速）
        [Tooltip("Time between each shot"), Min(.01f)] public float fireRate;

        // 换弹所需时间
        [Tooltip("Time you will have to wait for the weapon to reload")] public float reloadTime;
        // 空仓换弹所需时间（弹匣为空时）
        [Tooltip("Time you will have to wait for the weapon to reload when the magazine is empty")] public float emptyReloadTime = 1f;

        // 是否自定义掏枪时间（为 false 时使用动画长度）
        [Tooltip("If true, you can define a custom unholster time. If false, it will use the animation length")] public bool overrideUnholsterTime;
        // 掏枪（unholster）所需时间
        [Tooltip("Time it takes to unholster the weapon")] public float unholsterTime;

        // 过热冷却速度（过热换弹模式专用）
        public float coolSpeed;

        // 过热后冷却到多少百分比即视为可用（0.01~0.99）
        [Range(.01f, .99f)] public float cooledPercentageAfterOverheat;

        // 是否无限子弹（为 false 时使用弹匣）
        [Tooltip("Turn it off to set a magazine size")] public bool infiniteBullets;

        // 每个弹匣的子弹容量
        [Tooltip("How many bullets you can have per magazine"), Min(1)] public int magazineSize = 1;

        // 每次开火发射的子弹数量（可实现霰弹枪/连发枪）
        [Tooltip("Bullets instantiated per each shot. Grants the possibility of making shotguns, burstguns etc. Amount of bullets spawned every shot.")]

        [Range(1,8)]public int bulletsPerFire; // Grants the possibility of making shotguns, burstguns etc. Amount of bullets spawned every shot.

        // 每次开火消耗的弹药数（每个开火点各消耗一次）
        [Tooltip("How much ammo you lose per each fire ( & per each fire point )"), Min(0)]

        public int ammoCostPerFire;

        // 子弹最大射程
        [Tooltip("How far the bullet is able to travel")] public int bulletRange;

        // 连发时两发之间的间隔（0 为霰弹式齐射，>0 为连发式逐发）
        [Tooltip("Time elapsed until the next bullet spawns. Keep this at 0" +
            "in order to make shotguns." +
            "Change its value in order to make burst weapons.")]
        public float timeBetweenShots;

        // if bulletsPerFire != 1, then how much time do you wanna get for the next bullet to spawn ( while bullets spawned < bulletsPerFire ) 

        // ── 穿透（穿墙）─────────────────────────────────────────
        // 子弹可穿透物体的数量/层数
        [Tooltip("How much the bullet is able to penetrate an object")] [Range(0, 10)] public float penetrationAmount;

        // 穿透后的伤害衰减倍率（0~1）
        [Tooltip("Damage reduction multiplier. From 0 to 1 ( 0% to 100% )")] [Range(0, 1)] public float penetrationDamageReduction;

        // ── 瞄准（ADS）──────────────────────────────────────────
        // 是否允许瞄准
        [Tooltip("While true it grants the possibility of aiming the weapon")] public bool allowAim;

        // 瞄准时是否忽略瞄准点（为 true 时相机不会对准瞄准点中心）
        [Tooltip("If true, the camera won´t be placed at the center of the aim point when aiming")] public bool ignoreAimPoint;

        // 瞄准时相机与枪的距离
        [Tooltip("Distance from the camera when aiming")] public float aimDistance;

        // 瞄准时的最终旋转角度
        [Tooltip("Final Rotation aiming")] public Vector3 aimingRotation;

        // 瞄准/取消瞄准状态切换的速度
        [Tooltip("Velocity to change between not aiming and aiming states")] [Range(1, 50)] public float aimingSpeed;

        // 瞄准时的相机视场角（FOV）
        [Tooltip("Camera field of view on aiming")] [Range(1, 179)] public float aimingFOV;

        // 瞄准时是否允许换弹
        [Tooltip("If true, you will be able to reload while aiming.")] public bool allowAimingIfReloading;

        // 瞄准时的玩家移动速度
        [Tooltip("Player Movement Speed while Aiming"), Min(0f)] public float movementSpeedWhileAiming;

        // 瞄准时是否使用特定移动速度
        [Tooltip("Sets a specific speed while aiming if true")] public bool setMovementSpeedWhileAiming;

        // ── 准星 ───────────────────────────────────────────────
        // 射击时准星缩放量
        [Tooltip("Resize crosshair on shooting")] public float crosshairResize;

        // 准星部件（上/下/左/右/中心等）
        public CrosshairParts crosshairParts = new CrosshairParts();

        // ── 散布（Spread）───────────────────────────────────────
        // 是否每发都应用散布
        [Tooltip("Apply spread per shot")] public bool applyBulletSpread;

        // 散布幅度
        [Tooltip("How much spread is applied.")] [Range(0, .2f)] public float spreadAmount;

        // 瞄准时的散布幅度
        [Tooltip("How much spread is applied while aiming.")] [Range(0, .2f)] public float aimSpreadAmount;

        // ── 后坐力（Recoil）─────────────────────────────────────
        // 射击时是否应用后坐力
        [Tooltip("Apply recoil on shooting")] public bool applyRecoil;

        // 后坐力 X/Y 轴变化曲线（随时间变化的后坐力曲线）
        public AnimationCurve recoilX, recoilY;

        // 后坐力 X/Y 轴幅度
        public float xRecoilAmount, yRecoilAmount;

        // 后坐力回弹（放松）速度
        public float recoilRelaxSpeed;

        // 瞄准时是否使用不同的后坐力
        public bool applyDifferentRecoilOnAiming;

        // 瞄准时的后坐力 X/Y 轴幅度
        public float xRecoilAmountOnAiming, yRecoilAmountOnAiming;

        // ── 伤害 ───────────────────────────────────────────────
        // 每发子弹造成的伤害
        [Tooltip("Damage dealt per bullet")] public float damagePerBullet;

        // 是否按与目标的距离增减伤害
        [Tooltip("Damage will decrease or increase depending on how far you are from the target")] public bool applyDamageReductionBasedOnDistance;

        // 距离超过该值后才应用伤害衰减
        [Tooltip("Damage reduction will be applied for distances larger than this"), Min(0)] public float minimumDistanceToApplyDamageReduction;

        // 伤害衰减倍率调整量（0.1~1）
        [Tooltip("Adjust the damage reduction amount"), Range(.1f, 1)] public float damageReductionMultiplier;

        // 暴击（Critical）伤害倍率
        [Range(1, 3)] [Tooltip("Damage will get multiplied by this number when hitting a critical shot")] public float criticalDamageMultiplier;

        // ── 弹匣限制 ───────────────────────────────────────────
        // 是否启用有限弹匣（更真实：需要拾取新弹匣补充弹药）
        [Tooltip("Turn this true for a more realistic approach. It will stop using infinite magazines, so you will have to pick new magazines to add more bullets.")] public bool limitedMagazines;

        // 初始弹匣数量（满弹匣）
        [Tooltip("Amount of initial magazines. (Full magazines, meaning that 2 magazines of 10 bullets each will result in having 20 initial bullets you can dispose o f)")] [Range(1, 100)] public int totalMagazines;

        // ── 视觉特效与粒子 ─────────────────────────────────────
        // 是否生成抛出的弹壳（增加真实感）
        [Tooltip("Spawn bullet shells to add realism")] public bool showBulletShells;

        // 弹孔/命中特效（按图层区分）
        [Tooltip("Graphics")] public BulletHoleImpact bulletHoleImpact;

        // 枪口火焰（Muzzle VFX）
        public GameObject muzzleVFX;

        // 弹壳刚体图形（用于弹壳落地效果）
        public Rigidbody bulletGraphics;

        // 是否使用程序化射击动画
        public bool useProceduralShot;

        // 程序化射击动画模式（ProceduralShot_SO）
        public ProceduralShot_SO proceduralShotPattern;

        // ── 音效 ───────────────────────────────────────────────
        // 武器音效集合（射击/换弹/掏枪/收枪等）
        public AudioClips audioSFX;

        // 射击音效音高随机变化幅度（-pitchVariation ~ +pitchVariation）
        [Tooltip("Per each shot, pitch will be modified by a number between -pitchVariationFiringSFX and +pitchVariationFiringSFX"), Range(0, .2f)] public float pitchVariationFiringSFX;

        // ── 相机反馈 ───────────────────────────────────────────
        // 是否从枪口（第一个 firePoint）发射钩索
        [Tooltip("If the player can grapple, and this is true, the grapple will be drawin from the first firePoint of the weapon")] public bool grapplesFromTip;

        // 射击时的镜头抖动幅度
        [Tooltip("Cam Shake Amount on shoot"), Min(0)] public float camShakeAmount;

        // 瞄准时的镜头抖动倍率
        [Tooltip("Cam Shake multiplier when aiming"), Range(0, 1)] public float camShakeAimMultiplier;

        // 蹲伏时的镜头抖动倍率
        [Tooltip("Cam Shake multiplier when crouching"), Range(0, 1)] public float camShakeCrouchMultiplier;

        // 射击时是否应用 FOV 变化效果（FOV 会自动插值回默认值）
        [Tooltip(" If true it will apply a different FOV value when shooting to add an extra layer of detail. FOV will automatically lerp until it reaches its default value.")] public bool applyFOVEffectOnShooting;

        // 射击时从当前 FOV 中减去的值
        [Tooltip("FOV amount subtracted from the current fov on shooting"), Range(0, 180)] public float FOVValueToSubtract;

        // 瞄准射击时从当前 FOV 中减去的值
        [Tooltip("FOV amount subtracted from the current fov on shooting"), Range(0, 180)] public float AimingFOVValueToSubtract;

        // 可用射击动画数量（每次随机选取一个播放）
        [Tooltip("Amount of animations available to be played. A random shoot animation will be picked up each time"), Min(1)] public int amountOfShootAnimations;

        // 子弹拖尾（TrailRenderer）
        public TrailRenderer bulletTrail;

        // ── 近战专属（Melee Exclusive）──────────────────────────
        //Melee Exclusive
        // 近战武器每次攻击造成的伤害
        [Tooltip("Damage the melee weapon deals per hit")] [Range(0, 1000)] public float damagePerHit;

        // 攻击节奏（值越小越快）
        [Range(0, 6), Tooltip("Attacking pace. The lower, the faster.")] public float attackRate;

        // 近战命中判定的延迟时间
        [Range(0, 2), Tooltip("Time to delay the melee hit")] public float hitDelay;

        // 每个近战动画的命中延迟数组（索引 0 -> shooting, 1 -> shooting2, ...）
        [Tooltip("Per animation melee hit delays (index 0 -> shooting, 1 -> shooting2, ...)")] public float[] hitDelays;

        // 近战攻击范围（能命中目标的最大距离）
        [Range(0, 5)]
        [Tooltip("How far you are able to land a hit")] public float attackRange;

        // ── 其它 ───────────────────────────────────────────────
        // 是否隐藏弹匣（过热换弹模式等场景下自动设置）
        [HideInInspector] public bool dontShowMagazine;

        // 移动过渡速度覆盖配置
        public LocomotionTransitionOverrides locomotionTransitions = new LocomotionTransitionOverrides();

#if UNITY_EDITOR
        // 当前编辑器预设（Preset）
        public Preset currentPreset;
#endif

        // 预设名称（保存/应用预设时使用）
        public string presetName;

#if INVENTORY_PRO_ADD_ON
        /// <summary>物品被“使用”时的回调（Inventory Pro 插件专用）：从背包/快捷栏装备武器，
        /// 或直接切换到指定快捷栏武器。</summary>
        /// <param name="inventoryProManager">Inventory Pro 管理器引用</param>
        /// <param name="slot">被使用的物品槽位</param>
        public override void Use(InventoryProManager inventoryProManager, InventorySlot slot)
        {
            WeaponController weaponController = inventoryProManager._WeaponController;
            if (slot.IsInventorySlot)
            {
                if (weaponController.Inventory[weaponController.CurrentWeaponIndex] != null)
                {
                    ToastManager.Instance?.ShowToast(ToastManager.Instance.WeaponIsAlreadyUnholsteredMsg);
                    return;
                }
                SlotData slotData = slot.slotData;
                Weapon_SO weaponSO = (Weapon_SO)slotData.item;
                if (weaponSO == null) return;

                weaponController.InstantiateWeapon(weaponSO, weaponController.CurrentWeaponIndex, slotData.bulletsLeftInMagazine, slotData.totalBullets);
                inventoryProManager._GridGenerator.Operations.ClearSlotArea(slot);
                inventoryProManager._GridGenerator.UpdateHotbarSlot(weaponController.CurrentWeaponIndex, slotData.barrel, slotData.scope, slotData.stock, slotData.grip,
                    slotData.magazine, slotData.flashlight, slotData.laser);
            }
            else if (slot.IsHotbarSlot)
            {
                weaponController.CurrentWeaponIndex = slot.col;
                weaponController.weaponInventoryBehaviour.SelectWeapon(); 
            }
        }
#endif

    }
    #endregion

#if UNITY_EDITOR
    #region customEditor
    // 武器自定义编辑器：为 Weapon_SO 提供分页（Basic/Shooting/Stats/Visuals/Audio/UI）Inspector 界面，
    // 包含缺失引用检测、后坐力曲线绘制、预设保存/应用等辅助功能。
    /// <summary>
    /// CUSTOM EDITOR STUFF
    /// </summary>
    [System.Serializable]
    [CustomEditor(typeof(Weapon_SO))]
    public class Weapon_SOEditor : Editor
    {
        // 编辑器分页标签
        private string[] tabs = { "Basic", "Shooting", "Stats", "Visuals", "Audio", "UI" };
        // 当前选中的分页索引
        private int currentTab = 0;

        // Inventory 插件是否可用（用于锁定相关功能）
        private bool isInventoryAddonAvailable;
        // 是否显示后坐力曲线图
        private bool showRecoilPlot = false;
        // 是否显示默认后坐力曲线
        private bool showDefaultRecoilPlot = true;
        // 是否显示瞄准后坐力曲线
        private bool showAimedRecoilPlot = true;
        // 是否显示曲线标签
        private bool showLabels = true;
        // 是否显示合并曲线
        private bool showCombinedPlot = true;
        // 启用时：检测 Inventory 插件是否可用（通过编译宏判断）。
        private void OnEnable()
        {
            // Check if the Inventory Addon is available
#if INVENTORY_PRO_ADD_ON
                isInventoryAddonAvailable = true;
#else
                isInventoryAddonAvailable = false;
#endif
        }

        /// <summary>绘制自定义 Inspector：先检测缺失引用，再按分页渲染各设置区块，
        /// 最后渲染预设保存/应用与锁定图标，并应用修改后的属性。</summary>
        override public void OnInspectorGUI()
        {
            serializedObject.Update();
            Weapon_SO myScript = target as Weapon_SO;

            Texture2D myTexture = Resources.Load<Texture2D>("CustomEditor/weaponObject_CustomEditor") as Texture2D;
            GUILayout.Label(myTexture);

            EditorGUILayout.BeginVertical();
            EditorGUILayout.Space(10f);
            if (GUILayout.Button("Adding New Weapons Tutorial", GUILayout.Height(20)))
            {
                Application.OpenURL("https://www.cowsins.com/videos/OykOx8CgEMc");
            }
            EditorGUILayout.Space(10f);


            (bool hasMissingReferences, List<string> missingReferenceNames) = DetectMissingReferences(myScript);

            if (hasMissingReferences)
            {
                string missingReferencesString = string.Join(", ", missingReferenceNames.ToArray());
                EditorGUILayout.HelpBox("Missing references: " + missingReferencesString + ". Please fill in all required fields.", MessageType.Error);
            }




            EditorGUILayout.Space(10f);
            currentTab = GUILayout.Toolbar(currentTab, tabs);
            EditorGUILayout.Space(10f);
            EditorGUILayout.EndVertical();

            int style = (int)myScript.shootStyle;

            if (currentTab >= 0 || currentTab < tabs.Length)
            {
                switch (tabs[currentTab])
                {
                    case "Basic":
                        BeginBox("BASIC SETTINGS");
                        EditorGUILayout.Space(10f);
                        EditorGUILayout.PropertyField(serializedObject.FindProperty("_name"));
                        EditorGUILayout.LabelField("This represents your first-person weapon in the game.", EditorStyles.helpBox);
                        EditorGUILayout.PropertyField(serializedObject.FindProperty("weaponObject"));
                        EditorGUILayout.LabelField("This represents the graphics of your weapon on the ground.", EditorStyles.helpBox);
                        EditorGUILayout.PropertyField(serializedObject.FindProperty("pickUpGraphics"));
                        EditorGUILayout.PropertyField(serializedObject.FindProperty("icon"));
                        GUI.enabled = isInventoryAddonAvailable;
                        if (!isInventoryAddonAvailable)
                        {
                            EditorGUILayout.Space(10);
                            EditorGUILayout.HelpBox("These features are locked. Inventory Pro Manager + Save & Load Add-On is not installed.", MessageType.Info);
                            EditorGUILayout.Space(5);
                        }
                        EditorGUILayout.PropertyField(serializedObject.FindProperty("itemSize"));
                        if (myScript.itemSize.x != myScript.itemSize.y)
                        {
                            EditorGUI.indentLevel++;
                            EditorGUILayout.Space(5);
                            EditorGUILayout.HelpBox($"Your item has an irregular size [{myScript.itemSize.x} : {myScript.itemSize.y}], which may cause icon stretching." +
                            "To maintain the correct aspect ratio, consider setting 'irregularItemIcon' with a properly scaled icon.", MessageType.Info);
                            EditorGUILayout.Space(5);
                            EditorGUILayout.PropertyField(serializedObject.FindProperty("irregularItemIcon"));
                            EditorGUI.indentLevel--;
                        }

                        GUI.enabled = true;
                        EditorGUILayout.Space(20f);
                        EditorGUILayout.BeginHorizontal();
                        {
                            GUILayout.FlexibleSpace();

                            Texture2D tex = AssetPreview.GetAssetPreview(myScript.icon);
                            GUILayout.Label("", GUILayout.Height(50), GUILayout.Width(50));
                            GUI.DrawTexture(GUILayoutUtility.GetLastRect(), tex);

                            GUILayout.FlexibleSpace();
                        }
                        EditorGUILayout.EndHorizontal();
                        EndBox();

                        break;
                    case "Shooting":
                        if (myScript.shootStyle == ShootStyle.Custom)
                        {
                            EditorGUILayout.Space(10f);
                            if (GUILayout.Button("Custom Shot Weapons Tutorial", GUILayout.Height(20)))
                            {
                                Application.OpenURL("https://www.cowsins.com/videos/OJ6uPo66Ams");
                            }
                            EditorGUILayout.Space(10f);
                        }
                        BeginBox("SHOOTING SETTINGS");

                        EditorGUILayout.PropertyField(serializedObject.FindProperty("shootStyle"));
                        EditorGUILayout.PropertyField(serializedObject.FindProperty("shootMethod"));

                        if (myScript.shootMethod == ShootingMethod.HoldUntilReady || myScript.shootMethod == ShootingMethod.HoldAndRelease)
                        {
                            EditorGUI.indentLevel++;
                            EditorGUILayout.PropertyField(serializedObject.FindProperty("holdProgressSpeed"));
                            EditorGUI.indentLevel--;
                        }

                        EndBox();

                        switch (style)
                        {
                            case 0:
                                WeaponShootingSharedVariables(myScript);
                                break;
                            case 1:
                                BeginBox("PROJECTILE SETTINGS");
                                EditorGUILayout.PrefixLabel("Attach your Projectile here", EditorStyles.helpBox);
                                if (myScript.projectile != null && myScript.projectile.GetComponent<IBullet>() == null)
                                {
                                    EditorGUILayout.HelpBox($"Projectile prefab '{myScript.projectile.name}' does not have a component implementing IBullet!", MessageType.Error);
                                }
                                EditorGUILayout.PropertyField(serializedObject.FindProperty("projectile"));
                                EditorGUILayout.PropertyField(serializedObject.FindProperty("projectileUsesGravity"));
                                EditorGUILayout.PropertyField(serializedObject.FindProperty("speed"));
                                if (myScript.shootDelay > myScript.fireRate) myScript.shootDelay = myScript.fireRate;
                                EditorGUILayout.PropertyField(serializedObject.FindProperty("shootDelay"));

                                if (myScript.shootStyle == ShootStyle.Projectile && myScript.bulletDuration <= 0f)
                                {
                                    EditorGUILayout.HelpBox("Bullet Duration must be greater than 0 when using Projectile shoot style, otherwise the bullet will be destroyed immediately.", MessageType.Error);
                                }
                                
                                EditorGUILayout.PropertyField(serializedObject.FindProperty("bulletDuration"));
                                EditorGUILayout.PropertyField(serializedObject.FindProperty("hurtsPlayer"));
                                EditorGUILayout.PropertyField(serializedObject.FindProperty("explosionOnHit"));
                                if (myScript.explosionOnHit)
                                {
                                    EditorGUI.indentLevel++;
                                    EditorGUILayout.PropertyField(serializedObject.FindProperty("explosionRadius"));
                                    EditorGUILayout.PropertyField(serializedObject.FindProperty("explosionForce"));
                                    EditorGUILayout.LabelField("Check new options under 'Visuals' tab. ", EditorStyles.helpBox);
                                    EditorGUI.indentLevel--;
                                }
                                EndBox();
                                WeaponShootingSharedVariables(myScript);
                                EditorGUILayout.Space(5f);
                                break;
                            case 2:
                                BeginBox("MELEE OPTIONS");
                                EditorGUILayout.Space(2f);
                                var attackRangeProperty = serializedObject.FindProperty("attackRange");
                                EditorGUILayout.PropertyField(attackRangeProperty);
                                var attackRateProperty = serializedObject.FindProperty("attackRate");
                                EditorGUILayout.PropertyField(attackRateProperty);
                                var hitDelayProp = serializedObject.FindProperty("hitDelay");
                                var hitDelaysProp = serializedObject.FindProperty("hitDelays");

                                if (myScript.amountOfShootAnimations <= 1)
                                {
                                    EditorGUILayout.PropertyField(hitDelayProp);
                                    if (hitDelaysProp.arraySize != 0)
                                        hitDelaysProp.arraySize = 0;
                                }
                                else
                                {
                                    if (hitDelaysProp.arraySize != myScript.amountOfShootAnimations)
                                        hitDelaysProp.arraySize = myScript.amountOfShootAnimations;
                                    EditorGUILayout.PropertyField(hitDelaysProp, new GUIContent("Per-Attack Hit Delays"), true);
                                    if (hitDelayProp.floatValue != 0f)
                                        hitDelayProp.floatValue = 0f;
                                }
                                EndBox();
                                break;
                            case 3:
                                BeginBox("CUSTOM SHOT WEAPONS OPTIONS");
                                EditorGUILayout.PropertyField(serializedObject.FindProperty("continuousFire"));
                                if (!myScript.continuousFire)
                                {
                                    EditorGUI.indentLevel++;
                                    EditorGUILayout.PropertyField(serializedObject.FindProperty("fireRate"));
                                    EditorGUI.indentLevel--;
                                }
                                else EditorGUILayout.LabelField("Continuous Fire will call your custom method ONCE per frame.", EditorStyles.helpBox);
                                EndBox();
                                break;
                        }
                        break;
                    case "Stats":
                        switch (style)
                        {
                            case 0:
                                WeaponStatsSharedVariables(myScript);
                                break;
                            case 1:
                                WeaponStatsSharedVariables(myScript);
                                break;
                            case 2:
                                BeginBox("MELEE STATS");
                                EditorGUILayout.PropertyField(serializedObject.FindProperty("damagePerHit"));
                                EditorGUILayout.PropertyField(serializedObject.FindProperty("criticalDamageMultiplier")); 
                                
                                var applyWeightProperty = serializedObject.FindProperty("applyWeight");
                                EditorGUILayout.PropertyField(applyWeightProperty);

                                using (var group = new EditorGUILayout.FadeGroupScope(Convert.ToSingle(myScript.applyWeight)))
                                {
                                    if (group.visible == true)
                                    {
                                        EditorGUI.indentLevel++;
                                        var weightMultiplierProperty = serializedObject.FindProperty("weightMultiplier");
                                        EditorGUILayout.PropertyField(weightMultiplierProperty);
                                        EditorGUI.indentLevel--;
                                    }
                                }
                                if (!myScript.applyWeight) myScript.weightMultiplier = 1;
                                EndBox();   
                                break;
                            case 3:
                                DrawAimSettings(myScript);
                                break;
                        }
                        break;
                    case "Visuals":
                        switch (style)
                        {
                            case 0:
                                EditorGUILayout.Space(5f);
                                BeginBox("PROCEDURAL ANIMATIONS");
                                EditorGUILayout.PropertyField(serializedObject.FindProperty("useProceduralShot"));
                                if (myScript.useProceduralShot)
                                {
                                    EditorGUI.indentLevel++;
                                    EditorGUILayout.PropertyField(serializedObject.FindProperty("proceduralShotPattern"));
                                    EditorGUI.indentLevel--;
                                }
                                EndBox();
                                WeaponVisualSharedVariables(myScript);
                                break;
                            case 1:
                                BeginBox("PROCEDURAL ANIMATIONS");
                                GUILayout.Box("", new GUILayoutOption[] { GUILayout.ExpandWidth(true), GUILayout.Height(8) });
                                EditorGUILayout.PropertyField(serializedObject.FindProperty("useProceduralShot"));
                                if (myScript.useProceduralShot)
                                {
                                    EditorGUI.indentLevel++;
                                    EditorGUILayout.PropertyField(serializedObject.FindProperty("proceduralShotPattern"));
                                    EditorGUI.indentLevel--;
                                }
                                EndBox();
                                WeaponVisualSharedVariables(myScript);
                                if (myScript.explosionOnHit)
                                {
                                    BeginBox("");
                                    EditorGUILayout.PropertyField(serializedObject.FindProperty("explosionVFX"));
                                    EndBox();
                                }
                                break;
                            case 2:
                                BeginBox("CAMERA");
                                EditorGUILayout.PropertyField(serializedObject.FindProperty("camShakeAmount"));
                                EditorGUILayout.PropertyField(serializedObject.FindProperty("applyFOVEffectOnShooting"));
                                if (myScript.applyFOVEffectOnShooting)
                                {
                                    EditorGUI.indentLevel++;
                                    EditorGUILayout.PropertyField(serializedObject.FindProperty("FOVValueToSubtract"));
                                    EditorGUI.indentLevel--;
                                }
                                EndBox();
                                BeginBox("ANIMATIONS");
                                EditorGUILayout.PropertyField(serializedObject.FindProperty("amountOfShootAnimations"));
                                EndBox();
                                BeginBox("IMPACTS");
                                EditorGUILayout.PropertyField(serializedObject.FindProperty("bulletHoleImpact"));
                                EndBox();
                                break;
                             case 3:
                                BeginBox("CAMERA");
                                EditorGUILayout.PropertyField(serializedObject.FindProperty("camShakeAmount"));
                                break;
                        }
                        break;
                    case "Audio":
                        WeaponAudioSharedVariables(myScript);
                        break;

                    case "UI":
                        WeaponUISharedVariables(myScript);
                        if (style == 2) myScript.allowAim = false;

                        break;

                }

                EditorGUILayout.Space(10f);
                EditorGUILayout.LabelField("PRESETS", EditorStyles.boldLabel);
                GUILayout.Box("", new GUILayoutOption[] { GUILayout.ExpandWidth(true), GUILayout.Height(8) });
                EditorGUILayout.Space(2f);

                EditorGUILayout.PropertyField(serializedObject.FindProperty("currentPreset"));
                EditorGUILayout.Space(2f);
                EditorGUILayout.PropertyField(serializedObject.FindProperty("presetName"));
                EditorGUILayout.Space(5f);
                if (GUILayout.Button("Save Settings as a Preset")) CowsinsUtilities.SavePreset(myScript, myScript.presetName);

                EditorGUILayout.Space(5f);

                if (GUILayout.Button("Apply Current Preset"))
                {
                    if (myScript.currentPreset != null) CowsinsUtilities.ApplyPreset(myScript.currentPreset, myScript);
                    else Debug.LogError("Can�t apply a non existing preset. Please, assign your desired preset to 'currentPreset'. ");
                }
            }
            if ((int)myScript.reloadStyle == 1) myScript.dontShowMagazine = true;
            else myScript.dontShowMagazine = false;


            EditorGUILayout.Space(20f);
            EditorGUILayout.BeginHorizontal();
            {
                GUILayout.FlexibleSpace();

                // LOCK ICON 

                GUI.backgroundColor = Color.clear;

                btnTexture = ActiveEditorTracker.sharedTracker.isLocked
                    ? Resources.Load<Texture2D>("CustomEditor/lockedIcon") as Texture2D
                    : Resources.Load<Texture2D>("CustomEditor/unlockedIcon") as Texture2D;

                button = new GUIContent(btnTexture);

                if (GUILayout.Button(button, GUIStyle.none, GUILayout.Width(lockIconSize), GUILayout.Height(lockIconSize)))
                    ActiveEditorTracker.sharedTracker.isLocked = !ActiveEditorTracker.sharedTracker.isLocked;

                GUILayout.FlexibleSpace();
            }
            EditorGUILayout.EndHorizontal();



            serializedObject.ApplyModifiedProperties();
        }
        // 锁定图标尺寸（Inspector 右上角锁定按钮）
        private float lockIconSize = 40;
        // 锁定/解锁按钮当前使用的纹理
        private Texture btnTexture;
        // 锁定/解锁按钮的 GUIContent
        private GUIContent button;
        // 渲染 UI 分页：准星缩放与准星部件编辑。
        private void WeaponUISharedVariables(Weapon_SO myScript)
        {
            BeginBox("USER INTERFACE (UI)");

            EditorGUILayout.PropertyField(serializedObject.FindProperty("crosshairResize"));

            CrosshairParts crosshairParts = myScript.crosshairParts;
            if (!crosshairParts.topPart && !crosshairParts.downPart && !crosshairParts.leftPart && !crosshairParts.rightPart && !crosshairParts.center)
                EditorGUILayout.HelpBox($"Crosshair will not be visible because there are no parts selected. If this is the intended behaviour, ignore this message.", MessageType.Warning);

            CowsinsEditorWindowUtilities.DrawCrosshairEditorSection(serializedObject, myScript.crosshairParts, "crosshairParts");

            EndBox();   

        }
        // 渲染音频分页：音效集合与音高变化幅度。
        private void WeaponAudioSharedVariables(Weapon_SO myScript)
        {
            BeginBox("AUDIO");
            var audioSFXProperty = serializedObject.FindProperty("audioSFX");
            EditorGUILayout.PropertyField(audioSFXProperty);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("pitchVariationFiringSFX"));
            EndBox();
        }

        // 渲染射击设置：射速、射程、每发弹数、弹药消耗、散布与后坐力及穿透。
        private void WeaponShootingSharedVariables(Weapon_SO myScript)
        {
            BeginBox("WEAPON SHOOTING SETTINGS");

            if (myScript.shootMethod != ShootingMethod.HoldUntilReady && myScript.shootMethod != ShootingMethod.HoldAndRelease)
            {
                EditorGUI.indentLevel++;
                float rpm = 60 / myScript.fireRate;
                EditorGUILayout.HelpBox($"RPM ( Revolutions Per Minute ): {rpm:F0}rpm", myScript.fireRate > 0 ? MessageType.Info : MessageType.Error);
                EditorGUILayout.PropertyField(serializedObject.FindProperty("fireRate"));
                EditorGUI.indentLevel--;
            }

            var bulletRangeProperty = serializedObject.FindProperty("bulletRange");
            EditorGUILayout.PropertyField(bulletRangeProperty);

            var bulletsPerFireProperty = serializedObject.FindProperty("bulletsPerFire");
            EditorGUILayout.PropertyField(bulletsPerFireProperty);

            if (myScript.bulletsPerFire > 1)
            {
                EditorGUI.indentLevel++;
                var timeBetweenShotsProperty = serializedObject.FindProperty("timeBetweenShots");
                EditorGUILayout.PropertyField(timeBetweenShotsProperty);
                EditorGUI.indentLevel--;
            }
            EditorGUILayout.PropertyField(serializedObject.FindProperty("ammoCostPerFire"));
            if (myScript.ammoCostPerFire > myScript.bulletsPerFire) myScript.ammoCostPerFire = myScript.bulletsPerFire;

            EndBox();

            BeginBox("BULLET SPREAD");

            var applyBulletSpreadProperty = serializedObject.FindProperty("applyBulletSpread");
            EditorGUILayout.PropertyField(applyBulletSpreadProperty);

            using (var group = new EditorGUILayout.FadeGroupScope(Convert.ToSingle(myScript.applyBulletSpread)))
            {

                if (group.visible == true)
                {
                    EditorGUI.indentLevel++;
                    var spreadAmountProperty = serializedObject.FindProperty("spreadAmount");
                    EditorGUILayout.PropertyField(spreadAmountProperty);
                    if (myScript.allowAim) EditorGUILayout.PropertyField(serializedObject.FindProperty("aimSpreadAmount"));
                    EditorGUI.indentLevel--;
                }
            }
            if (!myScript.applyBulletSpread) myScript.spreadAmount = 0;

            EndBox();

            BeginBox("RECOIL");

            EditorGUILayout.PropertyField(serializedObject.FindProperty("applyRecoil"));

            if (myScript.applyRecoil)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(serializedObject.FindProperty("recoilX"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("recoilY"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("xRecoilAmount"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("yRecoilAmount"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("recoilRelaxSpeed"));
                if (myScript.allowAim)
                {
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("applyDifferentRecoilOnAiming"));
                    if (myScript.applyDifferentRecoilOnAiming)
                    {
                        EditorGUI.indentLevel++;
                        EditorGUILayout.PropertyField(serializedObject.FindProperty("xRecoilAmountOnAiming"));
                        EditorGUILayout.PropertyField(serializedObject.FindProperty("yRecoilAmountOnAiming"));
                        EditorGUI.indentLevel--;
                    }
                }
                GUILayout.Space(5);

                if(GUILayout.Button(showRecoilPlot ? "Hide Recoil Plot" : "Show Recoil Plot")) showRecoilPlot = !showRecoilPlot;

                if(showRecoilPlot) PlotRecoil(myScript);

                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space(5f);
            EditorGUI.indentLevel--;
            EditorGUILayout.EndVertical();


            if (myScript.shootStyle != ShootStyle.Projectile)
            {
                BeginBox("PENETRATION (WALLBANG)");
                var penetrationAmountProperty = serializedObject.FindProperty("penetrationAmount");
                EditorGUILayout.PropertyField(penetrationAmountProperty);
                if (myScript.penetrationAmount != 0)
                {
                    EditorGUI.indentLevel++;
                    var penetrationDamageReductionProperty = serializedObject.FindProperty("penetrationDamageReduction");
                    EditorGUILayout.PropertyField(penetrationDamageReductionProperty);
                    EditorGUI.indentLevel--;
                }
                EndBox();
            }
        }

        // 渲染属性分页：弹药/弹匣、换弹、掏枪、伤害、瞄准与物品重量。
        private void WeaponStatsSharedVariables(Weapon_SO myScript)
        {
            EditorGUILayout.LabelField("WEAPON STATS", EditorStyles.boldLabel);
            EditorGUILayout.Space(5f);

            var limitedMagazinesProperty = serializedObject.FindProperty("limitedMagazines");
            var reloadStyleProperty = serializedObject.FindProperty("reloadStyle");
            var allowCancelReloadProperty = serializedObject.FindProperty("allowCancelReload");

            BeginBox("AMMO & MAGAZINES");

            if (myScript.dontShowMagazine == false)
            {
                EditorGUILayout.PropertyField(serializedObject.FindProperty("infiniteBullets"));
                if (!myScript.infiniteBullets)
                {
                    EditorGUI.indentLevel++;
                    var magazineSizeProperty = serializedObject.FindProperty("magazineSize");
                    EditorGUILayout.PropertyField(magazineSizeProperty);

                    if(limitedMagazinesProperty.boolValue && reloadStyleProperty.enumValueIndex == 0) 
                    EditorGUILayout.LabelField("Bullet Type Identifier unlocked. Check under Reload options. ", EditorStyles.helpBox);
                    EditorGUILayout.PropertyField(limitedMagazinesProperty);
                    EditorGUI.indentLevel--;
                    using (var group = new EditorGUILayout.FadeGroupScope(Convert.ToSingle(myScript.limitedMagazines)))
                    {

                        if (group.visible == true)
                        {
                            EditorGUI.indentLevel++;
                            var totalMagazinesProperty = serializedObject.FindProperty("totalMagazines");
                            EditorGUILayout.PropertyField(totalMagazinesProperty);
                            EditorGUI.indentLevel--;
                        }
                    }
                }
            }
            else
            {
                EditorGUI.indentLevel++;
                myScript.limitedMagazines = false;
                myScript.infiniteBullets = false;
                EditorGUILayout.PropertyField(serializedObject.FindProperty("magazineSize"));
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(serializedObject.FindProperty("coolSpeed"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("cooledPercentageAfterOverheat"));
                EditorGUI.indentLevel--;
                EditorGUI.indentLevel--;
            }

            EndBox();   

            BeginBox("RELOAD");

            EditorGUILayout.PropertyField(reloadStyleProperty);
            EditorGUILayout.PropertyField(allowCancelReloadProperty);

            var bulletTypeIdentifierProperty = serializedObject.FindProperty("bulletTypeIdentifier");

            if(reloadStyleProperty.enumValueIndex == 0 && limitedMagazinesProperty.boolValue == true)
            {
                EditorGUI.indentLevel++;
                if(bulletTypeIdentifierProperty.objectReferenceValue == null )
                {
                    EditorGUILayout.HelpBox($"You´re using Universal Bullets Type. Leave Bullet Type Identifier unassigned if this is intended.", MessageType.Info);
                }
                EditorGUILayout.PropertyField(bulletTypeIdentifierProperty);
                EditorGUI.indentLevel--;
            }
            else
            {
                bulletTypeIdentifierProperty.objectReferenceValue = null;
            }

            Animator animator = null;
            float suggestedReload = 0f; bool hasSuggestedReload = false;
            float suggestedEmptyReload = 0f; bool hasSuggestedEmptyReload = false;
            if (animator == null && myScript.weaponObject != null)
            {
                animator = myScript.weaponObject.GetComponentInChildren<Animator>();
                if (animator != null)
                {
                    (bool success, float length) = CowsinsUtilities.CheckClipAvailability(animator, "reloading");
                    if (success) { suggestedReload = length; hasSuggestedReload = true; }

                    (bool emptySuccess, float emptyLength) = CowsinsUtilities.CheckClipAvailability(animator, "empty_reloading");
                    if (emptySuccess) { suggestedEmptyReload = emptyLength; hasSuggestedEmptyReload = true; }

                    (bool holsterSuccess, float holsterLength) = CowsinsUtilities.CheckClipAvailability(animator, "Holster");
                    (bool unholsterSuccess, float unholsterLength) = CowsinsUtilities.CheckClipAvailability(animator, "Unholster");
                }
            }

            if (hasSuggestedReload)
                EditorGUILayout.HelpBox($"Suggested reload time (from 'reloading' clip): {suggestedReload:F2} s", MessageType.Info);
            var reloadTimeProperty = serializedObject.FindProperty("reloadTime");
            EditorGUILayout.PropertyField(reloadTimeProperty);

            if (hasSuggestedEmptyReload)
                EditorGUILayout.HelpBox($"Suggested empty-reload time (from 'empty_reloading' clip): {suggestedEmptyReload:F2} s", MessageType.Info);

            var emptyReloadTimeProp = serializedObject.FindProperty("emptyReloadTime");
            if (Mathf.Approximately(emptyReloadTimeProp.floatValue, 0f))
                emptyReloadTimeProp.floatValue = reloadTimeProperty.floatValue;
            EditorGUILayout.PropertyField(emptyReloadTimeProp, new GUIContent("Empty Reload Time"));
            
            EndBox();

            BeginBox("UNHOLSTER");


            var overrideUnholsterTimeProperty = serializedObject.FindProperty("overrideUnholsterTime");
            EditorGUILayout.PropertyField(overrideUnholsterTimeProperty);

            if (myScript.overrideUnholsterTime)
            {
                EditorGUI.indentLevel++;
                if (myScript.weaponObject != null)
                {
                    animator = myScript.weaponObject.GetComponentInChildren<Animator>();
                    if (animator != null)
                    {
                        (bool unholsterSuccess, float unholsterLength) = CowsinsUtilities.CheckClipAvailability(animator, "Unholster");
                        if (unholsterSuccess) EditorGUILayout.HelpBox($"Suggested unholster time (from 'unholster' clip): {unholsterLength:F2} s", MessageType.Info);
                    }
                }
                EditorGUILayout.PropertyField(serializedObject.FindProperty("unholsterTime"));
                EditorGUI.indentLevel--;
            }
            EndBox();

            BeginBox("DAMAGE");

            float dps = myScript.damagePerBullet / myScript.fireRate;
            float criticalDps = myScript.damagePerBullet * myScript.criticalDamageMultiplier / myScript.fireRate;
            EditorGUILayout.HelpBox($"DPS (DAMAGE PER SECOND): {dps:F2} \nCRITICAL DPS: {criticalDps:F2}", MessageType.Info);

            var damagePerBulletProperty = serializedObject.FindProperty("damagePerBullet");
            EditorGUILayout.PropertyField(damagePerBulletProperty);

            EditorGUILayout.PropertyField(serializedObject.FindProperty("criticalDamageMultiplier"));

            if (myScript.shootStyle == ShootStyle.Hitscan)
            {
                EditorGUILayout.PropertyField(serializedObject.FindProperty("applyDamageReductionBasedOnDistance"));
                if (myScript.applyDamageReductionBasedOnDistance)
                {
                    EditorGUI.indentLevel++;
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("minimumDistanceToApplyDamageReduction"));
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("damageReductionMultiplier"));
                    EditorGUI.indentLevel--;
                }
            }
            EndBox();

            DrawAimSettings(myScript);

            BeginBox("ITEM WEIGHT");

            var applyWeightProperty = serializedObject.FindProperty("applyWeight");
            EditorGUILayout.PropertyField(applyWeightProperty);


            using (var group = new EditorGUILayout.FadeGroupScope(Convert.ToSingle(myScript.applyWeight)))
            {
                if (group.visible == true)
                {
                    var weightMultiplierProperty = serializedObject.FindProperty("weightMultiplier");
                    EditorGUILayout.PropertyField(weightMultiplierProperty);
                }
            }
            if (!myScript.applyWeight) myScript.weightMultiplier = 1;

            EndBox();
        }

        // 渲染视觉分页：钩索、相机、移动过渡、弹壳、枪口火焰与子弹拖尾。
        private void WeaponVisualSharedVariables(Weapon_SO myScript)
        {
            BeginBox("GRAPPLE VISUALS");
            EditorGUILayout.PropertyField(serializedObject.FindProperty("grapplesFromTip"));
            EndBox();

            BeginBox("CAMERA");
            EditorGUILayout.PropertyField(serializedObject.FindProperty("camShakeAmount"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("camShakeCrouchMultiplier"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("camShakeAimMultiplier"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("applyFOVEffectOnShooting"));
            if (myScript.applyFOVEffectOnShooting)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(serializedObject.FindProperty("FOVValueToSubtract"));
                EditorGUI.indentLevel++;
                EditorGUILayout.LabelField("Your normal aiming FOV value is " + myScript.aimingFOV, EditorStyles.helpBox);
                EditorGUI.indentLevel--;
                EditorGUILayout.PropertyField(serializedObject.FindProperty("AimingFOVValueToSubtract"));
                EditorGUI.indentLevel--;
            }

            EndBox();

            BeginBox("LOCOMOTION TRANSITIONS");
            var locomotionTransitions = serializedObject.FindProperty("locomotionTransitions");
            var overrideTransitions = locomotionTransitions.FindPropertyRelative("overrideTransitions");
            EditorGUILayout.PropertyField(overrideTransitions);

            using (var group = new EditorGUILayout.FadeGroupScope(Convert.ToSingle(overrideTransitions.boolValue)))
            {
                if (group.visible == true)
                {
                    EditorGUI.indentLevel++;
                    EditorGUILayout.PropertyField(locomotionTransitions.FindPropertyRelative("idleToWalkSpeed"));
                    EditorGUILayout.PropertyField(locomotionTransitions.FindPropertyRelative("walkToIdleSpeed"));
                    EditorGUILayout.PropertyField(locomotionTransitions.FindPropertyRelative("walkToRunSpeed"));
                    EditorGUILayout.PropertyField(locomotionTransitions.FindPropertyRelative("runToWalkSpeed"));
                    EditorGUI.indentLevel--;
                }
            }
            EndBox();

            BeginBox("BULLET SHELLS");
            var showBulletShellsProperty = serializedObject.FindProperty("showBulletShells");
            EditorGUILayout.PropertyField(showBulletShellsProperty);
            if (myScript.shootStyle == ShootStyle.Melee) myScript.showBulletShells = false;

            if (myScript.bulletsPerFire == 1) myScript.timeBetweenShots = 0;

            using (var group = new EditorGUILayout.FadeGroupScope(Convert.ToSingle(myScript.showBulletShells)))
            {
                if (group.visible == true)
                {
                    EditorGUI.indentLevel++;
                    var bulletGraphicsProperty = serializedObject.FindProperty("bulletGraphics");
                    EditorGUILayout.PropertyField(bulletGraphicsProperty);
                    EditorGUI.indentLevel--;
                }
            }

            if (myScript.explosionOnHit && myScript.shootStyle == ShootStyle.Projectile)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(serializedObject.FindProperty("explosionVFX"));
                EditorGUI.indentLevel--;
            }

            if (myScript.shootStyle != ShootStyle.Projectile)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(serializedObject.FindProperty("bulletHoleImpact"));
                EditorGUI.indentLevel--;
            }
            var MuzzleVFXProperty = serializedObject.FindProperty("muzzleVFX");
            EditorGUILayout.PropertyField(MuzzleVFXProperty);
            EditorGUILayout.LabelField("Leave this Bullet Trail Unassigned if you do not want to use Bullet Trails. No errors will show up.", EditorStyles.helpBox);
            var bulletTrailProperty = serializedObject.FindProperty("bulletTrail");
            EditorGUILayout.PropertyField(bulletTrailProperty);

            EndBox();
        }

        // 检测缺失引用：遍历关键字段，返回是否缺失及缺失项名称列表。
        private (bool, List<string>) DetectMissingReferences(Weapon_SO script)
        {
            var missingReferences = new List<string>();

            Dictionary<string, bool> references = new Dictionary<string, bool>
            {
                { "weaponObject [Basic Tab]", script.weaponObject == null },
                { "pickUpGraphics [Basic Tab]", script.pickUpGraphics == null },
                { "icon [Basic Tab]", script.icon == null },
                { "projectile [Shooting Tab]", script.shootStyle == ShootStyle.Projectile && script.projectile == null },
                { "proceduralShotPattern [Visuals Tab]", script.useProceduralShot && script.proceduralShotPattern == null },
                { "bulletGraphics [Visuals Tab]", script.showBulletShells && script.bulletGraphics == null && script.shootStyle != ShootStyle.Melee },
                { "firingSFX [Audio Tab]", script.audioSFX?.shooting == null || script.audioSFX?.shooting?.Length <= 0},
                { "holsterSFX [Audio Tab]", script.audioSFX?.holster == null },
                { "unholsterSFX [Audio Tab]", script.audioSFX?.unholster == null },
                { "reloadSFX [Audio Tab]", script.shootStyle != ShootStyle.Melee && !script.infiniteBullets && script.reloadStyle == ReloadingStyle.defaultReload && script.audioSFX?.reload == null },
                { "emptyMagReloadSFX [Audio Tab]", script.shootStyle != ShootStyle.Melee && !script.infiniteBullets && script.reloadStyle == ReloadingStyle.defaultReload && script.audioSFX?.emptyMagReload == null },
                { "emptyMagShootSFX [Audio Tab]", script.shootStyle != ShootStyle.Melee && !script.infiniteBullets && script.reloadStyle == ReloadingStyle.defaultReload && script.audioSFX?.emptyMagShoot == null },
                { "Bullet Duration can´t be 0 [Shooting Tab]", script.shootStyle == ShootStyle.Projectile && script.bulletDuration == 0 },
            };

            foreach (var reference in references)
            {
                if (reference.Value)
                {
                    missingReferences.Add(reference.Key);
                }
            }

            return (missingReferences.Count > 0, missingReferences);
        }

        // 开始一个带标题的 HelpBox 样式分组框。
        private void BeginBox(string title)
        {
            EditorGUILayout.BeginVertical(GUI.skin.GetStyle("HelpBox"));
            EditorGUI.indentLevel++;
            EditorGUILayout.Space(5f);
            if (!string.IsNullOrEmpty(title)) EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
        }

        // 结束分组框（与 BeginBox 配对）。
        private void EndBox()
        {
            EditorGUILayout.Space(10f);
            EditorGUI.indentLevel--;
            EditorGUILayout.EndVertical();
        }

        // 绘制后坐力曲线图（默认/瞄准/合并曲线及标签）。
        private void PlotRecoil(Weapon_SO myScript)
        {
            GUILayout.BeginHorizontal();

            GUILayout.BeginVertical(GUILayout.Width(140));

            if (GUILayout.Button(showDefaultRecoilPlot ? "Hide Default Recoil Plot" : "Show Default Recoil Plot", GUILayout.Height(30)))
                showDefaultRecoilPlot = !showDefaultRecoilPlot;
            if (GUILayout.Button(showAimedRecoilPlot ? "Hide Aimed Recoil Plot" : "Show Aimed Recoil Plot", GUILayout.Height(30)))
                showAimedRecoilPlot = !showAimedRecoilPlot;
            if (GUILayout.Button(showCombinedPlot ? "Hide Combined Plot" : "Show Combined Plot", GUILayout.Height(30)))
                showCombinedPlot = !showCombinedPlot;
            if (GUILayout.Button(showLabels ? "Hide Labels" : "Show Labels", GUILayout.Height(30)))
                showLabels = !showLabels;
            GUILayout.Label($"Simulated Shots: {myScript.magazineSize}");
            GUILayout.Label($"X Recoil Reduction Ratio: {(myScript.applyDifferentRecoilOnAiming ? (myScript.xRecoilAmount / myScript.xRecoilAmountOnAiming) : 1):F2}");
            GUILayout.Label($"X Recoil Reduction Ratio: {(myScript.applyDifferentRecoilOnAiming ? (myScript.yRecoilAmount / myScript.yRecoilAmountOnAiming) : 1):F2}");
            GUILayout.EndVertical();

            GUILayout.BeginHorizontal();

            if (showDefaultRecoilPlot)
            {
                GUILayout.BeginVertical(GUILayout.Width(200));
                CowsinsEditorWindowUtilities.DrawRecoilPlot(myScript, false, "Default Recoil", false, showLabels);
                GUILayout.EndVertical();
            }

            if (showAimedRecoilPlot)
            {
                GUILayout.BeginVertical(GUILayout.Width(200));
                CowsinsEditorWindowUtilities.DrawRecoilPlot(myScript, myScript.applyDifferentRecoilOnAiming, "Aimed Recoil", true, showLabels);
                GUILayout.EndVertical();
            }

            if (showCombinedPlot)
            {
                GUILayout.BeginVertical(GUILayout.Width(200));
                CowsinsEditorWindowUtilities.DrawCombinedRecoilPlot(myScript, showLabels);
                GUILayout.EndVertical();
            }

            GUILayout.EndHorizontal();

            GUILayout.EndHorizontal();
        }

        // 渲染瞄准（ADS）设置分组。
        private void DrawAimSettings(Weapon_SO myScript)
        {
            BeginBox("AIM / ADS SETTINGS");

            var allowAimProperty = serializedObject.FindProperty("allowAim");
            EditorGUILayout.PropertyField(allowAimProperty);

            EditorGUILayout.PropertyField(serializedObject.FindProperty("ignoreAimPoint"));

            using (var group = new EditorGUILayout.FadeGroupScope(Convert.ToSingle(myScript.allowAim)))
            {
                if (group.visible == true)
                {
                    EditorGUI.indentLevel++;
                    if (myScript.applyBulletSpread)
                    {
                        EditorGUI.indentLevel++;
                        EditorGUILayout.LabelField("Check new options under spread options. ", EditorStyles.helpBox);
                        EditorGUI.indentLevel--;
                    }
                    EditorGUI.indentLevel++;
                    var aimingPositionProperty = serializedObject.FindProperty("aimDistance");
                    EditorGUILayout.PropertyField(aimingPositionProperty);
                    var aimingRotationProperty = serializedObject.FindProperty("aimingRotation");
                    EditorGUILayout.PropertyField(aimingRotationProperty);
                    var aimingSpeedProperty = serializedObject.FindProperty("aimingSpeed");
                    EditorGUILayout.PropertyField(aimingSpeedProperty);
                    var aimingFOVProperty = serializedObject.FindProperty("aimingFOV");
                    EditorGUILayout.PropertyField(aimingFOVProperty);

                    EditorGUILayout.PropertyField(serializedObject.FindProperty("allowAimingIfReloading"));

                    EditorGUILayout.PropertyField(serializedObject.FindProperty("setMovementSpeedWhileAiming"));

                    if (myScript.setMovementSpeedWhileAiming)
                    {
                        EditorGUI.indentLevel++;
                        EditorGUILayout.PropertyField(serializedObject.FindProperty("movementSpeedWhileAiming"));
                        EditorGUI.indentLevel--;
                    }

                    EditorGUI.indentLevel--;
                }
            }
            EndBox();
        }
    }

    #endregion
#endif
}