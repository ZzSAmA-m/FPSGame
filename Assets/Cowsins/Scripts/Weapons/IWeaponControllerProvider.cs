// IWeaponControllerProvider.cs
// 定义武器控制器对外暴露的多个“提供者”接口集合。
// 这些接口由 WeaponController 实现，并被 PlayerDependencies 等系统引用，
// 用于解耦武器引用、武器行为状态、后坐力与武器事件之间的依赖关系。
using System.Collections.Generic;
using UnityEngine;

namespace cowsins
{
    // Implemented by WeaponController and required by PlayerDependencies
    /// <summary>提供武器引用信息(主摄像机、当前武器、武器身份标识与库存)的接口。</summary>
    public interface IWeaponReferenceProvider
    {
        // 玩家主摄像机，作为射击射线起点与散布计算的基准
        public Camera MainCamera { get; }
        // 当前持有的武器数据(ScriptableObject)
        Weapon_SO Weapon { get; set; }
        // 当前武器的身份标识(含伤害、射速、枪口点等运行时数据)
        WeaponIdentification Id { get; set; }
        // 当前武器在库存数组中的索引
        public int CurrentWeaponIndex {  get; set; }
        // 玩家武器库存数组
        public WeaponIdentification[] Inventory { get; set; }
    }
    /// <summary>提供武器行为状态(瞄准、换弹、射击等)以及武器增补/交换操作的接口。</summary>
    public interface IWeaponBehaviourProvider
    {
        // 是否处于瞄准状态
        bool IsAiming { get; set; }
        // 是否正在换弹
        bool IsReloading { get; set; }
        // 是否正在射击
        bool IsShooting { get; set; }
        // 是否处于备用瞄准(如侧身/替代瞄准)状态
        bool AlternateAiming { get; }
        // 近战攻击当前是否可用
        bool IsMeleeAvailable { get; set;  }
        // 瞄准时的相机抖动倍率
        float AimingCamShakeMultiplier { get; }
        // 蹲伏时的相机抖动倍率
        float CrouchingCamShakeMultiplier { get; set; }

        /// <summary>为已拥有的重复武器补充弹药。</summary>
        /// <param name="amount">要补充的弹药数量。</param>
        /// <returns>是否成功补充弹药。</returns>
        bool AddDuplicateWeaponAmmo(int amount);
        /// <summary>尝试将新武器及其弹药、配件加入玩家武器库。</summary>
        /// <param name="weapon">要加入的武器 ScriptableObject。</param>
        /// <param name="currentBullets">新武器当前弹匣内的弹药数。</param>
        /// <param name="totalBullets">新武器的备弹总数。</param>
        /// <param name="attachments">该武器携带的配件列表。</param>
        /// <returns>是否成功加入武器。</returns>
        bool TryToAddWeapons(Weapon_SO weapon, int currentBullets, int totalBullets, List<AttachmentIdentifier_SO> attachments);
        /// <summary>用新武器交换当前武器，并返回被换下的旧武器及其弹药。</summary>
        /// <param name="weapon">用于交换的新武器 ScriptableObject。</param>
        /// <param name="currentBullets">新武器当前弹匣内的弹药数。</param>
        /// <param name="totalBullets">新武器的备弹总数。</param>
        /// <param name="attachments">新武器携带的配件列表。</param>
        /// <returns>返回由(被换下的旧武器, 其弹匣弹药数, 其备弹总数)组成的三元组。</returns>
        (Weapon_SO, int, int) SwapWeapons(Weapon_SO weapon, int currentBullets, int totalBullets, List<AttachmentIdentifier_SO> attachments);

    }
    /// <summary>提供武器后坐力俯仰与偏航偏移量的接口。</summary>
    public interface IWeaponRecoilProvider
    {
        // 后坐力俯仰(上下)偏移量
        float RecoilPitchOffset { get; }
        // 后坐力偏航(左右)偏移量
        float RecoilYawOffset { get; }
    }
    /// <summary>提供武器控制器事件集引用，供外部订阅射击、命中等事件的接口。</summary>
    public interface IWeaponEventsProvider
    {
        // 武器控制器事件集，用于订阅射击/命中等事件
        WeaponControllerEvents Events { get; }
    }
}