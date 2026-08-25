using UnityEngine;

// ===========================================================================================
// Attachment.cs —— 武器配件基类
// 职责：所有武器配件（枪管、瞄准镜、枪托、握把、弹匣、手电筒、激光器）的抽象基类。
//       定义配件通用的装备 / 卸载流程：激活 / 隐藏配件物体、根据配件标识符修正武器各项
//       属性，并把当前配件状态记录到武器的 AttachmentStateManager 中。具体配件子类可重写
//       Attach / Dettach 与 AttachmentAction 以扩展专属行为。
// ===========================================================================================
namespace cowsins
{
    /// <summary>
    /// 武器配件抽象基类：提供装备（Attach）、卸载（Dettach）、主动动作（AttachmentAction）
    /// 与属性修正（ModifyStats）的通用实现。派生类（如 Barrel、Scope）继承并可按需重写。
    /// </summary>
    public abstract class Attachment : MonoBehaviour
    {
        // attachmentIdentifier：配件标识符（ScriptableObject），记录配件类型与各项属性修正量；
        // 不同武器可共享同一标识符以复用同一配件定义。
        [Title("Basic")]
        [Tooltip("Identifier of the attachment. You can have the same attachment within different weapons as long as they share this attachment identifier" +
                "scriptable object.")]
        public AttachmentIdentifier_SO attachmentIdentifier;

        /// <summary>
        /// 装备配件：激活配件物体，校验标识符后按正方向（+1）修正武器属性，
        /// 并把该配件登记为武器对应类型的当前配件。
        /// </summary>
        public virtual void Attach(WeaponIdentification Id)
        {
            this.gameObject.SetActive(true);

            if (attachmentIdentifier == null)
            {
                Debug.LogError($"<color=red>[COWSINS]</color> Attachment Identifier not configured in {this.name}");
                return;
            }

            ModifyStats(Id, 1);
            Id.AttachmentState.SetCurrent(attachmentIdentifier.attachmentType, this);
        }

        /// <summary>
        /// 卸载配件：隐藏配件物体，校验标识符后按反方向（-1）回退武器属性，
        /// 并清除武器对应类型的当前配件记录。
        /// </summary>
        public virtual void Dettach(WeaponIdentification Id)
        {
            this.gameObject.SetActive(false);

            if (attachmentIdentifier == null)
            {
                Debug.LogError($"<color=red>[COWSINS]</color> Attachment Identifier not configured in {this.name}");
                return;
            }

            ModifyStats(Id, -1);
            Id.AttachmentState.RemoveCurrent(attachmentIdentifier.attachmentType);
        }

        /// <summary>
        /// 配件主动动作（如手电筒开关、激光开关）。基类为空实现，由子类按需重写。
        /// </summary>
        public virtual void AttachmentAction() { }

        // 根据配件标识符中的属性修正量，按 direction（装备为 +1、卸载为 -1）方向修正武器各项属性。
        // 修正量均为“基础值 × 标识符百分比”，因此卸载时用相反方向即可精确还原。
        private void ModifyStats(WeaponIdentification Id, int direction)
        {
            Weapon_SO weapon = Id.weapon;
            // 伤害：按基础单发伤害的百分比增减
            Id.damage += direction * (weapon.damagePerBullet * attachmentIdentifier.damageIncrease);
            // 射速：按基础射速的百分比增减（fireRate 表示射击间隔，故用减号表示“提升射速”）
            Id.fireRate -= direction * (weapon.fireRate * attachmentIdentifier.fireRateDecrease);
            // 腰射扩散：按基础扩散量减少
            Id.baseSpread -= direction * (weapon.spreadAmount * attachmentIdentifier.spreadDecrease);
            // 瞄准扩散：按基础瞄准扩散量减少
            Id.baseAimSpread -= direction * (weapon.aimSpreadAmount * attachmentIdentifier.spreadDecrease);
            // 瞄准速度：按基础瞄准速度的百分比增减
            Id.aimSpeed += direction * (weapon.aimingSpeed * attachmentIdentifier.aimSpeedIncrease);
            // 换弹时间：按基础换弹时间的百分比增减
            Id.reloadTime += direction * (weapon.reloadTime * attachmentIdentifier.reloadSpeedIncrease);
            // 空仓换弹时间：同样按百分比增减
            Id.emptyReloadTime += direction * (weapon.emptyReloadTime * attachmentIdentifier.reloadSpeedIncrease);
            // 重量系数：按配件重量增量增减
            Id.weightMultiplier += direction * (weapon.weightMultiplier * attachmentIdentifier.weightAdded);
            // 镜头晃动：按基础晃动量的百分比增减
            Id.camShakeAmount += direction * (weapon.camShakeAmount * attachmentIdentifier.cameraShakeMultiplier);
            // 穿透：按基础穿透量的百分比增减
            Id.penetrationAmount += direction * (weapon.penetrationAmount * attachmentIdentifier.penetrationIncrease);
        }
    }
}