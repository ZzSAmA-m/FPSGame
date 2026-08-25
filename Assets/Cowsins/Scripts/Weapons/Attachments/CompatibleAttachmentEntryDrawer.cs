#if UNITY_EDITOR

using UnityEditor;
using UnityEngine;
using System.Collections.Generic;

// ===========================================================================================
// CompatibleAttachmentEntryDrawer.cs —— 兼容配件条目的属性绘制器
// 职责：为 CompatibleAttachments.CompatibleAttachmentEntry 自定义 Inspector 显示，提供
//       可折叠的配件列表（foldout），支持添加 / 删除配件，并分别追踪每个条目的折叠状态。
// ===========================================================================================
namespace cowsins
{
    [CustomPropertyDrawer(typeof(CompatibleAttachments.CompatibleAttachmentEntry))]
    /// <summary>
    /// 兼容配件条目绘制器：为每个配件类型绘制可折叠列表，支持增删配件。
    /// </summary>
    public class CompatibleAttachmentEntryDrawer : PropertyDrawer
    {
        // 分别记录每个条目（按属性路径哈希值）的折叠状态，避免多个条目共享同一折叠开关
        // Controls the state of the foldout of each compatible attachment list ( whether they are folded or unfolded individually )
        private readonly Dictionary<int, bool> foldoutStates = new Dictionary<int, bool>();

        /// <summary>
        /// 绘制条目：先绘制类型字段，再绘制一个按类型命名的折叠列表；
        /// 展开后逐个绘制配件元素，并提供删除（-）与添加（Add xxx）按钮。
        /// </summary>
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            // 获取子属性（配件类型与配件数组）
            var typeProperty = property.FindPropertyRelative("type");
            var attachmentsProperty = property.FindPropertyRelative("attachments");

            // 绘制类型字段
            var typeRect = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
            EditorGUI.PropertyField(typeRect, typeProperty);

            // 用属性路径的哈希值作为键，确保每个条目的折叠状态相互独立
            var foldoutKey = property.propertyPath.GetHashCode();
            if (!foldoutStates.ContainsKey(foldoutKey))
                foldoutStates[foldoutKey] = false;

            // 获取当前类型的显示名称，用于折叠标签；名称异常时回退为 "Attachment"
            string typeName = typeProperty.enumDisplayNames[typeProperty.enumValueIndex];
            if (string.IsNullOrEmpty(typeName))
                typeName = "Attachment";

            // 构造折叠标题，例如 "Compatible Scopes"
            string foldoutLabel = $"Compatible {typeName}s";

            // 绘制折叠开关
            var foldoutRect = new Rect(position.x, typeRect.y + EditorGUIUtility.singleLineHeight + 2, position.width, EditorGUIUtility.singleLineHeight);
            foldoutStates[foldoutKey] = EditorGUI.Foldout(foldoutRect, foldoutStates[foldoutKey], foldoutLabel, true);

            float currentY = foldoutRect.y + EditorGUIUtility.singleLineHeight + 2;

            // 若当前折叠项处于展开状态，则绘制列表内容
            if (foldoutStates[foldoutKey])
            {
                EditorGUI.indentLevel++;

                // 逐个绘制列表中的配件元素
                for (int i = 0; i < attachmentsProperty.arraySize; i++)
                {
                    var element = attachmentsProperty.GetArrayElementAtIndex(i);
                    float elementHeight = EditorGUI.GetPropertyHeight(element, true);

                    var elementRect = new Rect(position.x, currentY, position.width - 20, elementHeight);
                    EditorGUI.PropertyField(elementRect, element, new GUIContent($"{typeName} {i + 1}"), true);

                    // 绘制删除按钮（-）：点击后删除该元素并退出循环
                    var removeBtnRect = new Rect(position.x + position.width - 18, currentY, 18, EditorGUIUtility.singleLineHeight);
                    if (GUI.Button(removeBtnRect, "-"))
                    {
                        attachmentsProperty.DeleteArrayElementAtIndex(i);
                        break;
                    }

                    currentY += elementHeight + 2;
                }

                // 绘制添加按钮（Add xxx）：点击后在数组末尾追加一个元素
                float addBtnWidth = 125f;
                var addBtnRect = new Rect(position.x + position.width - addBtnWidth, currentY, addBtnWidth, EditorGUIUtility.singleLineHeight * 1.5f);

                if (GUI.Button(addBtnRect, $"Add {typeName}"))
                {
                    attachmentsProperty.InsertArrayElementAtIndex(attachmentsProperty.arraySize);
                }

                EditorGUI.indentLevel--;
            }

            EditorGUI.EndProperty();
        }


        /// <summary>
        /// 计算条目总高度：类型字段 + 折叠标题；若展开，再累加每个元素的高度与“添加”按钮的高度。
        /// </summary>
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            var attachmentsProp = property.FindPropertyRelative("attachments");
            var foldoutKey = property.propertyPath.GetHashCode();

            // 基础高度：类型字段 + 折叠标题（各一行，外加间距）
            float height = EditorGUIUtility.singleLineHeight + EditorGUIUtility.singleLineHeight + 2;

            // 若展开，累加每个元素的高度
            if (foldoutStates.TryGetValue(foldoutKey, out bool isExpanded) && isExpanded)
            {
                for (int i = 0; i < attachmentsProp.arraySize; i++)
                {
                    var element = attachmentsProp.GetArrayElementAtIndex(i);
                    height += EditorGUI.GetPropertyHeight(element, true) + 2;
                }

                // 为“添加”按钮预留一行空间
                height += EditorGUIUtility.singleLineHeight + 2;
            }
            height += 10;
            return height;
        }
    }
}

#endif