using UnityEngine;
#if INVENTORY_PRO_ADD_ON
using cowsins.Inventory;
#endif
// ===========================================================================================
// AttachmentIdentifier_SO.cs —— 配件标识符（ScriptableObject）
// 职责：以数据资产形式定义某一种配件的类型及其对武器各项属性的修正量（伤害、射速、扩散、
//       瞄准、换弹、重量、镜头晃动、穿透等），供武器引用以复用同一配件定义。
//       可选集成 Inventory Pro 插件：在背包中“使用”该配件时将其装备到当前武器。
// ===========================================================================================
namespace cowsins
{
    [CreateAssetMenu(fileName = "NewAttachmentIdentifier", menuName = "COWSINS/New Attachment Identifier", order = 2)]
    /// <summary>
    /// 配件标识符数据资产：继承 Item_SO，保存配件类型与属性修正数值，
    /// 供 Attachment 在装备 / 卸载时读取以计算武器属性变化。
    /// </summary>
    public class AttachmentIdentifier_SO : Item_SO
    {
        #region PROPERTIES
        // 配件类型：决定该配件属于哪个配件槽位（枪管 / 瞄准镜 / 枪托等）
        public AttachmentType attachmentType;
        // 换弹速度修正量（百分比，正数加快、负数减慢）
        [Range(-1f, 1f), Tooltip("Percentage added to the base reload speed")] public float reloadSpeedIncrease;
        // 瞄准速度修正量（百分比）
        [Range(-1f, 1f), Tooltip("Percentage added to the base aim speed")] public float aimSpeedIncrease;
        // 扩散减少量：用于降低武器基础扩散
        [Range(-.2f, .2f), Tooltip("Spread amount to reduce the base spread")] public float spreadDecrease = 0;
        // 射速修正量（百分比，正数表示降低射击间隔、提升射速）
        [Range(-1f, 1f), Tooltip("Percentage added to the base fire rate")] public float fireRateDecrease;
        // 伤害修正量（百分比）
        [Range(-.99f, 5f), Tooltip("Percentage added to the base damage")] public float damageIncrease;
        // 重量增量：叠加到武器重量系数上（若使武器更轻，设为负值）
        [Tooltip("Weight to add to the weapon. If it weighs less, set a negative value.")] public float weightAdded;
        // 镜头晃动倍率修正（百分比）
        [Range(-1f, 1f)] public float cameraShakeMultiplier;
        // 穿透修正量（百分比）
        [Range(-1f, 1f), Tooltip("Percentage added to the base penetration")] public float penetrationIncrease;
        #endregion

#if INVENTORY_PRO_ADD_ON
        /// <summary>
        /// 背包中使用该配件：先校验当前是否持有武器、该配件是否兼容，
        /// 再将其装备到当前武器，最后清空背包中该配件所占的格子区域。
        /// </summary>
        public override void Use(InventoryProManager inventoryProManager, InventorySlot slot)
        {
            // 当前没有持有武器（武器标识为空）时，提示不兼容并返回
            if (inventoryProManager._WeaponController.Id == null) 
            {
                ToastManager.Instance?.ShowToast(ToastManager.Instance.AttachmentNotCompatibleMsg);
                return;
            }

            // 通过工具方法查找与当前武器匹配的配件实例（success 表示是否兼容）
            (bool success, Attachment attachment, int attachmentIdentifier) = CowsinsUtilities.CompatibleAttachment(inventoryProManager._WeaponController.Weapon.weaponObject, this);
            if (!success)
            {
                // 不兼容：弹出提示并返回
                ToastManager.Instance?.ShowToast("This attachment is not compatible");
                return;
            }
            // 兼容：把配件实例装备到当前武器
            inventoryProManager._WeaponController.attachmentsSystem.AssignAttachmentToWeapon(attachment, inventoryProManager._WeaponController.CurrentWeaponIndex);
            // 清空背包中该配件占用的格子区域
            inventoryProManager._GridGenerator.Operations.ClearSlotArea(slot);
        }
#endif
    }
}