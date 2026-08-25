// ======================================================================
// WeaponPickeable.cs —— 武器拾取物
// 派生自 Pickeable，代表场景中可拾取的武器：负责初始化武器的子弹/配件数据、
// 与玩家武器系统的交互(添加、交换武器)、掉落，以及保存/加载时的数据存取。
// ======================================================================
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;
#if INVENTORY_PRO_ADD_ON
using cowsins.Inventory;
#endif
#if SAVE_LOAD_ADD_ON
using cowsins.SaveLoad;
#endif

namespace cowsins
{
    /// <summary>武器拾取物：代表场景中可拾取的武器实体，管理武器数据、子弹、配件，并实现拾取/掉落/存档逻辑。</summary>
    public partial class WeaponPickeable : Pickeable
    {
        // 该拾取物对应的武器数据资产。
        [Tooltip("Which weapon are we grabbing"), SaveField] public Weapon_SO weapon;

        // 当前弹匣内子弹数 与 备弹总数(用于拾取时恢复武器状态)。
        [SaveField] private int currentBullets, totalBullets;
        // 当前武器各附件类型对应的附件标识字典。
        public Dictionary<AttachmentType, AttachmentIdentifier_SO> currentAttachments = new Dictionary<AttachmentType, AttachmentIdentifier_SO>();

        /// <summary>初始化：先执行基类初始化，为所有附件类型建立默认空键，若非已丢弃状态则调用 Initialize 初始化武器数据。</summary>
        public override void Awake()
        {
            base.Awake();
            foreach (AttachmentType type in System.Enum.GetValues(typeof(AttachmentType)))
            {
                if (!currentAttachments.ContainsKey(type))
                    currentAttachments[type] = null;
            }
            if (dropped) return;
            Initialize();
        }

        /// <summary>玩家拾取武器：处理重复武器补弹、添加到武器系统或与当前武器交换的逻辑，并销毁拾取物。</summary>
        public override void Interact(Transform player)
        {
            if (weapon == null)
            {
                CowsinsUtilities.LogError("<b><color=yellow>Weapon_SO</color></b> " +
                "not found! Skipping Interaction.", this);
                return;
            }
            base.Interact(player);

            PlayerDependencies playerDependencies = player.GetComponent<PlayerDependencies>();
            IWeaponBehaviourProvider weaponController = player.GetComponent<IWeaponBehaviourProvider>();
            IInteractManagerProvider interactManager = playerDependencies.InteractManager;

            // 若开启重复武器补弹且成功补充弹药，则直接销毁拾取物并返回。
            if (interactManager.DuplicateWeaponAddsBullets && weaponController.AddDuplicateWeaponAmmo(10))
            {
                DestroyAndSave();
                return;
            }

            // 尝试将武器添加到玩家的武器系统；成功则销毁拾取物并返回。
            if (weaponController.TryToAddWeapons(weapon, currentBullets, totalBullets, currentAttachments.Values.ToList()))
            {
                DestroyAndSave();
                return;
            }

#if INVENTORY_PRO_ADD_ON
             if (InventoryProManager.instance && InventoryProManager.instance.StoreWeaponsIfHotbarFull)
            {
                bool success = InventoryProManager.instance._GridGenerator.Operations.AddWeaponToInventory(weapon, currentBullets, totalBullets);
                if (success)
                {
                    ToastManager.Instance?.ShowToast($"{weapon._name} {ToastManager.Instance.CollectedMsg}");
                    DestroyAndSave();
                }
                else
                    ToastManager.Instance?.ShowToast(ToastManager.Instance.InventoryIsFullMsg);
                return;
            }
#endif
            // 否则执行武器交换，把玩家当前武器与拾取物武器互换。
            (Weapon_SO swappedWeapon, int swappedCurrentBullets, int swappedTotalBullets) = weaponController.SwapWeapons(weapon, currentBullets, totalBullets, currentAttachments.Values.ToList());
            this.weapon = swappedWeapon;
            this.currentBullets = swappedCurrentBullets;
            this.totalBullets = swappedTotalBullets;

            DestroyGraphics();
            GetVisuals();

            alreadyInteracted = false;
#if SAVE_LOAD_ADD_ON
            StoreData();
#endif
        }

