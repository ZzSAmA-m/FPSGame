/// <summary>
/// This script belongs to cowsins� as a part of the cowsins� FPS Engine. All rights reserved. 
/// </summary>
// ======================================================================
// InteractManager.cs —— 玩家交互管理器
// 挂载于玩家角色：用射线检测可交互物体并显示/隐藏交互 UI，处理交互进度与执行交互，
// 并负责武器丢弃、配件卸下/丢弃、武器检视等逻辑，通过事件向 UI 与其他系统广播状态。
// ======================================================================
using UnityEngine;
using UnityEngine.Events;
using System.Collections.Generic;
#if INVENTORY_PRO_ADD_ON
using cowsins.Inventory;
#endif

namespace cowsins
{
    /// <summary>玩家交互管理器：统一管理交互检测、交互 UI、交互执行，以及武器/配件丢弃与检视，是整个拾取与交互系统的核心。</summary>
    public class InteractManager : MonoBehaviour, IInteractManagerProvider, IInteractEventsProvider
    {
        /// <summary>可序列化的事件集合：暴露完成交互、丢弃武器/配件、检测到可交互或禁止交互等回调，供 Inspector 配置。</summary>
        [System.Serializable]
        public class InteractEvents
        {
            // 完成一次交互后触发的事件。
            public UnityEvent OnFinishInteraction;
            // 丢弃一个拾取物(武器/配件)后触发，参数为被丢弃的拾取物。
            public UnityEvent<Pickeable> onDrop;
            // 丢弃武器时触发的事件。
            public UnityEvent onDropWeapon;
            // 检测到可交互物体时触发的事件。
            public UnityEvent onDetectInteractable;
            // 检测到被禁止的交互时触发的事件。
            public UnityEvent onDetectForbiddenInteraction;
        }

        // 玩家主摄像机，用于发射交互检测射线。
        [Tooltip("Attach your main camera"), SerializeField] private Camera mainCamera;

        // 定义可交互物体所在层的位掩码，用于过滤射线检测结果。
        [Tooltip("Bitmask that defines the interactable layer"), SerializeField] private LayerMask mask;

        // 是否允许玩家丢弃武器。
        [Tooltip("Enable this toggle if you want to be able to drop your weapons"), SerializeField] private bool canDrop;

        // 通用武器拾取物预制体，丢弃武器时用于实例化。
        [Tooltip("Attach the generic pickeable object here"), SerializeField] private Pickeable weaponGenericPickeable;

        // 通用配件拾取物预制体，丢弃配件时用于实例化。
        [Tooltip("Attach the generic pickeable object here"), SerializeField] private Pickeable attachmentGenericPickeable;

        // 检测可交互物体的射线距离。
        [Tooltip("Distance from the player to detect interactable objects"), SerializeField] private float detectInteractionDistance;

        // 丢弃物品时，拾取物在玩家前方实例化的距离。
        [Tooltip("Distance from the player where the pickeable will be instantiated"), SerializeField] private float droppingDistance;

        // 丢弃时的随机偏移量(在 -randomDropOffset 到 +randomDropOffset 之间随机)。
        [Tooltip("Randomize drop offset (from -randomDropOffset to +randomDropOffset)"), SerializeField, Range(0f,1f)] private float randomDropOffset = .2f;

        // 成功交互所需按住交互键的时长。
        [SerializeField, Tooltip("How much time player has to hold the interact button in order to successfully interact")] private float progressRequiredToInteract;

        // 两次交互之间的最小间隔，值越小可交互越频繁。
        [Tooltip("Adjust the interaction interval, the lower, the faster you will be able to interact"), Range(.2f, .7f), SerializeField] private float interactInterval = .4f;

        // 拾取重复武器时是否改为补充子弹而非生成新武器实例(仅对有限弹匣武器有效)。
        [Tooltip("When picking up a duplicate weapon, if duplicateWeaponAddsBullets is true, the bullets will be added to the total count of the current weapon instead of creating a new instance of the same weapon. " +
            "This feature is only applicable to weapons with limited magazines."), SerializeField]
        private bool duplicateWeaponAddsBullets;

