// ReadOnlyAttribute.cs：只读属性特性及其自定义 Inspector 绘制器。
// 在字段上标注 [ReadOnly] 后，该字段在 Inspector 中会以灰色只读方式显示，禁止被编辑。
// 绘制器逻辑仅在编辑器（UNITY_EDITOR）下编译，不会影响运行时行为。
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;

namespace cowsins
{
    /// <summary>
    /// 只读标记特性：被 [ReadOnly] 标注的字段在 Inspector 中仅作展示、不可编辑，本身不携带任何数据。
    /// </summary>
    public class ReadOnlyAttribute : PropertyAttribute { }

#if UNITY_EDITOR
    /// <summary>
    /// 只读属性绘制器：将标注了 [ReadOnly] 的字段在 Inspector 中渲染为不可编辑状态。
    /// </summary>
    [CustomPropertyDrawer(typeof(ReadOnlyAttribute))]
    public class ReadOnlyDrawer : PropertyDrawer
    {
        /// <summary>
        /// 返回属性在 Inspector 中占用的高度，直接复用 EditorGUI 对多行属性的默认高度计算。
        /// </summary>
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return EditorGUI.GetPropertyHeight(property, label, true);
        }

        /// <summary>
        /// 在指定矩形区域绘制属性字段：先临时禁用 GUI 交互，再调用 EditorGUI.PropertyField 绘制，随后恢复可用状态。
        /// </summary>
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            GUI.enabled = false;
            EditorGUI.PropertyField(position, property, label, true);
            GUI.enabled = true;
        }
    }
#endif
}