        // 在销毁拾取物之前先执行存档(若启用存档插件)，再销毁该 GameObject。
        private void DestroyAndSave()
        {
#if SAVE_LOAD_ADD_ON
            alreadyInteracted = true;
            StoreData();
#endif
            Destroy(this.gameObject);
        }

        /// <summary>丢弃当前武器为拾取物：调用基类掉落逻辑，从玩家武器系统读取当前武器、子弹数并刷新视觉模型。</summary>
        public override void Drop(PlayerDependencies playerDependencies, PlayerOrientation orientation)
        {
            base.Drop(playerDependencies, orientation);

            IWeaponReferenceProvider wRef = playerDependencies.WeaponReference;
            WeaponIdentification currentId = wRef.Id != null ? wRef.Id : wRef.Inventory[wRef.CurrentWeaponIndex];

            if (currentId != null)
            {
                currentBullets = currentId.bulletsLeftInMagazine;
                totalBullets = currentId.totalBullets;
                weapon = currentId.weapon;
            }
            GetVisuals();
        }

        /// <summary>用指定参数覆盖拾取物的武器、子弹与配件数据，并刷新视觉模型(通常用于程序化生成拾取物)。</summary>
        public void DropOverrideParameters(Weapon_SO weapon, int currentBullets, int totalBullets, Dictionary<AttachmentType, AttachmentIdentifier_SO> tempAttachments)
        {
            this.pickeable = true;
            this.weapon = weapon;
            this.currentBullets = currentBullets;
            this.totalBullets = totalBullets;
            foreach (AttachmentType type in System.Enum.GetValues(typeof(AttachmentType)))
            {
                currentAttachments[type] = tempAttachments[type];
            }
            GetVisuals();
        }

        // 从武器标识对象中读取当前各类型附件，存储到拾取物的 currentAttachments 字典，供后续拾取时恢复。
        /// <summary>
        /// Stores the attachments on the WeaponPickeable so they can be accessed later in case the weapon is picked up.
        /// </summary>
        public void SetPickeableAttachments(WeaponIdentification wId)
        {
            foreach (AttachmentType type in System.Enum.GetValues(typeof(AttachmentType)))
            {
                currentAttachments[type] = wId.AttachmentState.GetCurrent(type)?.attachmentIdentifier;
            }
        }

        // ===== 初始化相关逻辑区域 =====
        #region INITIALIZATION
        // 初始化武器拾取物的数据：刷新视觉模型、设置默认附件，并计算初始子弹数与备弹总数。
        private void Initialize()
        {
            if (weapon == null) return;
            GetVisuals();

            var weaponId = weapon.weaponObject;

            // Handle Attachments
            SetDefaultAttachments(weaponId);
            // 若默认弹匣附件存在，读取其提供的弹匣容量加成。
            int magCapacityAdded = 0;
            if (weaponId != null && weaponId.AttachmentState != null && weaponId.AttachmentState.GetDefault(AttachmentType.Magazine) is Magazine magazine)
                magCapacityAdded = magazine.magazineCapacityAdded;

            // 初始弹匣子弹 = 武器基础弹匣容量 + 默认弹匣加成；备弹总数 = 弹匣数量 x 单匣容量。
            currentBullets = weapon.magazineSize + magCapacityAdded;
            totalBullets = weapon.totalMagazines * currentBullets;
        }

        /// <summary>刷新拾取物的显示：设置交互提示文本与图标，销毁旧视觉模型并实例化武器对应的拾取物模型。</summary>
        public void GetVisuals()
        {
            // Get whatever we need to display
            interactText = weapon._name;
            image.sprite = weapon.icon;
            // Manage graphics
            Destroy(graphics.transform.GetChild(0).gameObject);
            Instantiate(weapon.pickUpGraphics, graphics);
        }