        // 是否允许玩家检视当前武器。
        [Tooltip("If true, the player will be able to inspect the current weapon."), SerializeField] private bool canInspect;

        // 检视时是否允许实时装卸配件，并显示对应的自定义 UI。
        [Tooltip("Allows the player to equip and unequip attachments while inspecting. It also displays a custom UI for that."), SerializeField] private bool realtimeAttachmentCustomization;

        // 检视时是否只显示当前已装备的配件(否则显示所有兼容配件)。
        [Tooltip("When inspecting, display current attachments only. Otherwise you will be able to see all compatible attachments."), SerializeField] private bool displayCurrentAttachmentsOnly;

        // 检视时若通过 UI 卸下配件且该值为 true，则配件会被丢弃到场景。
        [Tooltip("While Inspecting the weapon, if an attachment is dettached and this field is true, the attachment will be dropped."), SerializeField] private bool dropAttachmentOnDettachUI;

        // 当前高亮的交互是否被禁止。
        private bool isForbiddenInteraction = false;
        // 当前交互进度的累计时长。
        private float progressElapsed;
        // 是否刚完成一次交互(用于防止交互 spam)。
        private bool alreadyInteracted = false;
        // 当前是否处于武器检视状态。
        private bool inspecting = false;

        /// <summary>获取当前交互进度累计时长。</summary>
        public float ProgressElapsed => progressElapsed;
        /// <summary>当前是否处于武器检视状态。</summary>
        public bool Inspecting => inspecting;
        /// <summary>是否允许丢弃武器。</summary>
        public bool CanDrop => canDrop;
        /// <summary>丢弃物品的实例化距离。</summary>
        public float DroppingDistance => droppingDistance;
        /// <summary>重复武器是否改为补充子弹。</summary>
        public bool DuplicateWeaponAddsBullets => duplicateWeaponAddsBullets;
        /// <summary>是否允许检视武器。</summary>
        public bool CanInspect => canInspect;   
        /// <summary>检视时是否支持实时配件定制。</summary>
        public bool RealtimeAttachmentCustomization => realtimeAttachmentCustomization; 
        /// <summary>检视时是否仅显示当前配件。</summary>
        public bool DisplayCurrentAttachmentsOnly => displayCurrentAttachmentsOnly; 
        /// <summary>当前高亮的可交互物体。</summary>
        public Interactable HighlightedInteractable => highlightedInteractable;
        /// <summary>交互管理器对外暴露的事件集合。</summary>
        public InteractManagerEvents Events { get; private set; } = new InteractManagerEvents();


        // 玩家朝向信息，用于计算丢弃物品的方向。
        private PlayerOrientation orientation;

        // 玩家依赖聚合，集中获取各子系统引用。
        private PlayerDependencies playerDependencies;
        // 玩家移动状态提供者(实现于 PlayerMovement.cs)。
        private IPlayerMovementStateProvider playerMovement; // IPlayerMovementStateProvider is implemented in PlayerMovement.cs
        // 玩家控制提供者(实现于 PlayerControl.cs)，用于判断玩家当前是否可控。
        private IPlayerControlProvider playerControl; // IPlayerControlProvider is implemented in PlayerControl.cs
        // 武器行为提供者(实现于 WeaponController.cs)，提供换弹/近战等状态查询。
        private IWeaponBehaviourProvider weaponController; // IWeaponBehaviourProvider is implemented in WeaponController.cs
        // 武器引用提供者(实现于 WeaponController.cs)，提供当前武器与主相机等引用。
        private IWeaponReferenceProvider weaponReferences; // IWeaponReferenceProvider is implemented in WeaponController.cs
        // 武器事件提供者(实现于 WeaponController.cs)，用于监听切枪等事件。
        private IWeaponEventsProvider weaponEvents; // IWeaponEventsProvider is implemented in WeaponController.cs
        // 输入管理器，用于读取交互/丢弃输入并订阅事件。
        private InputManager inputManager;

        // 当前被高亮(可交互)的交互物体。
        private Interactable highlightedInteractable;

