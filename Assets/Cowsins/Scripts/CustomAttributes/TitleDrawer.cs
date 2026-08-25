// =====================================================================
// TitleDrawer.cs：为 [Title] 特性提供自定义属性绘制器（PropertyDrawer）。
// 在 Inspector 中绘制一个加粗标题、可选的分隔线以及其后的属性字段，
// 用于为字段分组添加视觉标题。仅在编辑器环境（UNITY_EDITOR）下编译。
// =====================================================================
#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

    namespace cowsins
    {
        /// <summary>
        /// TitleAttribute 的自定义属性绘制器：负责渲染标题文本、可选分隔线以及属性字段本身。
        /// </summary>
        [CustomPropertyDrawer(typeof(TitleAttribute))]
        public class TitleDrawer : PropertyDrawer
        {
            // 字段之间的额外间隔（此处未赋值，默认为 0），参与 GetPropertyHeight 的高度计算。
            float propertySeparation;
            /// <summary>
            /// 绘制该属性：先绘制标题，再（可选）绘制分隔线，最后绘制属性字段本身。
            /// </summary>
            public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
            {
                TitleAttribute titleAttribute = (TitleAttribute)attribute;

                // Create style for the title
                GUIStyle titleStyle = new GUIStyle(EditorStyles.boldLabel)
                {
                    fontSize = 16,
                    fontStyle = FontStyle.Bold
                };

                // Calculate rects
                Rect titleRect = new Rect(position.x, position.y + titleAttribute.upMargin, position.width, EditorGUIUtility.singleLineHeight);
                float spacing = 1f;
                float dividerHeight = titleAttribute.divider ? 2f : 0f;
                Rect dividerRect = new Rect(position.x, titleRect.yMax + spacing, position.width, dividerHeight); 
                Rect propertyRect = new Rect(position.x, dividerRect.yMax + 3, position.width, EditorGUIUtility.singleLineHeight);


                // Draw title
                EditorGUI.LabelField(titleRect, titleAttribute.title, titleStyle);

                // Draw divider if enabled
                if (titleAttribute.divider)
                {
                    EditorGUI.DrawRect(dividerRect, Color.grey);
                }

                EditorGUI.PropertyField(propertyRect, property, new GUIContent(property.displayName));
        }

            /// <summary>
            /// 计算并返回属性占用的总高度：属性行、字段间隔、分隔线高度与标题上边距之和。
            /// </summary>
            public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
            {
                return ( EditorGUIUtility.singleLineHeight + propertySeparation ) * 2.1f + 4 + ((TitleAttribute)attribute).upMargin;
            }
        }
    }


#endif