        /// <summary>按附件类型从字典中查找对应的附件标识；若不存在则返回 null。</summary>
        public AttachmentIdentifier_SO GetAttachmentByType(AttachmentType type)
        {
            if (currentAttachments.TryGetValue(type, out AttachmentIdentifier_SO attachmentId))
            {
                return attachmentId;
            }
            return null;
        }

        // 将武器各附件类型的默认附件标识填充到 currentAttachments 字典中。
        // Applied the default attachments to the weapon
        private void SetDefaultAttachments(WeaponIdentification weaponId)
        {
            if (weaponId == null || weaponId.AttachmentState == null)
            {
                return;
            }

            foreach (AttachmentType type in System.Enum.GetValues(typeof(AttachmentType)))
            {
                Attachment defaultAttachment = weaponId.AttachmentState.GetDefault(type);
                currentAttachments[type] = defaultAttachment?.attachmentIdentifier;
            }
        }


#if SAVE_LOAD_ADD_ON
        public override CustomSaveData SaveFields()
        {
            // Save base fields
            CustomSaveData saveData = base.SaveFields();
            
            // Save attachments
            if (currentAttachments.ContainsKey(AttachmentType.Barrel)) saveData.savedFields["barrel"] = currentAttachments[AttachmentType.Barrel]?.name;
            if (currentAttachments.ContainsKey(AttachmentType.Scope)) saveData.savedFields["scope"] = currentAttachments[AttachmentType.Scope]?.name;
            if (currentAttachments.ContainsKey(AttachmentType.Stock)) saveData.savedFields["stock"] = currentAttachments[AttachmentType.Stock]?.name;
            if (currentAttachments.ContainsKey(AttachmentType.Grip)) saveData.savedFields["grip"] = currentAttachments[AttachmentType.Grip]?.name;
            if (currentAttachments.ContainsKey(AttachmentType.Magazine)) saveData.savedFields["magazine"] = currentAttachments[AttachmentType.Magazine]?.name;
            if (currentAttachments.ContainsKey(AttachmentType.Flashlight)) saveData.savedFields["flashlight"] = currentAttachments[AttachmentType.Flashlight]?.name;
            if (currentAttachments.ContainsKey(AttachmentType.Laser)) saveData.savedFields["laser"] = currentAttachments[AttachmentType.Laser]?.name;
            
            return saveData;
        }

        /// <summary>从存档数据中恢复基类字段与各类型附件(通过 ItemRegistry 按名称反查附件标识)。</summary>
        /// <summary>从存档数据中恢复基类字段与各类型附件(通过 ItemRegistry 按名称反查附件标识)。</summary>
        public override void LoadFields(object data)
        {
            // Load base fields first
            base.LoadFields(data);
            
            if (data is CustomSaveData saveData)
            {
                // Restore attachments
                if (saveData.savedFields.TryGetValue("barrel", out object barrelName) && barrelName != null)
                    currentAttachments[AttachmentType.Barrel] = ItemRegistry.GetItemByName(barrelName.ToString()) as AttachmentIdentifier_SO;
                
                if (saveData.savedFields.TryGetValue("scope", out object scopeName) && scopeName != null)
                    currentAttachments[AttachmentType.Scope] = ItemRegistry.GetItemByName(scopeName.ToString()) as AttachmentIdentifier_SO;
                
                if (saveData.savedFields.TryGetValue("stock", out object stockName) && stockName != null)
                    currentAttachments[AttachmentType.Stock] = ItemRegistry.GetItemByName(stockName.ToString()) as AttachmentIdentifier_SO;
                
                if (saveData.savedFields.TryGetValue("grip", out object gripName) && gripName != null)
                    currentAttachments[AttachmentType.Grip] = ItemRegistry.GetItemByName(gripName.ToString()) as AttachmentIdentifier_SO;
                
                if (saveData.savedFields.TryGetValue("magazine", out object magazineName) && magazineName != null)
                    currentAttachments[AttachmentType.Magazine] = ItemRegistry.GetItemByName(magazineName.ToString()) as AttachmentIdentifier_SO;
                
                if (saveData.savedFields.TryGetValue("flashlight", out object flashlightName) && flashlightName != null)
                    currentAttachments[AttachmentType.Flashlight] = ItemRegistry.GetItemByName(flashlightName.ToString()) as AttachmentIdentifier_SO;
                
                if (saveData.savedFields.TryGetValue("laser", out object laserName) && laserName != null)
                    currentAttachments[AttachmentType.Laser] = ItemRegistry.GetItemByName(laserName.ToString()) as AttachmentIdentifier_SO;
            }
        }