        // 用户可配置的交互事件集合，Inspector 中序列化保存。
        public InteractEvents userEvents;

        // 启用时：若启用了实时配件定制，则根据是否丢弃配件，订阅 UI 配件元素的点击事件。
        private void OnEnable()
        {
            // Subscribe to the event
            if(realtimeAttachmentCustomization)
            {
                if (dropAttachmentOnDettachUI) UIEvents.onAttachmentUIElementClicked += DropAttachment;
                else UIEvents.onAttachmentUIElementClicked += DeactivateCurrentAttachment;
            }
        }

        // 初始化：抓取各子系统引用；若允许丢弃武器则订阅丢弃事件，并监听切枪事件以关闭交互 UI。
        private void Start()
        {
            // Grab main references
            playerDependencies = GetComponent<PlayerDependencies>();
            weaponController = playerDependencies.WeaponBehaviour;
            weaponReferences = playerDependencies.WeaponReference;
            weaponEvents = playerDependencies.WeaponEvents; 
            playerMovement = playerDependencies.PlayerMovementState;
            playerControl = playerDependencies.PlayerControl;
            orientation = playerMovement.Orientation;
            mainCamera = weaponReferences.MainCamera;
            inputManager = playerDependencies.InputManager;

            // Listen for the drop event from the InputManager
            if(canDrop)
                inputManager.OnDrop += HandleDrop;

            weaponEvents.Events.OnSwitchingWeapon.AddListener(DisableInteractionUI);
        }


        // 禁用时：取消订阅 UI 配件元素点击事件，避免悬空引用。
        private void OnDisable()
        {
            // Unsubscribe to the event
            UIEvents.onAttachmentUIElementClicked -= DropAttachment;
            UIEvents.onAttachmentUIElementClicked -= DeactivateCurrentAttachment;
        }

        // 销毁时：解除对丢弃事件与切枪事件的订阅，防止内存泄漏/悬空引用。
        private void OnDestroy()
        {
            if (canDrop && inputManager != null) 
                inputManager.OnDrop -= HandleDrop;

            if (weaponEvents != null && weaponEvents.Events != null)
                weaponEvents.Events.OnSwitchingWeapon.RemoveListener(DisableInteractionUI);
        }

        // 每帧更新：若刚完成交互或玩家不可控则直接返回，否则检测可交互物体并读取输入。
        private void Update()
        {
            // If we already interacted, or the player is not controllable, return!
            if (alreadyInteracted || !playerControl.IsControllable) return;

            DetectInteractable();
            DetectInput();
        }
        // 射线检测：从主相机沿前方发射射线，命中可交互物体则高亮/显示 UI，否则关闭交互 UI。
        private void DetectInteractable()
        {
            if(mainCamera == null) return;

            // If we got a hit from the raycast:
            if (Physics.Raycast(mainCamera.transform.position, mainCamera.transform.forward, out RaycastHit interactableHit, detectInteractionDistance, mask))
            {
                if (highlightedInteractable?.transform != interactableHit.transform)
                {
                    if (!interactableHit.collider.TryGetComponent(out Interactable interactableTarget) || highlightedInteractable != null)
                    {
                        DisableInteractionUI();
                        return;
                    }
                    // Check if the interaction is forbidden
                    if (interactableTarget.IsForbiddenInteraction(weaponReferences))
                    {
                        isForbiddenInteraction = true;
                        Events.OnForbiddenInteraction?.Invoke();
                        userEvents.onDetectForbiddenInteraction?.Invoke();
                    }
                    else
                    {
                        // If its not, enable interaction UI to display an interaction
                        EnableInteractionUI(interactableTarget);
                        isForbiddenInteraction = false;
                        
                        userEvents.onDetectInteractable?.Invoke();
                    }
                    highlightedInteractable = interactableTarget;
                }
            }
            else if(highlightedInteractable != null) 
            {
                // If we dont find any interactable, disable interactions UI
                DisableInteractionUI();
            }
        }

