// ----------------------------------------------------------------------
// Identifiable.cs
// 作用：为场景中的对象提供唯一 ID（uniqueID），用于存档系统的身份识别与按 ID 查找。
//       提供 ID 生成、替换、编辑器查重等方法；在存档插件（SAVE_LOAD_ADD_ON）启用时，
//       还负责对象字段的保存、加载以及动态实例化对象的登记。
// ----------------------------------------------------------------------
using UnityEngine;
#if SAVE_LOAD_ADD_ON
using cowsins.SaveLoad;
using Newtonsoft.Json.Linq;
#endif
using System.Collections.Generic;
using UnityEngine.SceneManagement;


#if UNITY_EDITOR
using UnityEditor;
#endif

namespace cowsins
{
    /// <summary>
    /// 场景对象唯一 ID 组件。为对象分配全局唯一标识，供存档系统进行身份识别与按 ID 查找；
    /// 提供 ID 生成、替换、查重等方法，并在存档插件启用时负责字段的保存、加载与动态实例登记。
    /// </summary>
    public class Identifiable : MonoBehaviour
    {
        // 对象的唯一标识字符串（在 Inspector 中隐藏，通常由系统自动生成）。
        [SerializeField, HideInInspector]
        protected string uniqueID;

        // 只读属性：获取对象的唯一 ID；若尚未分配，则在运行时自动生成一个。
        public string UniqueIDValue
        {
            get
            {
                // Generate an ID at runtime if it's not already assigned
                if (string.IsNullOrEmpty(uniqueID))
                {
                    // ID 为空则自动生成。
                    GenerateUniqueID();
                }
                return uniqueID;
            }
        }

#if UNITY_EDITOR
        // 编辑器校验：非播放模式下若 ID 重复或为空，则重新生成唯一 ID。
        public virtual void OnValidate()
        {
            // Detect duplicates in the Editor by checking if the uniqueID is already set.
            // If it's set and we�re not in play mode, then we might have a duplicate.
            if (!Application.isPlaying && (HasDuplicateID() || string.IsNullOrEmpty(uniqueID)))
            {
                GenerateUniqueID();
            }
        }

        // 检测场景中是否存在其他对象持有相同的唯一 ID（用于编辑器下查重）。
        private bool HasDuplicateID()
        {
            // Check if any other Identifiable object has the same uniqueID
            if (string.IsNullOrEmpty(uniqueID)) return false;
            
#pragma warning disable CS0618
            Identifiable[] allIdentifiables = FindObjectsOfType<Identifiable>();
#pragma warning restore CS0618
            foreach (Identifiable identifiable in allIdentifiables)
            {
                if (identifiable != this && !string.IsNullOrEmpty(identifiable.uniqueID) && identifiable.uniqueID == uniqueID)
                {
                    return true;
                }
            }
            return false;
        }
#endif

        // 生成一个新的全局唯一 ID（GUID）并赋值给 uniqueID；编辑器下标记对象为脏以保存。
        public void GenerateUniqueID()
        {
            uniqueID = System.Guid.NewGuid().ToString();
            // Debug.Log($"Assigned new UniqueID: {uniqueID}", this);

#if UNITY_EDITOR
            // Mark the object as dirty to save the new ID in the editor
            EditorUtility.SetDirty(this);
#endif
        }

        // 用指定值 newUniqueID 替换当前唯一 ID；编辑器下标记对象为脏以保存。
        public void ReplaceUniqueID(string newUniqueID)
        {
            uniqueID = newUniqueID;

#if UNITY_EDITOR
            // Mark the object as dirty to save the new ID in the editor
            EditorUtility.SetDirty(this);
#endif
        }

        // 按唯一 ID 在场景中查找对应的 Identifiable 对象；ID 为空或未找到时返回 null。
        /// <summary>
        /// Finds an Identifiable object by its unique ID in the scene.
        /// </summary>
        /// <param name="id">The unique ID to look for.</param>
        /// <returns>The Identifiable object if found, otherwise null.</returns>
        public static Identifiable FindByID(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
#pragma warning disable CS0618
            Identifiable[] allIdentifiables = FindObjectsOfType<Identifiable>();
#pragma warning restore CS0618
            foreach (Identifiable identifiable in allIdentifiables)
            {
                if (identifiable.UniqueIDValue == id)
                {
                    return identifiable;
                }
            }

            return null;
        }

#if SAVE_LOAD_ADD_ON
        // 收集并返回对象的存档数据（CustomSaveData），包含场景名与对象位置等字段。
        public virtual CustomSaveData SaveFields()
        {
            CustomSaveData saveData = new CustomSaveData(SceneManager.GetActiveScene().name, this.transform.position);
            saveData = (CustomSaveData)saveData.SaveFields(this);
            return saveData;
        }

        // 根据传入的存档数据 data 恢复对象字段，并调用 LoadedState 执行加载后的状态处理。
        public virtual void LoadFields(object data)
        {
            if (data is CustomSaveData gameData)
            {
                gameData.LoadFields(this, gameData);
            }

            LoadedState();
        }

        // 将当前对象的存档数据写入 GameDataManager 的自定义对象字典（以 uniqueID 为键）。
        public virtual void StoreData()
        {
            if (GameDataManager.instance == null) return;

            if (GameDataManager.instance.customObjectsData == null)
            {
                GameDataManager.instance.customObjectsData = new Dictionary<string, CustomSaveData>();
            }
            GameDataManager.instance.customObjectsData[this.uniqueID] = SaveFields();
        }

        // 保存动态实例化对象：生成存档数据、刷新 ID，并登记到实例化对象字典中。
        public virtual void SaveInstance()
        {
            if (GameDataManager.instance)
            {
                var saveData = SaveFields();
                GenerateUniqueID();
                saveData.Position = new SerializableVector3(transform.position);
                GameDataManager.instance.instantiatedObjects[UniqueIDValue] = saveData;
            }
        }

        // 加载后的状态处理钩子（虚方法，默认空实现，供子类重写以恢复加载后的状态）。
        public virtual void LoadedState()
        {

        }
#endif
    }
}