// ===================================================================
// WeaponIdentification.cs —— 武器实例的运行时组件。
// 挂载在具体武器物体上，作为 WeaponController 与武器 ScriptableObject（Weapon_SO）
// 之间的桥梁：读取并缓存武器的基础属性（伤害、射速、散布、换弹时间等），
// 维护运行时弹药/附件状态（弹匣、总弹量、热量、附件状态管理器），
// 并封装射击、扣减弹药、选择射击动画/音效等核心逻辑。
// ===================================================================
/// <summary>
/// This script belongs to cowsins� as a part of the cowsins� FPS Engine. All rights reserved. 
/// </summary>
using UnityEngine;  
using System.Collections.Generic;
using UnityEditor;

namespace cowsins
{
    /// <summary>
    /// 武器标识与运行时逻辑组件（中文说明）：
    /// 挂载到具体武器物体上，从 Weapon_SO 读取静态数据并在运行时缓存，
    /// 维护弹药/附件状态，并提供射击、扣减弹药、选择射击动画与音效等接口。
    /// 该组件会被放入 WeaponController 的武器数组中，由武器控制器统一驱动。
    /// </summary>
    /// <summary>
    /// Attach this to your weapon object ( the one that goes in the weapon array of WeaponController )
    /// </summary>
    public class WeaponIdentification : MonoBehaviour
    {
        // 该武器对应的静态数据 ScriptableObject（伤害、射速、弹匣容量等）。
        public Weapon_SO weapon;

        // 枪口（开火点）数组：子弹射出的位置；除近战外每个武器都应有至少一个 firePoint。
        [Tooltip("Every weapon, excluding melee, must have a firePoint, which is the point where the bullet comes from." +
            "Just make an empty object, call it firePoint for organization purposes and attach it here. ")]
        public Transform[] FirePoint;

        // 弹壳弹出点（可选）；为空时回退使用 FirePoint。
        [Tooltip("Optional. Reference to the point where bullet shells will be ejected. If left empty, the FirePoint will be used instead.")]
        public Transform shellEjectPoint;

        // 瞄准参考点：瞄准时用于对齐视线的位置。
        public Transform aimPoint;

        // 运行时弹药状态：总弹匣数、单个弹匣容量、当前弹匣剩余子弹、总备弹量。
        [HideInInspector] public int totalMagazines, magazineSize, bulletsLeftInMagazine, totalBullets;

        // 头部骨骼引用：武器动画带动相机时用于对齐头部（可为空）。
        [SerializeField] private Transform headBone; 

        // 默认附件配置：首次拾取该武器时自动装备的附件。
        [Tooltip("Defines the default attachments for your weapon. The first time you pick it up, these attachments will be equipped."), SerializeField] private DefaultAttachment defaultAttachments;

        // 该武器可装备的全部附件（兼容附件）配置。
        [Tooltip("Defines all the attachments that can be equipped on your weapon.")] public CompatibleAttachments compatibleAttachments;

        // 附件状态管理器：在运行时跟踪每个附件槽位的当前状态（默认/自定义/空）。
        private AttachmentStateManager attachmentStateManager;

        // 瞄准点的初始本地位置与旋转（欧拉角），用于在瞄准结束后恢复原位。
        [HideInInspector] public Vector3 originalAimPointPos, originalAimPointRot;

        // 武器热量比例（0~1）：用于过热式换弹风格。
        [HideInInspector] public float heatRatio;

        // 扣减弹药方式的委托类型，用于在默认/过热两种扣弹逻辑间切换。
        private delegate void ReduceAmmoStyle();

        // 当前使用的扣减弹药方法实例（默认式或过热式）。
        private ReduceAmmoStyle reduceAmmo;

        // 射击行为接口实例：定义该武器如何开火（由 SetShootStyle 注入）。
        private IShootStyle shootBehaviour;
        // 只读属性：对外暴露头部骨骼引用。
        public Transform HeadBone => headBone;

        // 该武器（含子物体）上的 Animator 组件引用。
        private Animator animator;
        // 只读属性：对外暴露 Animator。
        public Animator Animator => animator;

        // 单发伤害值（运行时缓存自 Weapon_SO.damagePerBullet）。
        public float damage;
        // 射速（运行时缓存自 Weapon_SO.fireRate）。
        public float fireRate;
        // 基础散布值（不开镜时的子弹散布；未启用散布时为 0）。
        public float baseSpread;
        // 瞄准（开镜）时的散布值。
        public float baseAimSpread;
        // 瞄准速度。
        public float aimSpeed;
        // 换弹耗时（弹匣仍有剩余子弹时的战术换弹）。
        public float reloadTime;
        // 空仓换弹耗时（弹匣打空后的完整换弹）。
        public float emptyReloadTime;
        // 重量系数：影响移动速度等。
        public float weightMultiplier;
        // 开火时相机抖动强度。
        public float camShakeAmount;
        // 子弹穿透能力（可穿透的层数/厚度）。
        public float penetrationAmount;