        // 启用交互 UI：设置目标交互物为可交互、高亮它，并通过事件把交互提示文本广播给 UI。
        private void EnableInteractionUI(Interactable interactable)
        {
            if (interactable == null)
            {
                DisableInteractionUI();
                return;
            }
            interactable.interactable = true;
            // Current interactable is equal to the passed interactable value
            if (highlightedInteractable == interactable) return;
            highlightedInteractable = interactable;
            interactable.Highlight();
            Events.OnAllowedInteraction?.Invoke(interactable.interactText);
        }

        // 关闭交互 UI：取消当前高亮物体的高亮与可交互状态，清空引用并广播关闭事件。
        private void DisableInteractionUI()
        {
            if(highlightedInteractable)
            {
                highlightedInteractable.interactable = false;
                highlightedInteractable.Unhighlight();
            }
            highlightedInteractable = null;
            Events.OnDisableInteraction?.Invoke();
        }

        // 读取输入：按住交互键时累计进度并在达标后执行交互；松开或无可交互目标则重置进度。
        private void DetectInput()
        {
            if (highlightedInteractable == null || isForbiddenInteraction)
            {
                ResetInteractionProgress();
                return;
            }
            // If we dont detect an interactable then dont continue
            // However if we detected an interactable + we pressing the interact button, then: 
            if (inputManager.Interacting)
            {
                progressElapsed += Time.deltaTime;
                if (progressRequiredToInteract > 0)
                {
                    Events.OnInteractionProgressChanged?.Invoke(progressElapsed / progressRequiredToInteract);
                }

                // Interact
                if (progressElapsed >= progressRequiredToInteract || highlightedInteractable.InstantInteraction && progressElapsed > 0) PerformInteraction();
            }
            else
            {
                ResetInteractionProgress();
            }
        }

        // 执行交互：重置进度、防止 spam，调用目标物体的 Interact，延迟重置交互冷却并广播完成事件、关闭 UI。
        private void PerformInteraction()
        {
            ResetInteractionProgress();
            // prevent from spamming
            alreadyInteracted = true;
            // Perform any interaction you may like
            // Please note that classes that inherit from interactable can override the virtual void Interact()
            highlightedInteractable.Interact(this.transform);
            // Prevent from spamming but let the user interact again
            Invoke(nameof(ResetInteractTimer), interactInterval);
            
            Events.OnPerformInteraction?.Invoke();  

            // Manage UI
            highlightedInteractable?.Unhighlight();
            highlightedInteractable = null;

            userEvents.OnFinishInteraction.Invoke(); // Call our event
            Events.OnFinishInteraction?.Invoke();
        }

        // 重置交互进度：将进度设为一个极小负值并向 UI 广播进度为 0。
        private void ResetInteractionProgress()
        {
            progressElapsed = -.01f; 
            Events.OnInteractionProgressChanged?.Invoke(0);
        }

        // 处理丢弃武器：校验可丢弃状态后，在玩家前方实例化武器拾取物，填充当前武器与配件数据并广播丢弃事件。
        private void HandleDrop()
        {
            // Handles weapon dropping by pressing the drop button
            if (weaponReferences.Weapon == null || (weaponController.IsReloading && !weaponReferences.Weapon.allowCancelReload) || !weaponController.IsMeleeAvailable || inspecting || !playerControl.IsControllable) return;

            WeaponPickeable pick = Instantiate(weaponGenericPickeable, orientation.Position + orientation.Forward * droppingDistance + transform.right * randomDropOffset, orientation.Rotation) as WeaponPickeable;
            pick.Drop(playerDependencies, orientation);
            WeaponIdentification wp = weaponReferences.Id; 
            pick.SetPickeableAttachments(wp);

            Events.OnDrop?.Invoke();
            userEvents.onDrop?.Invoke(pick);
        }
        private void ResetInteractTimer() => alreadyInteracted = false;

        /// <summary>设置检视状态标记。</summary>
        /// <param name="state">是否进入检视状态。</param>
        public void ToggleInspectionState(bool state) => inspecting = state;

