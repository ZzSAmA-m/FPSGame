// ----------------------------------------------------------------------
// AttachmentPickeable.cs
// 作用：定义“配件拾取物”。玩家与之交互时，若当前武器与该配件兼容则直接装配并触发事件，
//       否则在库存插件可用时加入物品栏；同时负责设置拾取物的图标与展示模型，
//       并判断交互是否被禁止（如武器不兼容等）。
// ----------------------------------------------------------------------
using UnityEngine;
#if INVENTORY_PRO_ADD_ON
using cowsins.Inventory;
#endif
namespace cowsins
{
    /// <summary>
    /// 配件拾取物。继承自 Pickeable，交互时若与当前武器兼容则直接装配，
    /// 否则在库存插件可用时将其加入物品栏；并负责展示外观与交互合法性判断。
    /// </summary>
    public partial class AttachmentPickeable : Pickeable
    {
        // 待拾取的配件标识资源（SaveField 表示其参与存档）。
        [Tooltip("Attachment to be picked up. Notice that attachment identifiers can be shared among attachments in different weapons."), SaveField]
        public AttachmentIdentifier_SO attachmentIdentifier;

        // 兼容判定成功后缓存的配件 ID。
        private int attachmentID;

        // 兼容判定成功后缓存的配件对象。
        private Attachment atc;
        // 只读属性：返回当前缓存的配件对象。
        public Attachment Atc => atc;

        // 初始化：若该拾取物是掉落物（dropped 为 true）则直接返回，否则加载展示外观。
        private void Start()
        {
            // If the pickeable hasnt been dropped, dont keep going
            if (dropped) return;
            GetVisuals();
        }
        // 执行配件拾取：兼容则装配并触发事件，否则在库存插件可用时加入物品栏。
        public override void Interact(Transform player)
        {
            // 配件标识为空时记录错误并跳过交互。
            if (attachmentIdentifier == null)
            {
                CowsinsUtilities.LogError("<b><color=yellow>AttachmentIdentifier_SO</color></b> " +
                "not found! Skipping Interaction.", this);
                return;
            }

            // 获取玩家的依赖组件引用。
            PlayerDependencies playerDependencies = player.GetComponent<PlayerDependencies>();
            // Reference to WeaponController
            IWeaponReferenceProvider weaponReference = playerDependencies.WeaponReference;
            IInteractEventsProvider interactEventsProvider = playerDependencies.InteractEvents;

            // 武器为空或配件与当前武器不兼容时，进入下方的库存/返回分支。
            // If the weapon is null or this is not a compatible attachment for the current unholstered weapon, return
            if (weaponReference.Weapon == null || !CheckCompatibleAttachment(weaponReference))
            {
#if INVENTORY_PRO_ADD_ON
                if (InventoryProManager.instance)
                {
                    (bool success, int remainingAmount) = InventoryProManager.instance._GridGenerator.Operations.AddItemToInventory(attachmentIdentifier, 1);
                    if(success)
                    {
                        alreadyInteracted = true;
                        ToastManager.Instance?.ShowToast($"{attachmentIdentifier._name} {ToastManager.Instance.CollectedMsg}");
                        StoreData();
                        Destroy(this.gameObject);
                    }
                    else
                        ToastManager.Instance?.ShowToast(ToastManager.Instance.InventoryIsFullMsg);
                }
#endif
                alreadyInteracted = false;
                return;
            }

            // 兼容时触发配件拾取事件。
            // If compatible, assign a new attachment && Unholster the weapon
            interactEventsProvider.Events.OnAttachmentPickedUp?.Invoke(atc);

            // 调用基类交互逻辑（触发事件、标记已交互、存档）。
            base.Interact(player);

            // 拾取完成后销毁该拾取物。
            Destroy(this.gameObject);
        }

        // Get visuals of the attachment when dropping
        // 掉落该拾取物：调用基类掉落逻辑后重新加载展示外观。
        public override void Drop(PlayerDependencies playerDependencies, PlayerOrientation orientation)
        {
            base.Drop(playerDependencies, orientation);
            GetVisuals();
        }
        // 设置拾取物的展示外观：交互提示文本、图标与展示模型。
        public void GetVisuals()
        {
            // Get whatever we need to display
            if (attachmentIdentifier == null)
            {
                Debug.LogError("Attachment Identifier not set-up! Please assign a proper attachment identifier to your existing attachments, otherwise the system won�t work properly.");
                return;
            }
            // 用配件名称作为交互提示文本。
            interactText = attachmentIdentifier._name;
            // 用配件图标作为拾取物图标。
            image.sprite = attachmentIdentifier.icon;
            // 没有配置展示模型则直接返回。
            if (attachmentIdentifier.pickUpGraphics == null) return;
            // 移除默认子物体，并用配件的展示模型替换。
            Destroy(graphics.GetChild(0).gameObject);
            Instantiate(attachmentIdentifier.pickUpGraphics, transform.position, Quaternion.identity, graphics);
        }
        // 判断交互是否被禁止：库存插件可用时永不禁止；否则武器为空或不兼容则禁止。
        public override bool IsForbiddenInteraction(IWeaponReferenceProvider weaponController)
        {
            return AddonManager.instance.isInventoryAddonAvailable
                ? false
                : weaponController.Weapon != null && !CheckCompatibleAttachment(weaponController) || weaponController.Weapon == null;
        }
        // 检查配件是否与当前武器兼容：兼容时缓存配件对象与 ID，并返回 true。
        public bool CheckCompatibleAttachment(IWeaponReferenceProvider weaponController)
        {
            (bool success, Attachment attachment, int atcId) = CowsinsUtilities.CompatibleAttachment(weaponController.Id, attachmentIdentifier);
            if (success)
            {
                atc = attachment;
                attachmentID = atcId;
            }
            return success;
        }

#if SAVE_LOAD_ADD_ON
        // Destroy if picked up.
        // Interacted State is called after loading.
        // 加载后的状态处理：若该拾取物已被拾取则销毁，否则重新加载展示外观。
        public override void LoadedState()
        {
            if (this.alreadyInteracted) Destroy(this.gameObject);
            else GetVisuals();
        }
#endif
    }
}