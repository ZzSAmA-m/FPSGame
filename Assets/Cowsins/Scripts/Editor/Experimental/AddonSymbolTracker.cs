// =====================================================================
// AddonSymbolTracker.cs：资源后处理监听器，用于跟踪 Inventory Pro 与 Save&Load
// 两个 Add-On 的文件夹路径：文件夹被移动时更新记录的路径并持久化；
// 文件夹被删除时自动移除对应的宏定义（INVENTORY_PRO_ADD_ON / SAVE_LOAD_ADD_ON）。
// 仅在编辑器环境且启用了相应 Add-On 宏时编译。
// =====================================================================
#if UNITY_EDITOR && (SAVE_LOAD_ADD_ON || INVENTORY_PRO_ADD_ON)

using UnityEditor;
using UnityEngine;

/// <summary>
/// 资源后处理监听器：跟踪 Add-On 文件夹的移动/删除，并维护对应的编译宏定义。
/// </summary>
class AddonSymbolTracker : AssetPostprocessor
{
    // 记录 Inventory Pro Add-On 的当前文件夹路径（从 EditorPrefs 读取，移动后可更新）。
    private static string inventoryTargetFolder;
    // 记录 Save&Load Add-On 的当前文件夹路径（同上）。
    private static string saveLoadTargetFolder;

    // 静态构造函数：从 EditorPrefs 读取两个 Add-On 文件夹的当前路径作为初始值。
    static AddonSymbolTracker()
    {
        inventoryTargetFolder = EditorPrefs.GetString("InventoryFolder", "Assets/InventoryPro_ADD-ON");
        saveLoadTargetFolder = EditorPrefs.GetString("SaveLoadFolder", "Assets/Save&LoadSystem [ADD-ON]");
    }

    // 资源发生导入/删除/移动后的 Unity 回调：分别检查文件夹的移动与删除两种情况。
    static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
    {
        CheckFolderMoves(movedAssets, movedFromAssetPaths);
        CheckFolderDeletions(deletedAssets);
    }

    // 检查是否有 Add-On 文件夹被移动：若有，则把新路径更新到对应字段并写入 EditorPrefs。
    private static void CheckFolderMoves(string[] movedAssets, string[] movedFromAssetPaths)
    {
        for (int i = 0; i < movedAssets.Length; i++)
        {
            // 若移动前路径位于 Inventory 文件夹内，则用移动后的新路径更新记录。
            if (movedFromAssetPaths[i].StartsWith(inventoryTargetFolder))
            {
                inventoryTargetFolder = movedAssets[i];
                EditorPrefs.SetString("InventoryFolder", inventoryTargetFolder);
            }
            // 若移动前路径位于 Save&Load 文件夹内，则用移动后的新路径更新记录。
            else if (movedFromAssetPaths[i].StartsWith(saveLoadTargetFolder))
            {
                saveLoadTargetFolder = movedAssets[i];
                EditorPrefs.SetString("SaveLoadFolder", saveLoadTargetFolder);
            }
        }
    }

    // 检查是否有 Add-On 文件夹被删除：若有，则移除对应的编译宏定义。
    private static void CheckFolderDeletions(string[] deletedAssets)
    {
        foreach (string deletedAsset in deletedAssets)
        {
            if (deletedAsset.StartsWith(inventoryTargetFolder))
                RemoveDefineSymbol("INVENTORY_PRO_ADD_ON"); 
            else if (deletedAsset.StartsWith(saveLoadTargetFolder))
                RemoveDefineSymbol("SAVE_LOAD_ADD_ON"); 
        }
    }
    // 从当前构建目标组的脚本宏定义中移除指定的宏符号。
    private static void RemoveDefineSymbol(string defineSymbol)
    {
        // 获取当前选中的构建目标组；若为未知目标组则直接返回，不做处理。
        BuildTargetGroup currentBuildTargetGroup = EditorUserBuildSettings.selectedBuildTargetGroup;
        if (currentBuildTargetGroup == BuildTargetGroup.Unknown)
            return;
#pragma warning disable CS0618
        var defines = PlayerSettings.GetScriptingDefineSymbolsForGroup(currentBuildTargetGroup);
        if (defines.Contains(defineSymbol))
        {
            defines = defines.Replace(defineSymbol, "").Trim(';');
            PlayerSettings.SetScriptingDefineSymbolsForGroup(currentBuildTargetGroup, defines);
            Debug.LogWarning($"Removed define symbol: {defineSymbol}");
        }
#pragma warning restore CS0618
    }
}

#endif
