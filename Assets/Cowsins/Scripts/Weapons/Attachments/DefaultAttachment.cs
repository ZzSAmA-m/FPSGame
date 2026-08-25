using System.Collections.Generic;
using UnityEngine;

// ===========================================================================================
// DefaultAttachment.cs —— 武器默认配件容器
// 职责：存储某把武器各配件槽位的“默认配件”（如出厂自带枪管 / 瞄准镜）。为兼容 Unity
//       序列化，编辑器中使用可序列化列表 defaultAttachmentsList 展示，运行时惰性构建字典
//       defaultAttachments 供快速查询。
// ===========================================================================================
namespace cowsins
{
    [System.Serializable]
    /// <summary>
    /// 默认配件容器：记录武器各配件类型的默认配件，提供字典形式的惰性访问。
    /// </summary>
    public class DefaultAttachment
    {
        // Used to Display the Default Attachments in the WeaponIdentificationEditor.cs
        // Dictionaries themselves are not serializable in Unity, so they cannot be shown in the Editor just like that.
        [System.Serializable]
        /// <summary>
        /// 单个默认配件条目：记录配件类型与其对应的默认配件实例。
        /// </summary>
        public class AttachmentEntry
        {
            // 配件类型
            public AttachmentType type;
            // 该类型对应的默认配件实例
            public Attachment attachment;
        }

        // This is a Temporary list used for Editor Display reasons. Attachments during runtime are held by defaultAttachments.
        // 供编辑器展示用的可序列化列表（Unity 无法直接序列化字典），运行时数据由下面的字典持有
        [SerializeField]
        private List<AttachmentEntry> defaultAttachmentsList = new List<AttachmentEntry>();

        // This is the Dictionary that actually contains our runtime attachment Info.
        // 运行时真正持有默认配件数据的字典（不可序列化，使用时惰性初始化）
        [System.NonSerialized]
        private Dictionary<AttachmentType, Attachment> defaultAttachments;

        // Default Attachments Getter
        // Ensures Dictionary Initialization + Avoids Duplicates 
        // 默认配件字典的访问入口：首次访问时从列表构建字典，并跳过重复类型避免冲突。
        public Dictionary<AttachmentType, Attachment> DefaultAttachments
        {
            get
            {
                if (defaultAttachments == null)
                {
                    defaultAttachments = new Dictionary<AttachmentType, Attachment>();
                    foreach (var entry in defaultAttachmentsList)
                    {
                        if (entry != null && !defaultAttachments.ContainsKey(entry.type))
                            defaultAttachments.Add(entry.type, entry.attachment);
                    }
                }
                return defaultAttachments;
            }
        }
    }
}