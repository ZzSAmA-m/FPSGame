// ----------------------------------------------------------------------
// BulletsPickeable.cs
// 作用：定义“子弹拾取物”。玩家与之交互时，将对应数量的子弹加入玩家当前武器的
//       弹药总量；在库存插件（INVENTORY_PRO_ADD_ON）启用时改为加入物品栏。
//       拾取的数量与子弹类型分别由 amountOfBullets 与 bulletTypeIdentifier 决定。
// ----------------------------------------------------------------------
using UnityEngine;
#if INVENTORY_PRO_ADD_ON
using cowsins.Inventory;
#endif
namespace cowsins
{
    /// <summary>
    /// 子弹拾取物。继承自 Pickeable，玩家交互后根据库存插件是否可用，
    /// 将指定数量的子弹加入物品栏或当前武器的弹药总量。
    /// </summary>
    public partial class BulletsPickeable : Pickeable
    {
        // 拾取时获得的子弹数量（SaveField 表示其参与存档）。
        [Tooltip("How many bullets you will get"), SerializeField, SaveField] private int amountOfBullets;

        // 该拾取物对应的子弹类型标识资源。
        [SerializeField] private BulletTypeIdentifier_SO bulletTypeIdentifier;

        // 只读属性：返回该拾取物的子弹数量。
        public int AmountOfBullets => amountOfBullets;

        // 初始化：调用基类后，校验子弹类型标识并据此设置拾取物的图标与展示模型。
        public override void Awake()
        {
            base.Awake();

            // 子弹类型标识为空时记录错误并终止初始化。
            if (bulletTypeIdentifier == null)
            {
                CowsinsUtilities.LogError("<b><color=yellow>Bullet_SO</color></b> " +
                "not found!", this);
                return;
            }

            // 设置拾取物图标。
            image.sprite = bulletTypeIdentifier.icon;
            // 移除默认子物体，并用子弹类型对应的拾取展示模型替换。
            Destroy(graphics.transform.GetChild(0).gameObject);
            Instantiate(bulletTypeIdentifier.pickUpGraphics, graphics);
        }
        // 执行拾取：根据库存插件是否可用，将子弹加入物品栏或玩家当前武器的弹药总量。
        public override void Interact(Transform player)
        {

            // 子弹类型标识为空时记录错误并跳过交互。
            if (bulletTypeIdentifier == null)
            {
                CowsinsUtilities.LogError("<b><color=yellow>Bullet_SO</color></b> " +
                "not found! Skipping Interaction.", this);
                return;
            }

#if INVENTORY_PRO_ADD_ON
            if (InventoryProManager.instance)
            {
                (bool success, int remainingAmount) = InventoryProManager.instance._GridGenerator.Operations.AddItemToInventory(bulletTypeIdentifier, amountOfBullets);
                if (success)
                {
                    alreadyInteracted = true;
                    interactableEvents.OnInteract?.Invoke();
                    StoreData();
                    ToastManager.Instance?.ShowToast($"x{amountOfBullets - remainingAmount} {ToastManager.Instance.CollectedMsg}");
                    amountOfBullets = remainingAmount;
                    if(amountOfBullets <= 0) Destroy(this.gameObject);
                }
                else
                    ToastManager.Instance?.ShowToast(ToastManager.Instance.InventoryIsFullMsg);
                return;
            }
#else
            // 未启用库存插件时：玩家未持武器则直接返回（无法拾取子弹）。
            if (player.GetComponent<IWeaponReferenceProvider>().Weapon == null) return;
#endif
            alreadyInteracted = true; 
            // 调用基类交互逻辑（触发事件、标记已交互、存档）。
            base.Interact(player);

            PlayerDependencies playerDependencies = player.GetComponent<PlayerDependencies>();
            // 把拾取到的子弹数累加到当前武器的弹药总量。
            playerDependencies.WeaponReference.Id.totalBullets += amountOfBullets;
            playerDependencies.WeaponEvents.Events.OnAmmoChanged?.Invoke(false); 
            // 拾取完成后销毁该拾取物。
            Destroy(this.gameObject);
        }
        // 设置子弹类型与数量（供外部在运行时动态配置该拾取物）。
        public void SetBullets(BulletTypeIdentifier_SO bulletsSO, int amountOfBullets)
        {
            // 记录子弹数量。
            this.amountOfBullets = amountOfBullets;
            // 记录子弹类型标识。
            this.bulletTypeIdentifier = bulletsSO;
        }

        // 判断拾取是否被禁止：库存插件可用时永不禁止；否则根据武器是否为空、
        // 是否限弹匣以及子弹类型是否匹配来决定。
        public override bool IsForbiddenInteraction(IWeaponReferenceProvider weaponController)
        {
            Weapon_SO weapon = weaponController.Weapon;

            return AddonManager.instance.isInventoryAddonAvailable
                ? false
                : weapon != null && !weapon.limitedMagazines || weapon != null && weapon.limitedMagazines && !CowsinsUtilities.MatchesBulletType(bulletTypeIdentifier, weapon.bulletTypeIdentifier) || weaponController.Weapon == null;
        }


#if SAVE_LOAD_ADD_ON
        // Destroy if picked up.
        // Interacted State is called after loading.
        // 加载后的状态处理：若该拾取物已被拾取，则销毁它。
        public override void LoadedState()
        {
            if (this.alreadyInteracted) Destroy(this.gameObject);
        }
#endif
    }
}