        // 瞄准时武器/相机的目标旋转偏移。
        public Vector3 aimingRotation;
        // 瞄准时武器的位置偏移。
        public Vector3 aimingOffset;

        // 枪口特效预制体（开火时生成）。
        public GameObject muzzleVFX;
        // 开火音效数组（随机或按动画索引选择）。
        public AudioClip[] fireSFXs;

        // 组件启用时记录瞄准点的初始本地位置与旋转（欧拉角），
        // 供瞄准结束后恢复原位使用。
        private void OnEnable()
        {
            originalAimPointPos = aimPoint.localPosition;
            originalAimPointRot = aimPoint.localRotation.eulerAngles;
        }
        // 初始化函数：在物体首次激活时执行。
        // 1) 获取子物体上的 Animator，并让其在禁用后保持状态不重置；
        // 2) 从 Weapon_SO 读取总弹匣数等静态配置；
        // 3) 创建附件状态管理器；
        // 4) 依据换弹风格选择对应的扣减弹药方法（默认式/过热式）；
        // 5) 缓存武器各项运行时属性（伤害、射速、散布、换弹时间、特效、音效、弹匣容量等）。
        private void Awake()
        {
            animator = GetComponentInChildren<Animator>(true); 
            if(animator) animator.keepAnimatorStateOnDisable = true;

            totalMagazines = weapon.totalMagazines;

            // Initialize attachment state manager for this weapon
            attachmentStateManager = new AttachmentStateManager(this, defaultAttachments, compatibleAttachments);

            if(weapon.reloadStyle == ReloadingStyle.defaultReload)
                reduceAmmo = ReduceDefaultAmmo;
            else 
                reduceAmmo = ReduceOverheatAmmo;

            damage = weapon.damagePerBullet;
            fireRate = weapon.fireRate;
            baseSpread = weapon.applyBulletSpread ? weapon.spreadAmount : 0;
            baseAimSpread = weapon.applyBulletSpread ? weapon.aimSpreadAmount : 0;
            aimSpeed = weapon.aimingSpeed;
            reloadTime = weapon.reloadTime;
            emptyReloadTime = weapon.emptyReloadTime;
            weightMultiplier = weapon.weightMultiplier;
            camShakeAmount = weapon.camShakeAmount;
            penetrationAmount = weapon.penetrationAmount;

            aimingOffset = Vector3.zero;
            aimingRotation = weapon.aimingRotation;

            muzzleVFX = weapon.muzzleVFX;
            fireSFXs = weapon.audioSFX.shooting;
            magazineSize = weapon.magazineSize;
        }

        /// <summary>
        /// 注入该武器的射击行为（实现 IShootStyle 接口的对象）。
        /// 射击逻辑（如命中扫描/投射物）由外部实现，武器组件只负责调用。
        /// </summary>
        /// <param name="shootBehaviour">要赋值的射击行为实例。</param>
        public void SetShootStyle(IShootStyle shootBehaviour) => this.shootBehaviour = shootBehaviour;

        /// <summary>
        /// 执行一次射击：先随机选择本次射击动画索引（PickNextShot），
        /// 再调用注入的射击行为执行实际开火。
        /// </summary>
        /// <param name="spread">本次射击应用的散布值。</param>
        /// <param name="damageMultiplier">伤害倍率。</param>
        /// <param name="shakeMultiplier">相机抖动倍率。</param>
        public void Shoot(float spread, float damageMultiplier, float shakeMultiplier)
        {
            PickNextShot();
            shootBehaviour?.Shoot(spread, damageMultiplier, shakeMultiplier);
        }

        /// <summary>
        /// 扣减弹药入口：调用当前绑定的扣弹方法（默认式或过热式），
        /// 若未绑定则不做任何操作。
        /// </summary>
        public void ReduceAmmo() => reduceAmmo?.Invoke();   

        /// <summary>
        /// 过热式扣减弹药：每开一枪按 1/弹匣容量 的比例累加热量值（heatRatio），
        /// 当热量达到 1 时视为“过热”，交由换弹/冷却逻辑处理。
        /// </summary>
        public void ReduceOverheatAmmo()
        {
            heatRatio += (float)1f / magazineSize;
        }