        // If the Interactable was interacted, destroy on load, if not, load its visuals.
        /// <summary>加载完成后的状态处理：若该交互物已被拾取则销毁，否则刷新其视觉模型。</summary>
        public override void LoadedState()
        {
            if (this.alreadyInteracted) Destroy(this.gameObject);
            else GetVisuals();
        }
#endif
        #endregion
    }

#if UNITY_EDITOR

    /// <summary>武器拾取物的自定义 Inspector：按 Basic/References/Effects/Events 页签分组展示字段。</summary>
    [System.Serializable]
    [CustomEditor(typeof(WeaponPickeable))]
    public class WeaponPickeableEditor : Editor
    {
        private string[] tabs = { "Basic", "References", "Effects", "Events" };
        private int currentTab = 0;

        /// <summary>绘制武器拾取物的自定义 Inspector：顶部显示贴图与页签，按页签展示武器、引用、特效、事件字段，最后应用修改。</summary>
        override public void OnInspectorGUI()
        {
            serializedObject.Update();
            WeaponPickeable myScript = target as WeaponPickeable;

            Texture2D myTexture = Resources.Load<Texture2D>("CustomEditor/WeaponPickeable_CustomEditor") as Texture2D;
            GUILayout.Label(myTexture);

            EditorGUILayout.BeginVertical();
            currentTab = GUILayout.Toolbar(currentTab, tabs);
            EditorGUILayout.Space(10f);
            EditorGUILayout.EndVertical();
            #region variables

            if (currentTab >= 0 || currentTab < tabs.Length)
            {
                switch (tabs[currentTab])
                {
                    case "Basic":
                        EditorGUILayout.LabelField("CUSTOMIZE YOUR WEAPON PICKEABLE", EditorStyles.boldLabel);
                        EditorGUILayout.PropertyField(serializedObject.FindProperty("weapon"));
                        EditorGUILayout.PropertyField(serializedObject.FindProperty("interactText"));
                        EditorGUILayout.PropertyField(serializedObject.FindProperty("instantInteraction"));
                        break;
                    case "References":
                        EditorGUILayout.PropertyField(serializedObject.FindProperty("image"));
                        EditorGUILayout.PropertyField(serializedObject.FindProperty("graphics"));

                        break;
                    case "Effects":
                        EditorGUILayout.LabelField("EFFECTS", EditorStyles.boldLabel);
                        EditorGUILayout.PropertyField(serializedObject.FindProperty("rotates"));
                        if (myScript.rotates) EditorGUILayout.PropertyField(serializedObject.FindProperty("rotationSpeed"));

                        EditorGUILayout.PropertyField(serializedObject.FindProperty("translates"));
                        if (myScript.translates) EditorGUILayout.PropertyField(serializedObject.FindProperty("translationSpeed"));
                        break;
                    case "Events":
                        EditorGUILayout.PropertyField(serializedObject.FindProperty("events"));
                        break;

                }
            }

            #endregion

            serializedObject.ApplyModifiedProperties();

        }
    }
#endif
}