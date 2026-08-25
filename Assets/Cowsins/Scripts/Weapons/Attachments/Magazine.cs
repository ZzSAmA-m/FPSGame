using UnityEngine;
// ===========================================================================================
// Magazine.cs —— 弹匣配件脚本
// 职责：定义武器“弹匣”配件，继承自 Attachment 基类。装备时在武器基础弹匣容量上叠加
//       容量增量并限制剩余子弹数，卸载时恢复原始容量，随后交由基类处理通用流程。
// ===========================================================================================
namespace cowsins
{
    /// <summary>
    /// 弹匣配件：继承 Attachment 基类，通过 magazineCapacityAdded 增减武器弹匣容量。
    /// </summary>
    public class Magazine : Attachment
    {
        // magazineCapacityAdded：弹匣容量增量，装备时叠加到武器默认弹匣容量上（容量更小时可设为负值）
        [Title("Magazine")]
        [Tooltip("Capacity to add to the default magazine. If this magazine supports less bullets, set a negative value.")] public int magazineCapacityAdded;

        /// <summary>
        /// 装备弹匣：把容量增量叠加到武器弹匣容量上，并把当前剩余子弹数限制在容量内，
        /// 最后调用基类 Attach 完成通用装备流程。
        /// </summary>
        public override void Attach(WeaponIdentification Id)
        {
            Id.magazineSize += magazineCapacityAdded;
            ClampBulletsToMagazineCapacity(Id);

            base.Attach(Id);
        }

        /// <summary>
        /// 卸载弹匣：把武器弹匣容量恢复为武器原始值并重新限制剩余子弹数，
        /// 最后调用基类 Dettach 完成通用卸载流程。
        /// </summary>
        public override void Dettach(WeaponIdentification Id)
        {
            Id.magazineSize = Id.weapon.magazineSize;
            ClampBulletsToMagazineCapacity(Id);

            base.Dettach(Id);
        }

        // 把当前弹匣剩余子弹数限制在不超过弹匣容量（防止容量减少后剩余子弹溢出）
        private void ClampBulletsToMagazineCapacity(WeaponIdentification Id)
        {
            if (Id.bulletsLeftInMagazine > Id.magazineSize) Id.bulletsLeftInMagazine = Id.magazineSize;
        }
    }
}