        /// <summary>
        /// 默认式扣减弹药：若非无限子弹，则按 Weapon_SO.ammoCostPerFire 扣除
        /// 当前弹匣剩余子弹，并确保子弹数不为负。
        /// </summary>
        public void ReduceDefaultAmmo()
        {
            if (!weapon.infiniteBullets)
            {
                bulletsLeftInMagazine -= weapon.ammoCostPerFire;
                if (bulletsLeftInMagazine < 0)
                {
                    bulletsLeftInMagazine = 0;
                }
            }
        }

        /// <summary>
        /// 当前（最近一次）选择的射击动画索引，用于匹配射击动画与音效。
        /// </summary>
        public int CurrentShotIndex { get; private set; }

        /// <summary>
        /// 在 [0, weapon.amountOfShootAnimations) 范围内随机选取本次射击动画索引，
        /// 用于在多种射击动画/音效之间随机切换以增加变化感。
        /// </summary>
        public void PickNextShot()
        {
            CurrentShotIndex = Random.Range(0, weapon.amountOfShootAnimations);
        }

        /// <summary>
        /// 根据当前射击动画索引返回对应的射击动画状态名。
        /// 只有一个动画时固定返回 "shooting"；多个动画时按 "shooting"、"shooting2"… 命名。
        /// </summary>
        /// <returns>射击动画状态名（string）。</returns>
        public string GetCurrentShotAnimation()
        {
            if (weapon.amountOfShootAnimations <= 1) return "shooting";
            return CurrentShotIndex == 0 ? "shooting" : "shooting" + (CurrentShotIndex + 1).ToString();
        }

        /// <summary>
        /// 返回本次射击应播放的音效。逻辑：
        /// 1) 音效数组为空时返回 null；
        /// 2) 若音效数量与射击动画数量一致且多于 1 个，则按当前动画索引 1:1 配对返回；
        /// 3) 否则从数组中随机选择一个以增加变化。
        /// </summary>
        /// <returns>本次射击音效（AudioClip），无音效时返回 null。</returns>
        public AudioClip GetFireSFX()
        {
            if (fireSFXs == null || fireSFXs.Length == 0) return null;

            // If the amount of animations matches the amount of sounds, they are paired 1:1.
            if (fireSFXs.Length == weapon.amountOfShootAnimations && weapon.amountOfShootAnimations > 1)
                return fireSFXs[CurrentShotIndex];

            // Otherwise, pick a random sound from the array for variety.
            return fireSFXs[Random.Range(0, fireSFXs.Length)];
        }


        // Returns the attachment state manager for this weapon.
        // 返回该武器的附件状态管理器（只读访问器）。
        public AttachmentStateManager AttachmentState => attachmentStateManager;

#if UNITY_EDITOR

        #region Gizmos

        // Additional Weapon Information on the editor view
        // 编辑器 Scene 视图中用于绘制的武器近似包围盒尺寸。
        Vector3 boxSize = new Vector3(0.1841836f, 0.14f, 0.54f);
        // 包围盒相对武器位置的绘制偏移。
        Vector3 boxPosition = new Vector3(0,-.2f,.6f);

        // 编辑器辅助可视化：非运行时在 Scene 视图中绘制武器近似位置、瞄准点、
        // 各开火点与弹壳弹出点的线框与文字标签，便于在编辑器中摆放与对齐。
        private void OnDrawGizmos()
        {
            if (!Application.isPlaying)
            {
                Gizmos.color = new Color(1, 0, 0, 0.3f);

                Gizmos.DrawWireCube(transform.position + boxPosition, boxSize);
                Handles.Label(transform.position + boxPosition + Vector3.up * (boxSize.y / 2 + 0.1f), "Approximate Weapon Location");

                // 若已指定瞄准点，则以蓝色线框与标签标记其位置。
                if(aimPoint)
                {
                    Gizmos.color = Color.blue;
                    Gizmos.DrawWireCube(aimPoint.position , Vector3.one * .02f);
                    Handles.Label(aimPoint.position + Vector3.up * .05f, "Aim Point");

                }

                // 遍历所有开火点，以绿色线框与编号标签标记。
                for(int i = 0; i < FirePoint.Length; i++)
                {
                    if (FirePoint[i] != null)
                    {
                        Gizmos.color = Color.green;
                        Gizmos.DrawWireCube(FirePoint[i].position, Vector3.one * .02f);
                        Handles.Label(FirePoint[i].position + Vector3.up * .05f, "Fire Point " + (i + 1));

                    }
                }

                // 若已指定弹壳弹出点，则以黄色线框与标签标记。
                if (shellEjectPoint)
                {
                    Gizmos.color = Color.yellow;
                    Gizmos.DrawWireCube(shellEjectPoint.position, Vector3.one * .02f);
                    Handles.Label(shellEjectPoint.position + Vector3.up * .05f, "Shell Eject Point");
                }
            }
        }
        #endregion
#endif
    }
}
