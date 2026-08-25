// ======================================================================
// Item_SO.cs —— 物品数据资产基类
// 派生自 ScriptableObject，定义物品(名称、描述、图标、拾取物模型、堆叠上限、
// 占用格子尺寸、重量等)的通用数据，供库存与拾取系统使用；并附带自定义 Inspector。
// ======================================================================
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif
#if INVENTORY_PRO_ADD_ON
using cowsins.Inventory;
#endif
namespace cowsins
{
    // Disallow creating Items if the Inventory Pro Add-On is not available
    /// <summary>物品数据基类(可序列化资产)：定义物品的名称、描述、图标、拾取物模型、堆叠与重量等通用属性，供库存/拾取系统使用。</summary>
    [System.Serializable]
#if INVENTORY_PRO_ADD_ON
[CreateAssetMenu(fileName = "newInventoryItem", menuName = "COWSINS/Inventory/New Inventory Item", order = 1)]
#endif
    public class Item_SO : ScriptableObject
    {
        // 物品名称(例如: Glock)。
        [Tooltip("Your item name. Ex: Glock")] public string _name;
        // 物品描述文本，库存插件会在悬停槽位时展示。
        [Tooltip("You can describe your item here. Inventory Add-On will show this description on hovering slots in the inventory.")] public string description;
        // 物品的自定义图标。
        [Tooltip("Custom image of your item")] public Sprite icon;
        // 物品被丢弃到场景后显示的视觉模型预制体。
        [Tooltip("Visuals that will appear on a dropped item.")] public GameObject pickUpGraphics;
        // 单个物品槽的最大堆叠数量。
        [Range(1, 99)] public int maxStack = 1;
        // 物品在库存网格中占用的尺寸(宽 x 高)。
        public Vector2Int itemSize = new Vector2Int(1, 1);
        // 不规则尺寸物品使用的专用图标，避免图标拉伸变形。
        public Sprite irregularItemIcon;


        // 若为 true，则物品/武器的重量会影响玩家移动速度。
        [Tooltip("Velocity will be decreased depending on the weight of the weapon if this is true.")] public bool applyWeight;
        // 整叠物品的重量系数，用于影响移动速度。
        [Range(.2f, 1)]
        [Tooltip("Represents the weight of an entire stack of this item.")] public float weightMultiplier = 1f;

#if INVENTORY_PRO_ADD_ON
    /// <summary>使用物品：减少当前物品数量；若数量耗尽则清空该槽位区域，否则刷新锚点槽及其占用区域所有槽位的显示。</summary>
    /// <param name="inventoryProManager">库存管理器，用于获取网格/槽位操作接口。</param>
    /// <param name="inventorySlot">被使用的物品所在的库存槽位。</param>
    public virtual void Use(InventoryProManager inventoryProManager, InventorySlot inventorySlot)
    {
        InventorySlot anchor = inventorySlot.GetAnchorSlot();
        anchor.slotData.amount -= 1;
        if(anchor.slotData.amount <= 0)
        {
            inventoryProManager._GridGenerator.Operations.ClearSlotArea(inventorySlot);
            return;
        }
        Vector2Int size = inventorySlot.GetAnchorSlot().GetItemSize();
        int startRow = inventorySlot.row;
        int startCol = inventorySlot.col;

        for (int i = 0; i < size.y; i++)
        {
            for (int j = 0; j < size.x; j++)
            {
                InventorySlot[,] inventorySlots = inventorySlot.IsInventorySlot ? inventoryProManager.InventorySlots : inventoryProManager.ChestSlots;

                inventorySlots[startRow + i, startCol + j].anchorSlot = anchor;
                inventorySlots[startRow + i, startCol + j].UpdateSlotGraphics();
            }
        }
    }
#endif
    }
}

#if UNITY_EDITOR

namespace cowsins
{
    [CustomEditor(typeof(Item_SO), true)]
    /// <summary>Item_SO 的自定义 Inspector：按分类展示物品基础设置、库存插件相关设置与其余派生属性，并根据插件是否安装启用/禁用相关功能。</summary>
    public class Item_SOEditor : Editor
    {
        // 记录库存插件是否已安装，用于控制 Inspector 中相关字段的可用状态。
        private bool isInventoryAddonAvailable;
        // 初始化：通过编译宏判断库存插件是否可用，并将结果缓存到字段中。
        private void OnEnable()
        {
            // Check if the Inventory Addon is available
            #if INVENTORY_PRO_ADD_ON
            isInventoryAddonAvailable = true;
            #else
            isInventoryAddonAvailable = false;
            #endif
        }

        /// <summary>绘制自定义 Inspector：依次展示名称、图标、拾取物模型、重量，以及库存插件相关字段，并对不规则尺寸给出提示。</summary>
        override public void OnInspectorGUI()
        {
            serializedObject.Update();
            var myScript = target as Item_SO;

            EditorGUILayout.PropertyField(serializedObject.FindProperty("_name"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("icon"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("pickUpGraphics"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("applyWeight"));
            if (myScript.applyWeight)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(serializedObject.FindProperty("weightMultiplier"));
                EditorGUI.indentLevel--;
            }
            else myScript.weightMultiplier = 1;

            GUI.enabled = isInventoryAddonAvailable;

            EditorGUILayout.Space(15);
            EditorGUILayout.LabelField("INVENTORY PRO MANAGER", EditorStyles.boldLabel);
            if (!isInventoryAddonAvailable)
            {
                EditorGUILayout.Space(10);
                EditorGUILayout.HelpBox("These features are locked. Inventory Pro Manager + Save & Load Add-On is not installed.", MessageType.Info);
                EditorGUILayout.Space(5);
            }
            EditorGUILayout.PropertyField(serializedObject.FindProperty("maxStack"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("itemSize"));
            if (myScript.itemSize.x != myScript.itemSize.y)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.Space(5);
                EditorGUILayout.HelpBox($"Your item has an irregular size [{myScript.itemSize.x} : {myScript.itemSize.y}], which may cause icon stretching." +
                "To maintain the correct aspect ratio, consider setting 'irregularItemIcon' with a properly scaled icon.", MessageType.Info);
                EditorGUILayout.Space(5);
                EditorGUILayout.PropertyField(serializedObject.FindProperty("irregularItemIcon"));
                EditorGUI.indentLevel--;
            }
            GUI.enabled = true;
            EditorGUILayout.Space(15);
            EditorGUILayout.LabelField("SPECIFIC SETTINGS", EditorStyles.boldLabel);
            DrawPropertiesExcluding(serializedObject, "m_Script", "id", "icon", "_name", "pickUpGraphics", "applyWeight", "weightMultiplier", "maxStack", "itemSize", "irregularItemIcon");

            serializedObject.ApplyModifiedProperties();
        }
    }
}
#endif