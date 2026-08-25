// ===================================================================
// WeaponControllerSettings.cs —— 武器控制器的可序列化配置数据类。
// 集中保存武器系统的所有可配置参数：库存（初始武器、切换方式）、引用（相机、武器挂点）、
// 近战（伤害、范围、延迟、音效等）、换弹（自动换弹）、瞄准（按住/切换式）、命中层、
// 收枪行为，以及用户事件、撞击特效、自定义射击方法等嵌套子配置。
// 借助 [System.Serializable] 与嵌套类，使这些设置可直接在 Inspector 中编辑。
// ===================================================================
using UnityEngine;
using UnityEngine.Events;
using System.Collections.Generic; 

namespace cowsins
{
    [System.Serializable]
    /// <summary>
    /// 武器控制器设置类。作为 WeaponController 的数据容器，
    /// 集中存储武器系统的全部可配置参数与若干嵌套子配置（事件、自定义射击、撞击特效等）。
    /// </summary>
    public class WeaponControllerSettings
    {
        /// <summary>
        /// 收枪（Holster）行为枚举：决定在切换武器时何时播放收枪动画。
        /// </summary>
        public enum HolsterBehaviour
        {
            // 不播放任何收枪动画。
            None = 0,
            // 仅当选中空槽位（即收起武器）时播放收枪动画。
            OnlyWhenSelectingNull = 1,
            // 每次切换武器时都播放收枪动画。
            Always = 2
        }
        [System.Serializable]
        /// <summary>
        /// 用户可配置的事件集合：把若干 UnityEvent 暴露到 Inspector，
        /// 让用户在不写代码的情况下为射击、瞄准、换弹、近战、命中、暴击、
        /// 装备附件、收枪、库存变化、选中武器等时机挂接回调。
        /// </summary>
        public class Events
        {
            // 用户可在 Inspector 中挂接的各类事件回调（射击、瞄准、换弹、近战、命中、暴击、附件、收枪、库存、选枪）。
            public UnityEvent OnShoot, OnStartAim, OnAiming, OnStopAim, onStartReload, OnFinishReload, OnSecondaryMelee, OnHit, OnCriticalHit, 
                OnAttachAttachment, OnUnholster, OnInventorySlotChanged, OnSelectWeapon;
        }
        [System.Serializable]
        /// <summary>
        /// 自定义射击方法配置：把一个武器 ScriptableObject 与其开火事件绑定，
        /// 用于那些使用自定义射击逻辑的武器——开火时调用此处指定的方法。
        /// </summary>
        public class CustomShotMethods
        {
            // 使用自定义射击逻辑的武器 ScriptableObject。
            public Weapon_SO weapon;
            // 开火时调用的自定义事件/方法。
            public UnityEvent OnShoot;
        }
        [System.Serializable]
        /// <summary>
        /// 撞击特效条目：把某个图层名（layerName）与对应的撞击特效预制体（impact）关联。
        /// </summary>
        public class ImpactEffectEntry
        {
            // 要匹配的图层名称。
            public string layerName;
            // 命中该图层时生成的撞击特效预制体。
            public GameObject impact;

            [HideInInspector]
            // 缓存的图层索引（-1 表示尚未解析），避免运行时重复按名称查找。
            public int cachedLayerIndex = -1;
        }

        [System.Serializable]
        /// <summary>
        /// 撞击特效配置：包含一个默认撞击特效（defaultImpact），
        /// 以及按图层区分的多个撞击特效条目列表（impacts）。
        /// </summary>
        public class ImpactEffects
        {
            // 未匹配到特定图层时使用的默认撞击特效。
            public GameObject defaultImpact;

            // 按图层区分的撞击特效条目列表。
            public List<ImpactEffectEntry> impacts = new List<ImpactEffectEntry>();
        }

        // ---------- INVENTORY ----------

