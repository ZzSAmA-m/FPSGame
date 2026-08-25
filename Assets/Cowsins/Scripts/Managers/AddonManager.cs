// 文件：AddonManager.cs
// 作用：检测并记录项目是否启用了可选的 Add-on（扩展包），具体为 Inventory Pro 与 Save/Load 两个扩展包。
//       通过编译宏（#if）在运行时判断对应扩展包是否被编译进项目，
//       供其它模块查询并据此决定是否启用相关功能。
using UnityEngine;

namespace cowsins
{
    /// <summary>
    /// 用于检测并标记 Inventory 与 Save/Load 两个可选扩展包是否可用的管理器单例。
    /// </summary>
    public class AddonManager : MonoBehaviour
    {
        // 是否可用 Inventory Pro 扩展包（该字段隐藏于 Inspector）。
        [HideInInspector] public bool isInventoryAddonAvailable;
        // 是否可用 Save/Load 扩展包（该字段隐藏于 Inspector）。
        [HideInInspector] public bool isSaveLoadAddonAvailable;

        // AddonManager 的单例引用，供全局访问。
        public static AddonManager instance;

        // 初始化：将自身设为单例，并根据编译宏判断两个扩展包是否可用。
        private void Awake()
        {
            instance = this;

            // 若编译时定义了 INVENTORY_PRO_ADD_ON 宏，则标记 Inventory 扩展包可用，否则不可用。
#if INVENTORY_PRO_ADD_ON
            isInventoryAddonAvailable = true;
#else
            isInventoryAddonAvailable = false;
#endif
            // 若编译时定义了 SAVE_LOAD_ADD_ON 宏，则标记 Save/Load 扩展包可用，否则不可用。
#if SAVE_LOAD_ADD_ON
            isSaveLoadAddonAvailable = true;
#else
            isSaveLoadAddonAvailable = false;
#endif
        }
    }

}