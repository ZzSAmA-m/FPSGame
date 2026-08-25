// ============================================================================
// CrosshairShapeEditor.cs —— CrosshairShape 的自定义 Inspector 编辑器。
// 仅在编辑器环境下编译。在 Inspector 中调用工具类绘制准星各部件（上下左右
// 线、中心点、角括号）的编辑区域，并应用序列化修改。
// ============================================================================
#if UNITY_EDITOR
using UnityEditor;
namespace cowsins
{
    /// <summary>
    /// CrosshairShape 的自定义 Inspector：绘制准星部件编辑界面。
    /// </summary>
    [CustomEditor(typeof(CrosshairShape))]
    public class CrosshairShapeEditor : Editor
    {
        // 绘制 Inspector：更新序列化对象，绘制准星编辑器区块，最后应用修改。
        override public void OnInspectorGUI()
        {
            serializedObject.Update();
            var myScript = target as CrosshairShape; // 获取被编辑的 CrosshairShape 实例

            CowsinsEditorWindowUtilities.DrawCrosshairEditorSection(serializedObject, myScript.DefaultCrosshairParts, "defaultCrosshairParts"); // 绘制准星部件编辑区域

            serializedObject.ApplyModifiedProperties(); // 应用修改
        }
    }
}
#endif