        // 背包可容纳的最大武器数量。
        [Tooltip("max amount of weapons you can have")] public int inventorySize;
        // 初始出生时携带的武器数组（其长度不能超过背包容量）。
        [Tooltip("An array that includes all your initial weapons.")] public Weapon_SO[] initialWeapons;
        // 是否允许使用鼠标滚轮切换武器。
        public bool allowMouseWheelWeaponSwitch;
        // 是否允许使用数字键切换武器。
        public bool allowNumberKeyWeaponSwitch;


        // ---------- REFERENCES ----------

        // 玩家主相机引用（用于瞄准、射击方向等）。
        [Tooltip("Attach your main camera")] public Camera mainCamera;
        // 相机轴心（pivot）Transform 引用，用于控制瞄准时的旋转与后坐力等。
        [Tooltip("Attach your camera pivot object")] public Transform cameraPivot;
        // 武器挂点 Transform 引用，武器实例将作为其子物体放置。
        [Tooltip("Attach your weapon holder")] public Transform weaponHolder;


        // ---------- QUICK ACTIONS (MELEE) ----------

        // 是否启用副攻击（近战）。
        public bool canMelee;
        // 近战攻击所用物体（如近战碰撞体/特效），可为空。
        public GameObject meleeObject;
        // 近战动画中用于头部骨骼参考的 Transform；若相机不随近战动画移动可留空。
        public Transform meleeHeadBone;
        // 近战攻击造成的伤害值。
        public float meleeAttackDamage;
        // 近战攻击的判定范围（距离）。
        public float meleeRange;
        // 近战攻击时相机的抖动强度。
        public float meleeCamShakeAmount;
        // 近战攻击的延迟时间（秒），用于控制动画/判定时机。
        public float meleeDelay;
        // 近战动作结束后重新启用近战的等待时间（秒）。
        public float reEnableMeleeAfterAction;
        // 近战攻击时播放的音效。
        public AudioClip meleeAudioClip;


        // ---------- RELOAD ----------

        // 是否在弹匣打空后自动换弹（无需按换弹键）。
        [Tooltip("If true you won�t have to press the reload button when you run out of bullets")] public bool autoReload;
        // 自动换弹触发前的延迟时间（秒）。
        [Tooltip("Time (in seconds) it takes to reload automatically when Auto Reload is triggered."), Min(0)] public float autoReloadDelay = .1f;
        // 是否允许在收枪过程中按换弹键进行换弹。
        [Tooltip("If true, the player will reload when pressing the reload key even if it�s currently unholstering a weapon.")] public bool allowReloadWhileUnholstering;


        // ---------- AIM ----------

        // 瞄准模式：false 为按住瞄准、松开取消；true 为切换式（点按一次进入、再点退出）。
        [Tooltip("If false, hold to aim, and release to stop aiming.")] public bool alternateAiming;


        // ---------- HIT ----------

        // 可被命中的对象所在图层（LayerMask），用于射线检测过滤。
        [Tooltip("What objects should be hit")] public LayerMask hitLayer;


        // ---------- OTHERS ----------

        [Tooltip("Controls when holster animations should be played when switching weapons.")]
        // 切换武器时播放收枪动画的时机（见 HolsterBehaviour 枚举）。
        public HolsterBehaviour holsterBehaviour = HolsterBehaviour.OnlyWhenSelectingNull;

        // 用户自定义事件集合（见 Events 类）。
        public Events userEvents;
        // 撞击特效配置（见 ImpactEffects 类）。
        public ImpactEffects impactEffects; 
        // 自定义主射击方法配置数组，用于带自定义射击逻辑的武器。
        [Tooltip("Used for weapons with custom shot method. Here, " +
            "you can attach your scriptable objects and assign the method you want to call on shoot. " +
            "Please only assign those scriptable objects that use custom shot methods, Otherwise it won�t work or you will run into issues.")]
        public CustomShotMethods[] customPrimaryShot;
    }
}