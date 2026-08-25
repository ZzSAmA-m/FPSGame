// 文件：ImpactEffectsDrawer.cs
// 作用：仅在 Unity 编辑器下编译的自定义属性绘制器（PropertyDrawer），用于在 Inspector 中
//       可视化编辑 WeaponControllerSettings.ImpactEffects 结构（默认命中特效 + 按图层划分的命中特效列表）。
//       该绘制器为每个图层条目提供折叠与删除按钮，并提供“刷新图层”按钮，
//       遍历全部 32 个图层自动生成对应层级的命中特效条目。
#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

namespace cowsins
{
    /// <summary>
    /// 负责在 Inspector 中自定义绘制 ImpactEffects 结构的属性绘制器。
    /// </summary>
    [CustomPropertyDrawer(typeof(WeaponControllerSettings.ImpactEffects))]
    public class ImpactEffectsDrawer : PropertyDrawer
    {
        // 缓存每个图层条目（以图层名为 key）的折叠/展开状态，保证 Inspector 重绘时状态不丢失。
        private Dictionary<string, bool> foldouts = new Dictionary<string, bool>();

        /// <summary>
        /// 在 Inspector 中绘制该结构：先绘制默认命中特效字段，再逐项绘制按图层划分的命中特效列表，
        /// 每个条目带折叠与删除按钮，最后提供“刷新图层”按钮以便自动填充各图层。
        /// </summary>
        /// <param name="position">当前可绘制的矩形区域。</param>
        /// <param name="property">被绘制的序列化属性。</param>
        /// <param name="label">属性标签。</param>
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            // 分别取到“默认命中特效”与“按图层划分的命中特效列表”两个序列化子属性。
            var defaultImpactProp = property.FindPropertyRelative("defaultImpact");
            var impactsProp = property.FindPropertyRelative("impacts");

            // yPos 记录当前绘制行的纵向位置，每绘制一个控件后向下累加。
            float yPos = position.y;

            // Default impact field
            var defaultRect = new Rect(position.x, yPos, position.width, EditorGUI.GetPropertyHeight(defaultImpactProp));
            EditorGUI.PropertyField(defaultRect, defaultImpactProp, true);
            yPos += EditorGUI.GetPropertyHeight(defaultImpactProp) + 5;

            // Impacts list with foldouts
            for (int i = 0; i < impactsProp.arraySize; i++)
            {
                // 获取列表中的第 i 个元素，并取出其图层名与命中特效两个子属性。
                var element = impactsProp.GetArrayElementAtIndex(i);
                var layerNameProp = element.FindPropertyRelative("layerName");
                var impactProp = element.FindPropertyRelative("impact");

                // 以图层名作为折叠状态的 key；若为空则用 "Layer{i}" 兜底。
                string layerKey = layerNameProp.stringValue;
                if (string.IsNullOrEmpty(layerKey))
                    layerKey = $"Layer{i}";

                if (!foldouts.ContainsKey(layerKey))
                    foldouts[layerKey] = true;

                // Foldout row
                Rect foldoutRect = new Rect(position.x, yPos, position.width - 25, EditorGUIUtility.singleLineHeight);
                foldouts[layerKey] = EditorGUI.Foldout(foldoutRect, foldouts[layerKey], layerKey, true);

                // Remove button
                if (GUI.Button(new Rect(position.x + position.width - 20, yPos, 20, EditorGUIUtility.singleLineHeight), "x"))
                {
                    impactsProp.DeleteArrayElementAtIndex(i);
                    foldouts.Remove(layerKey);
                    break;
                }

                yPos += EditorGUIUtility.singleLineHeight + 2;

                // 仅当该图层条目处于展开状态时，才绘制图层名字段与命中特效字段。
                if (foldouts[layerKey])
                {
                    // Layer name field
                    var layerRect = new Rect(position.x + 15, yPos, position.width - 15, EditorGUIUtility.singleLineHeight);
                    EditorGUI.PropertyField(layerRect, layerNameProp, new GUIContent("Layer Name"));
                    yPos += EditorGUIUtility.singleLineHeight + 2;

                    // Impact prefab field
                    var impactRect = new Rect(position.x + 15, yPos, position.width - 15, EditorGUI.GetPropertyHeight(impactProp));
                    EditorGUI.PropertyField(impactRect, impactProp, new GUIContent("Impact Prefab"), true);
                    yPos += EditorGUI.GetPropertyHeight(impactProp) + 10;
                }
            }

