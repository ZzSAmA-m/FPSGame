using System.Collections.Generic;
using UnityEngine;

// ===========================================================================================
// AttachmentStateManager.cs —— 武器配件状态管理器
// 职责：集中管理某把武器的配件状态，包括当前已装备配件、默认配件、兼容配件的查询与增删，
//       并提供配件状态（未装备 / 默认 / 自定义）判定等工具方法。是配件系统的核心枢纽。
// ===========================================================================================
namespace cowsins
{
    // 配件状态管理器：集中处理某把武器的所有配件操作与状态查询。
    /// <summary>
    /// Centralized System for all attachment operations for a specific weapon
    /// </summary>
    public class AttachmentStateManager
    {
        // 所属武器的标识（关联的武器实例）
        private readonly WeaponIdentification weaponIdentification;
        // 当前各配件类型实际装备的配件字典
        private readonly Dictionary<AttachmentType, Attachment> currentAttachments;
        // 武器默认配件容器
        private readonly DefaultAttachment defaultAttachments;
        // 武器兼容配件容器
        private readonly CompatibleAttachments compatibleAttachments;

        // 构造函数：保存武器标识、默认配件与兼容配件引用，并初始化当前配件字典（所有类型初始为空）。
        public AttachmentStateManager(WeaponIdentification weaponId, DefaultAttachment defaults, CompatibleAttachments compatible)
        {
            weaponIdentification = weaponId;
            defaultAttachments = defaults;
            compatibleAttachments = compatible;
            
            // 初始化当前配件字典：为每种配件类型预置空值
            currentAttachments = new Dictionary<AttachmentType, Attachment>();
            foreach (AttachmentType type in System.Enum.GetValues(typeof(AttachmentType)))
            {
                currentAttachments[type] = null;
            }
        }

        #region Current State Management

        /// <summary>
        /// Gets the currently equipped attachment of the specified type
        /// </summary>
        // 获取指定类型当前装备的配件（未装备则返回 null）。
        public Attachment GetCurrent(AttachmentType type)
        {
            currentAttachments.TryGetValue(type, out var attachment);
            return attachment;
        }

        /// <summary>
        /// Sets the current attachment for the specified type
        /// </summary>
        // 设置指定类型当前装备的配件。
        public void SetCurrent(AttachmentType type, Attachment attachment)
        {
            currentAttachments[type] = attachment;
        }

        /// <summary>
        /// Return all currently equipped attachments
        /// </summary>
        // 返回所有类型当前装备的配件字典。
        public IReadOnlyDictionary<AttachmentType, Attachment> GetAllCurrent()
        {
            return currentAttachments;
        }

        /// <summary>
        /// Checks if an attachment of the specified type is currently equipped
        /// </summary>
        // 检查指定类型当前是否已装备配件。
        public bool HasCurrent(AttachmentType type)
        {
            return currentAttachments.ContainsKey(type) && currentAttachments[type] != null;
        }

        /// <summary>
        /// Removes the currently equipped attachment of the specified type
        /// </summary>
        // 移除指定类型当前装备的配件（置空）。
        public void RemoveCurrent(AttachmentType type)
        {
            if (currentAttachments.ContainsKey(type)) currentAttachments[type] = null;
        }

        /// <summary>
        /// Deactivates the currently equipped attachment of the specified type
        /// </summary>
        // 停用（隐藏）指定类型当前装备的配件物体。
        public void DeactivateCurrent(AttachmentType type)
        {
            if (currentAttachments.TryGetValue(type, out var attachment) && attachment != null) attachment.gameObject.SetActive(false);
        }

        #endregion

        #region Default Attachments

        /// <summary>
        /// Gets the default attachment for the specified type
        /// </summary>
        // 获取指定类型的默认配件（无则返回 null）。
        public Attachment GetDefault(AttachmentType type)
        {
            if (defaultAttachments != null && defaultAttachments.DefaultAttachments.TryGetValue(type, out var attachment))
                return attachment;
            return null;
        }

        /// <summary>
        /// Gets all default attachment identifiers
        /// </summary>
        // 获取所有默认配件的标识符列表（用于比对 / 校验）。
        public List<AttachmentIdentifier_SO> GetDefaultIdentifiers()
        {
            var identifiers = new List<AttachmentIdentifier_SO>();

            if (defaultAttachments == null) return identifiers;

            foreach (var kvp in defaultAttachments.DefaultAttachments)
            {
                var attachment = kvp.Value;
                if (attachment != null && attachment.attachmentIdentifier != null)
                {
                    identifiers.Add(attachment.attachmentIdentifier);
                }
            }

            return identifiers;
        }

