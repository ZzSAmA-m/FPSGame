using UnityEngine;
using System.Collections.Generic;
using System.Linq;

// ===========================================================================================
// CompatibleAttachments.cs —— 武器兼容配件容器
// 职责：存储某把武器“可装备”的配件，按配件类型分组。为兼容 Unity 序列化，编辑器使用列表
//       compatibleAttachmentsList 展示，运行时惰性构建字典用于快速查询。
// ===========================================================================================
namespace cowsins
{
    /// <summary>
    /// Stores the compatible attachments of a WeaponIdentification object.
    /// </summary>
    // 兼容配件容器：记录武器各配件类型下可装备的配件，供配件状态管理与检视 UI 查询。
    [System.Serializable]
    public class CompatibleAttachments
    {
        // Used to Display the Compatible Attachments in the WeaponIdentificationEditor.cs
        // Dictionaries themselves are not serializable in Unity, so they cannot be shown in the Editor just like that.
        [System.Serializable]
        /// <summary>
        /// 单个兼容配件条目：记录配件类型与其可装备的配件数组。
        /// </summary>
        public class CompatibleAttachmentEntry
        {
            // 配件类型
            public AttachmentType type;
            // 该类型下所有可装备的配件
            public Attachment[] attachments;
        }

        // This is a Temporary list used for Editor Display reasons. Attachments during runtime are held by compatibleAttachmentsDict.
        // 供编辑器展示的可序列化列表；运行时数据由 compatibleAttachmentsDict 字典持有
        [SerializeField] private List<CompatibleAttachmentEntry> compatibleAttachmentsList = new List<CompatibleAttachmentEntry>();

        // 运行时实际使用的字典（惰性初始化，从列表构建）
        private Dictionary<AttachmentType, Attachment[]> compatibleAttachmentsDict;

        // Ensures Dictionary Initialization + Avoids Duplicates 
        // 确保字典已初始化：首次调用时从列表构建字典，并跳过重复的配件类型。
        private void EnsureDictInitialized()
        {
            if (compatibleAttachmentsDict == null)
            {
                compatibleAttachmentsDict = new Dictionary<AttachmentType, Attachment[]>();
                foreach (var entry in compatibleAttachmentsList)
                {
                    if (entry != null && !compatibleAttachmentsDict.ContainsKey(entry.type))
                    {
                        compatibleAttachmentsDict.Add(entry.type, entry.attachments);
                    }
                }
            }
        }

        // 返回指定配件类型下所有兼容的配件（无则返回空数组）。
        /// <summary>
        /// Returns the Compatible Attachment for the Specific Attachment Type given.
        /// </summary>
        /// <param name="type">Attachment Type to return</param>
        /// <returns></returns>
        public IReadOnlyList<Attachment> GetCompatible(AttachmentType type)
        {
            EnsureDictInitialized();

            if (compatibleAttachmentsDict.TryGetValue(type, out var attachments))
                return attachments;

            return new Attachment[0];
        }
    }
}