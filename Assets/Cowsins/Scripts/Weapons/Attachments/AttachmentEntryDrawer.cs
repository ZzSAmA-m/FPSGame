#if UNITY_EDITOR

using UnityEditor;
using UnityEngine;

// ===========================================================================================
// AttachmentEntryDrawer.cs —— 默认配件条目的属性绘制器
// 职责：为 DefaultAttachment.AttachmentEntry 自定义 Inspector 显示，将“类型 + 配件”两项
//       并排绘制在同一行，方便在武器编辑器中配置默认配件。
// ===========================================================================================
namespace cowsins
{
    [CustomPropertyDrawer(typeof(DefaultAttachment.AttachmentEntry))]
    /// <summary>
    /// 默认配件条目绘制器：把 AttachmentEntry 的 type 与 attachment 字段并排显示。
    /// </summary>
    public class AttachmentEntryDrawer : PropertyDrawer
    {
        private const float Spacing = 4f;

        /// <summary>
        /// 绘制条目：将 type 与 attachment 两个字段左右并排绘制，且不显示字段标题。
        /// </summary>
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            // 获取子属性（类型与配件引用）
            var typeProperty = property.FindPropertyRelative("type");
            var attachmentProperty = property.FindPropertyRelative("attachment");

            // 计算左右两个字段的宽度（各占一半，中间留出间距）
            float fieldWidth = (position.width - Spacing) / 2f;

            // 左侧：类型字段区域
            var typeRect = new Rect(position.x, position.y, fieldWidth, position.height);
            // 右侧：配件字段区域
            var attachmentRect = new Rect(position.x + fieldWidth + Spacing, position.y, fieldWidth, position.height);

            // 绘制两个字段，GUIContent.None 用于隐藏字段标题
            EditorGUI.PropertyField(typeRect, typeProperty, GUIContent.none);
            EditorGUI.PropertyField(attachmentRect, attachmentProperty, GUIContent.none);

            EditorGUI.EndProperty();
        }

        /// <summary>
        /// 返回条目高度：单行高度。
        /// </summary>
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return EditorGUIUtility.singleLineHeight;
        }
    }
}

#endif