        /// <summary>
        /// checks if a default attachment exists for the specified type.
        /// </summary>
        // 检查指定类型是否存在默认配件。
        public bool HasDefault(AttachmentType type)
        {
            return defaultAttachments?.DefaultAttachments.ContainsKey(type) == true &&
                   defaultAttachments.DefaultAttachments[type] != null;
        }

        #endregion

        #region Compatible Attachments

        /// <summary>
        /// Gets all compatible attachments for the specified type
        /// </summary>
        // 获取指定类型所有兼容配件（无则返回空列表）。
        public IReadOnlyList<Attachment> GetCompatible(AttachmentType type)
        {
            return compatibleAttachments?.GetCompatible(type) ?? new List<Attachment>();
        }

        /// <summary>
        /// Checks if an attachment is compatible with this weapon
        /// </summary>
        // 判断某个配件标识符是否与当前武器兼容（即在兼容列表中能找到同标识符的配件）。
        public bool IsCompatible(AttachmentIdentifier_SO attachmentIdentifier)
        {
            if (attachmentIdentifier == null || compatibleAttachments == null)
                return false;

            var compatible = compatibleAttachments.GetCompatible(attachmentIdentifier.attachmentType);
            
            foreach (var attachment in compatible)
            {
                if (attachment != null && attachment.attachmentIdentifier == attachmentIdentifier)
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Finds the compatible attachment instance for the given identifier
        /// </summary>
        // 根据标识符查找兼容配件实例（未找到返回 null）。
        public Attachment FindCompatibleAttachment(AttachmentIdentifier_SO attachmentIdentifier)
        {
            if (attachmentIdentifier == null || compatibleAttachments == null)
                return null;

            var compatible = compatibleAttachments.GetCompatible(attachmentIdentifier.attachmentType);
            
            foreach (var attachment in compatible)
            {
                if (attachment != null && attachment.attachmentIdentifier == attachmentIdentifier)
                    return attachment;
            }

            return null;
        }

        /// <summary>
        /// Gathers the number of compatible attachments for the specified type
        /// </summary>
        // 统计指定类型的兼容配件数量。
        public int GetCompatibleCount(AttachmentType type)
        {
            return compatibleAttachments?.GetCompatible(type)?.Count ?? 0;
        }

        #endregion

        #region State Utilities

        /// <summary>
        /// Checks if the current attachment is the default for the specified type.
        /// </summary>
        // 判断指定类型当前装备的配件是否为默认配件。
        public bool IsCurrentDefault(AttachmentType type)
        {
            var current = GetCurrent(type);
            var defaultAttachment = GetDefault(type);
            return current == defaultAttachment && current != null;
        }

        /// <summary>
        /// Gets the state of an attachment type
        /// </summary>
        // 获取指定类型的配件状态：未装备 / 默认配件 / 自定义配件。
        public AttachmentState GetState(AttachmentType type)
        {
            var current = GetCurrent(type);
            
            if (current == null)
                return AttachmentState.None;
            
            var defaultAttachment = GetDefault(type);
            return current == defaultAttachment ? AttachmentState.Default : AttachmentState.Custom;
        }

        /// <summary>
        /// Verifies if any attachment is currently equipped or not
        /// </summary>
        // 判断当前是否装备了任意配件。
        public bool HasAnyAttachment()
        {
            foreach (var kvp in currentAttachments)
            {
                if (kvp.Value != null)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Gets the amount of currently equipped attachments.
        /// </summary>
        // 统计当前已装备的配件数量。
        public int GetCurrentCount()
        {
            int count = 0;
            foreach (var kvp in currentAttachments)
            {
                if (kvp.Value != null)
                    count++;
            }
            return count;
        }

        #endregion
    }

    // 配件状态枚举：描述某个配件槽位当前的装备情况。
    public enum AttachmentState
    {
        // 未装备任何配件
        None,       // No attachment equipped
        // 装备的是默认配件
        Default,    // Default attachment equipped
        // 装备的是非默认（自定义）配件
        Custom      // non default attachment equipped
    }
}