            yPos += EditorGUIUtility.singleLineHeight;

            // Auto-populate button
            if (GUI.Button(new Rect(position.x, yPos, position.width, EditorGUIUtility.singleLineHeight),
                          "Refresh Impact Layers"))
            {
                AutoPopulateImpacts(property);
            }

            EditorGUI.EndProperty();
        }

        /// <summary>
        /// 计算并返回该属性在 Inspector 中所需的总高度，与 OnGUI 中实际绘制的控件高度保持一致。
        /// </summary>
        /// <param name="property">被绘制的序列化属性。</param>
        /// <param name="label">属性标签。</param>
        /// <returns>属性块所需的总像素高度。</returns>
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            // 基础高度：预留默认字段、刷新按钮及各种间距等固定高度。
            float height = EditorGUIUtility.singleLineHeight * 3;

            // 累加“默认命中特效”字段的高度。
            var defaultImpactProp = property.FindPropertyRelative("defaultImpact");
            height += EditorGUI.GetPropertyHeight(defaultImpactProp) + 10;

            // 遍历所有图层条目，按折叠状态累加每个条目的高度。
            var impactsProp = property.FindPropertyRelative("impacts");
            for (int i = 0; i < impactsProp.arraySize; i++)
            {
                var element = impactsProp.GetArrayElementAtIndex(i);
                var impactProp = element.FindPropertyRelative("impact");

                string layerKey = element.FindPropertyRelative("layerName").stringValue;
                if (string.IsNullOrEmpty(layerKey)) layerKey = $"Layer{i}";

                height += EditorGUIUtility.singleLineHeight + 2;

                if (!foldouts.ContainsKey(layerKey) || foldouts[layerKey])
                {
                    height += EditorGUIUtility.singleLineHeight + 2; // layer name
                    height += EditorGUI.GetPropertyHeight(impactProp) + 10;
                }
            }

            return height;
        }

        // 自动填充命中特效列表：遍历全部 32 个图层，为尚未存在的有效图层追加条目并应用修改。
        private void AutoPopulateImpacts(SerializedProperty property)
        {
            var impactsProp = property.FindPropertyRelative("impacts");
            for (int i = 0; i < 32; i++)
            {
                // 获取第 i 个图层的名称；无名称的图层直接跳过。
                string layerName = LayerMask.LayerToName(i);
                if (string.IsNullOrEmpty(layerName)) continue;

                // 跳过 Unity 内建的 Default 与 Ignore Raycast 图层。
                if (layerName == "Default" || layerName == "Ignore Raycast") continue;

                if (!ContainsLayer(impactsProp, layerName))
                    AddImpactEntry(impactsProp, layerName, i);
            }
            property.serializedObject.ApplyModifiedProperties();
        }

        // 检查列表中是否已经存在指定图层名的条目，避免重复添加。
        private bool ContainsLayer(SerializedProperty impactsProp, string layerName)
        {
            for (int i = 0; i < impactsProp.arraySize; i++)
            {
                var element = impactsProp.GetArrayElementAtIndex(i);
                if (element.FindPropertyRelative("layerName").stringValue == layerName)
                    return true;
            }
            return false;
        }

        // 向列表末尾追加一条新条目，写入图层名与缓存图层索引，命中特效引用置空并默认展开折叠。
        private void AddImpactEntry(SerializedProperty impactsProp, string layerName, int layerIndex)
        {
            impactsProp.arraySize++;
            var newEntry = impactsProp.GetArrayElementAtIndex(impactsProp.arraySize - 1);
            newEntry.FindPropertyRelative("layerName").stringValue = layerName;
            newEntry.FindPropertyRelative("cachedLayerIndex").intValue = layerIndex;
            newEntry.FindPropertyRelative("impact").objectReferenceValue = null;

            foldouts[layerName] = true;
        }
    }
}
#endif
