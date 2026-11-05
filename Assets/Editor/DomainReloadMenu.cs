using UnityEditor;

/// <summary>提供手动域重载入口。</summary>
public static class DomainReloadMenu
{
    /// <summary>按 Ctrl+Alt+R 请求一次域重载。</summary>
    [MenuItem("Tools/Reload Domain %&r")]
    private static void Reload()
    {
        EditorUtility.RequestScriptReload();
    }
}
