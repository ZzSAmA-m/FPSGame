// ===================================================================
// WeaponControllerEvents.cs —— 武器控制器事件中心。
// 集中声明并初始化武器系统在整个生命周期中广播的所有 UnityEvent 事件，
// 以及一个用于向订阅者“索取”数据的 Func 委托（OnGetSpread）。
// 这些事件覆盖：系统初始化、射击、命中检测、弹药/换弹、瞄准、武器选择、
// 库存/附件、敌情检测与副攻击（近战）等环节，供 UI、音效、特效等模块统一订阅，
// 从而避免各脚本之间建立硬引用，降低耦合度。
// ===================================================================
using UnityEngine;
using UnityEngine.Events;
using System.Collections.Generic;
using System;

namespace cowsins
{
    /// <summary>
    /// 武器控制器事件集合类（事件中心）。
    /// 把所有与武器相关的对外事件集中到一个类中统一声明与初始化，
    /// 由 WeaponController 在合适时机触发，其它系统订阅以响应相应行为。
    /// </summary>
    public class WeaponControllerEvents
    {
        // INITIALIZATION
        // 武器系统初始化完成时触发；int 参数为当前已持有的武器数量。
        public UnityEvent<int> OnInitializeWeaponSystem = new UnityEvent<int>();

        // SHOOTING
        // 玩家开火时触发（无参数）。
        public UnityEvent OnShoot = new UnityEvent();
        // 开火时请求相机抖动；float 参数为抖动强度。
        public UnityEvent<float> OnShootShake = new UnityEvent<float>();
        // 开火时应用 FOV（视野）变化；float 参数为 FOV 变化量。
        public UnityEvent<float> OnShootApplyFOV = new UnityEvent<float>();
        // 开火时生成枪口/开火特效（无参数）。
        public UnityEvent OnShootSpawnEffects = new UnityEvent();
        // 开火时执行命中扫描（hitscan）或发射投射物（projectile）（无参数）。
        public UnityEvent OnShootHitscanProjectile = new UnityEvent();

        // Damage / Hit Detection
        // 命中目标时触发；参数依次为：伤害值(int)、伤害倍率(float)、命中信息(RaycastHit)、是否暴击(bool)。
        public UnityEvent<int, float, RaycastHit, bool> OnHit = new UnityEvent<int, float, RaycastHit, bool>();
        // 生成弹孔/命中特效时触发；参数为伤害值(int)与命中信息(RaycastHit)。
        public UnityEvent<int, RaycastHit> OnInstantiateBulletHoleImpact = new UnityEvent<int, RaycastHit>();

        // AMMO / RELOAD
        // 扣除弹药时触发（无参数）。
        public UnityEvent OnReduceAmmo = new UnityEvent();
        // 弹药数量变化时触发；bool 参数表示弹药是否已耗尽。
        public UnityEvent<bool> OnAmmoChanged = new UnityEvent<bool>();
        // 开始换弹时触发（无参数）。
        public UnityEvent OnStartReload = new UnityEvent();
        // 换弹被取消时触发（无参数）。
        public UnityEvent OnCancelReload = new UnityEvent();
        // 换弹完成时触发（无参数）。
        public UnityEvent OnFinishReload = new UnityEvent();
        // 换弹 UI 状态变化时触发；两个 bool 参数含义由调用方约定（通常为是否显示换弹 UI 等）。
        public UnityEvent<bool, bool> OnReloadUIChanged = new UnityEvent<bool, bool>();
        // 武器过热后进入冷却时触发（无参数）。
        public UnityEvent OnWeaponCooling = new UnityEvent();

        // AIMING
        // 开始瞄准时触发；float 参数为瞄准速度。
        public UnityEvent<float> OnAimStart = new UnityEvent<float>();
        // 持续瞄准过程中触发（无参数）。
        public UnityEvent OnAiming = new UnityEvent();
        // 停止瞄准时触发（无参数）。
        public UnityEvent OnAimStop = new UnityEvent();

        // WEAPON SELECTION
        // 选中武器时触发（无参数）。
        public UnityEvent OnSelectWeapon = new UnityEvent();
        // 装备武器时触发；参数为被装备武器的 WeaponIdentification 组件。
        public UnityEvent<WeaponIdentification> OnEquipWeapon = new UnityEvent<WeaponIdentification>();
        // 正在切换武器时触发（无参数）。
        public UnityEvent OnSwitchingWeapon = new UnityEvent();
        // 收枪/掏枪（Unholster）时触发；两个 bool 参数含义由调用方约定（通常与是否立即/是否静音有关）。
        public UnityEvent<bool, bool> OnUnholster = new UnityEvent<bool, bool>();
        // 取消选中当前武器时触发（无参数）。
        public UnityEvent OnUnselectingWeapon = new UnityEvent();
        // 释放/丢弃武器时触发（无参数）。
        public UnityEvent OnReleaseWeapon = new UnityEvent();

        // INVENTORY / ATTACHMENTS
        // 武器库存变化时触发；参数为库存槽位索引(int)与对应武器 ScriptableObject(Weapon_SO)。
        public UnityEvent<int, Weapon_SO> OnWeaponInventoryChanged = new UnityEvent<int, Weapon_SO>();
        // 为武器分配附件时触发；参数依次为：武器标识(WeaponIdentification)、槽位索引(int)、附件列表(List&lt;AttachmentIdentifier_SO&gt;)。
        public UnityEvent<WeaponIdentification, int, List<AttachmentIdentifier_SO>> OnAssignAttachmentsToWeapon =
            new UnityEvent<WeaponIdentification, int, List<AttachmentIdentifier_SO>>();

        // ENEMY DETECTION
        // 发现/丢失敌人时触发；bool 参数表示是否发现敌人。
        public UnityEvent<bool> OnEnemySpotted = new UnityEvent<bool>();

        // SECONDARY ATTACK
        // 触发副攻击（近战）时触发；参数为与近战判定相关的 Transform。
        public UnityEvent<Transform> OnSecondaryAttack = new UnityEvent<Transform>();

        // DATA REQUESTS
        // 数据请求委托：供订阅者返回当前武器散布值（float）。
        public event Func<float> OnGetSpread;
        /// <summary>
        /// 请求当前武器散布值：调用 OnGetSpread 委托，由订阅者返回散布值；
        /// 若没有任何订阅者（委托为 null），则回退返回默认值 0f。
        /// </summary>
        /// <returns>当前散布值（float）；无订阅者时为 0f。</returns>
        public float RequestSpread()
        {
            // If no listeners are subscribed, return default value
            return OnGetSpread?.Invoke() ?? 0f;
        }
    }

}