        // 将指定配件从武器上卸下并丢弃到地面：若安装了库存插件则尝试加入库存，否则实例化配件拾取物，最后停用该配件并刷新检视 UI。
        /// <summary>
        /// Drops the current attachment to the ground ( generates a new attachment pickeable )
        /// </summary>
        /// <param name="atc">Attachment to drop </param>
        /// <param name="enableDefault">Enables the default attachment when dropped if true.</param>
        public void DropAttachment(Attachment atc, bool enableDefault)
        {
            if (atc == null) return;

#if INVENTORY_PRO_ADD_ON
            // If Inventory Pro Add-On is installed and the Inventory is available in the scene, try to add to the Inventory
            if (InventoryProManager.instance != null)
            {
                TryAddAttachmentToInventory(atc.attachmentIdentifier);
            }
            else
            {
                InstantiateAttachmentPickeable(atc.attachmentIdentifier);
            }
#else
            if(dropAttachmentOnDettachUI) InstantiateAttachmentPickeable(atc.attachmentIdentifier);
#endif

            DeactivateCurrentAttachment(atc, enableDefault);

            // We should repaint
            if (displayCurrentAttachmentsOnly)
                Events.OnInspectionUIRefreshRequested?.Invoke(displayCurrentAttachmentsOnly);
        }

        // 卸下并停用当前配件：从武器标识中移除同类型配件，若启用默认配件则回退到默认配件并激活。
        private void DeactivateCurrentAttachment(Attachment atc, bool enableDefault)
        {
            // Grab the current weaponidentification object.
            WeaponIdentification wId = weaponReferences.Id;

            if (wId == null || atc == null || atc.attachmentIdentifier == null) return;

            var state = wId.AttachmentState;

            // Grab what type of attachment it is, returns barrel, Scope, etc...
            AttachmentType attachmentType = atc.attachmentIdentifier.attachmentType;
            // Check if any of the attachments saved in the dictionary is the same type as the attachment to drop type.
            if (state.HasCurrent(attachmentType))
            {
                atc.Dettach(wId);

                // Check all the attachment types 
                // This will determine which attachment type matches the dropped attachment
                Attachment defaultAttachment = state.GetDefault(attachmentType);
                state.DeactivateCurrent(attachmentType);

                // If the default attachment is not null, and we should enable default attachments, assign it and enable it
                if (defaultAttachment != null && enableDefault)
                {
                    state.SetCurrent(attachmentType, defaultAttachment);
                    defaultAttachment.gameObject.SetActive(true);
                }
                else
                {
                    // Otherwise do not assign anything
                    state.SetCurrent(attachmentType, null);
                }
            }
        }
#if INVENTORY_PRO_ADD_ON
        /// <summary>尝试将配件加入库存；若加入失败则改为实例化配件拾取物丢弃到场景。</summary>
        public void TryAddAttachmentToInventory(AttachmentIdentifier_SO atcIdentifier)
        {
            (bool atcAddedToInv, int amount) = InventoryProManager.instance._GridGenerator.Operations.AddItemToInventory(atcIdentifier, 1);
            // If the attachment couldnt be added to the Inventory, drop it.
            if (!atcAddedToInv) InstantiateAttachmentPickeable(atcIdentifier);
        }
#endif
        // 实例化配件拾取物：在玩家前方生成，并赋值配件标识、刷新视觉模型。
        private void InstantiateAttachmentPickeable(AttachmentIdentifier_SO atcIdentifier)
        {
            if(atcIdentifier == null)
            {
                Debug.LogError($"<color=red>[COWSINS]</color> Attachment Pickeable couldnt be instantiated, Attachment Identifier is null. Ensure all attachments in your weapon have an attachment identifier assigned.");
                return;
            }

            // Spawn a new pickeable.
            AttachmentPickeable pick = Instantiate(attachmentGenericPickeable, orientation.Position + orientation.Forward * droppingDistance, orientation.Rotation) as AttachmentPickeable;
            // Assign the appropriate attachment identifier to the spawned pickeable.
            pick.attachmentIdentifier = atcIdentifier;
            // Get visuals
            pick.Drop(playerDependencies, orientation);
        }

        // 清空当前高亮交互物体引用。
        private void ResetInteractable() => highlightedInteractable = null;
    }
}