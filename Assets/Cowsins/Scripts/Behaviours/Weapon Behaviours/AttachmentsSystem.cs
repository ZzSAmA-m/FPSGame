// AttachmentsSystem.cs：武器配件系统。
// 负责将配件（Attachment）装备到指定武器：校验兼容性、替换旧配件、丢弃非默认配件、
// 通过配件状态管理器挂载新配件，并处理手电筒开关以及批量装备默认配件。
using UnityEngine;
using System.Collections.Generic;

namespace cowsins
{
    /// <summary>
    /// 武器配件系统：管理配件的装备、替换、丢弃与切换，以及手电筒等附件的触发。
    /// </summary>
    public class AttachmentsSystem
    {
        // 武器上下文。
        private WeaponContext context;
        // 输入管理器（本系统内暂未直接使用）。
        private InputManager inputManager;
        // 武器引用提供者，提供当前武器、实例标识与库存数组。
        private IWeaponReferenceProvider weaponReference;

        // 当前武器数据（Weapon_SO）快捷属性。
        private Weapon_SO weapon => weaponReference.Weapon;
        // 当前武器实例标识快捷属性。
        private WeaponIdentification id => weaponReference.Id;
        // 武器库存数组快捷属性。
        private WeaponIdentification[] inventory => weaponReference.Inventory;

        // 武器控制器设置。
        private WeaponControllerSettings settings;

        /// <summary>
        /// 构造函数：缓存依赖引用，并订阅配件 UI 点击、装备配件以及拾取配件等事件，以触发对应的装备逻辑。
        /// </summary>
        /// <param name="context">武器上下文。</param>
        /// <param name="settings">武器控制器设置。</param>
        public AttachmentsSystem(WeaponContext context, WeaponControllerSettings settings)
        {
            this.context = context;
            this.settings = settings;

            this.inputManager = context.InputManager;
            this.weaponReference = context.Dependencies.WeaponReference;

            // Each time we click on the attachment UI, we should perform the assignment.
            UIEvents.onAttachmentUIElementClickedNewAttachment += AssignNewAttachment;
            context.Dependencies.WeaponEvents.Events.OnAssignAttachmentsToWeapon.AddListener(AssignAttachmentsToWeapon);

            context.Dependencies.InteractEvents.Events.OnAttachmentPickedUp.AddListener(AssignNewAttachment);
        }

        /// <summary>将指定配件标识装备到当前武器。</summary>
        /// <param name="attachmentIdentifier_SO">要装备的配件标识（ScriptableObject）。</param>
        public void AssignNewAttachment(AttachmentIdentifier_SO attachmentIdentifier_SO) => AssignNewAttachmentToWeapon(attachmentIdentifier_SO, weaponReference.CurrentWeaponIndex);

        /// <summary>
        /// 将指定配件标识装备到指定库存槽位的武器：先校验兼容性，不兼容则直接返回，否则执行装备。
        /// </summary>
        /// <param name="attachmentIdentifier_SO">要装备的配件标识。</param>
        /// <param name="inventoryIndex">目标武器在库存中的索引。</param>
        public void AssignNewAttachmentToWeapon(AttachmentIdentifier_SO attachmentIdentifier_SO, int inventoryIndex)
        {
            (bool compatible, Attachment newAttachment, int atcIndex) = CowsinsUtilities.CompatibleAttachment(inventory[inventoryIndex], attachmentIdentifier_SO);
            if (!compatible) return;

            AssignAttachmentToWeapon(newAttachment, inventoryIndex);
        }

        // 将给定的配件实例装备到当前武器。
        /// <summary>
        /// Equips the passed attachment.
        /// </summary>
        /// <param name="attachment">Attachment to equip.</param>
        /// <param name="attachmentID">Order ID of the attachment in the WeaponIdentification's compatible attachment array.</param>
        public void AssignNewAttachment(Attachment attachment) => AssignAttachmentToWeapon(attachment, weaponReference.CurrentWeaponIndex);

        // 将配件实例装备到指定武器：校验配置与兼容性后，停用并丢弃旧配件，再激活并挂载新配件，最后触发装备事件。
        /// <summary>
        /// Equips the passed attachment. 
        /// </summary>
        /// <param name="attachment">Attachment to equip</param>
        /// <param name="attachmentID">Order ID of the attachment to equip in the WeaponIdentification compatible attachment array.</param>
        public void AssignAttachmentToWeapon(Attachment attachment, int inventoryIndex)
        {
            if (attachment == null) return;

            if(attachment.attachmentIdentifier == null)
            {
                Debug.LogError($"<color=red>[COWSINS]</color> Attachment Identifier not configured in {attachment.name}");
                return;
            }

            WeaponIdentification curWeapon = inventory[inventoryIndex];

            (bool compatible, Attachment newAttachment, int atcIndex) = CowsinsUtilities.CompatibleAttachment(curWeapon, attachment.attachmentIdentifier);
            if (!compatible) return;

            AttachmentType type = attachment.attachmentIdentifier.attachmentType;
            AttachmentStateManager stateManager = curWeapon.AttachmentState;

            // Handle current attachment replacement using attachment state for the specific weapon
            Attachment currentAttachment = stateManager.GetCurrent(type);
            bool isCurrentDefault = stateManager.IsCurrentDefault(type);

            if (currentAttachment == newAttachment) return;

            if (currentAttachment != null)
            {
                currentAttachment.gameObject.SetActive(false);

                // Drop non default attachments
                if (!isCurrentDefault)
                {
                    context.Transform.GetComponent<InteractManager>().DropAttachment(currentAttachment, false);
                }
            }

            // Equip new attachment through state manager
            newAttachment.gameObject.SetActive(true);
            newAttachment.Attach(curWeapon);

            settings.userEvents.OnAttachAttachment?.Invoke();
        }

        /// <summary>
        /// 切换当前武器手电筒的开/关：取出当前武器的手电筒附件并执行其 AttachmentAction 动作。
        /// </summary>
        public void ToggleFlashLight()
        {
            Attachment flashlight = inventory[weaponReference.CurrentWeaponIndex]?.AttachmentState.GetCurrent(AttachmentType.Flashlight);

            if (flashlight == null) return;

            flashlight.AttachmentAction();
        }

        // 为指定武器批量装备配件：若传入列表为空则使用武器默认配件，否则逐项装备传入的配件标识。
        private void AssignAttachmentsToWeapon(WeaponIdentification weaponPicked, int inventoryIndex, List<AttachmentIdentifier_SO> attachmentsToAssign)
        {
            List<AttachmentIdentifier_SO> attachments = attachmentsToAssign == null || attachmentsToAssign.Count <= 0 ? weaponPicked.AttachmentState.GetDefaultIdentifiers() : attachmentsToAssign;
            foreach (AttachmentIdentifier_SO attachmentIdentifier in attachments)
            {
                AssignNewAttachmentToWeapon(attachmentIdentifier, inventoryIndex);
            }
        }
    }

}