// 文件：FootstepSoundsDrawer.cs
// 作用：仅在 Unity 编辑器下编译的自定义属性绘制器（PropertyDrawer），用于在 Inspector 中
//       可视化编辑 PlayerMovementSettings.FootStepsSounds 结构（默认脚步声 + 按地面层级划分的脚步声列表）。
//       该绘制器为每个图层条目提供折叠与删除按钮，并提供“刷新图层”按钮，
//       依据玩家设置中的 whatIsGround 图层掩码自动生成各层级的脚步声条目。
#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

namespace cowsins
{
    /// <summary>
    /// 负责在 Inspector 中自定义绘制 FootStepsSounds 结构的属性绘制器。
    /// </summary>
    [CustomPropertyDrawer(typeof(PlayerMovementSettings.FootStepsSounds))]
    public class FootstepSoundsDrawer : PropertyDrawer
    {
        // 缓存每个图层条目（以图层名为 key）的折叠/展开状态，保证 Inspector 重绘时状态不丢失。
        private Dictionary<string, bool> foldouts = new Dictionary<string, bool>();

        /// <summary>
        /// 在 Inspector 中绘制该结构：先绘制默认脚步声字段，再逐项绘制按图层划分的脚步声列表，
        /// 每个条目带折叠与删除按钮，最后提供“刷新图层”按钮以便自动填充各图层。
        /// </summary>
        /// <param name="position">当前可绘制的矩形区域。</param>
        /// <param name="property">被绘制的序列化属性。</param>
        /// <param name="label">属性标签。</param>
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            // 分别取到“默认脚步声”与“按地面层级划分的脚步声列表”两个序列化子属性。
            var defaultStepProp = property.FindPropertyRelative("defaultStep");
            var surfaceSoundsProp = property.FindPropertyRelative("surfaceSounds");

            // yPos 记录当前绘制行的纵向位置，每绘制一个控件后向下累加。
            float yPos = position.y;

            // 绘制“默认脚步声”字段并累加其占用高度。
            var defaultRect = new Rect(position.x, yPos, position.width, EditorGUI.GetPropertyHeight(defaultStepProp));
            EditorGUI.PropertyField(defaultRect, defaultStepProp, true);
            yPos += EditorGUI.GetPropertyHeight(defaultStepProp);

            // Surface sounds list with foldouts
            for (int i = 0; i < surfaceSoundsProp.arraySize; i++)
            {
                // 获取列表中的第 i 个元素，并取出其图层名与音效数组两个子属性。
                var element = surfaceSoundsProp.GetArrayElementAtIndex(i);
                var layerNameProp = element.FindPropertyRelative("layerName");
                var soundsProp = element.FindPropertyRelative("sounds");

                // 以图层名作为折叠状态的 key；若尚未记录则默认展开。
                string layerKey = layerNameProp.stringValue;
                if (!foldouts.ContainsKey(layerKey))
                    foldouts[layerKey] = true;

                // Foldout showing only the layer name
                Rect foldoutRect = new Rect(position.x, yPos, position.width - 25, EditorGUIUtility.singleLineHeight);
                foldouts[layerKey] = EditorGUI.Foldout(foldoutRect, foldouts[layerKey], layerKey, true);

                // Remove button
                if (GUI.Button(new Rect(position.x + position.width - 20, yPos, 20, EditorGUIUtility.singleLineHeight), "×"))
                {
                    surfaceSoundsProp.DeleteArrayElementAtIndex(i);
                    foldouts.Remove(layerKey);
                    break;
                }

                yPos += EditorGUIUtility.singleLineHeight + 2;

                // 仅当该图层条目处于展开状态时，才绘制图层名字段与音效数组。
                if (foldouts[layerKey])
                {
                    // Layer name field (optional, can remove if fully hidden)
                    var layerRect = new Rect(position.x + 15, yPos, position.width - 15, EditorGUIUtility.singleLineHeight);
                    EditorGUI.PropertyField(layerRect, layerNameProp, new GUIContent("Layer Name"));
                    yPos += EditorGUIUtility.singleLineHeight + 2;

                    // Sounds array
                    var soundsRect = new Rect(position.x + 15, yPos, position.width - 15, EditorGUI.GetPropertyHeight(soundsProp));
                    EditorGUI.PropertyField(soundsRect, soundsProp, new GUIContent("Sounds"), true);
                    yPos += EditorGUI.GetPropertyHeight(soundsProp) + 10;
                }
            }

            yPos += EditorGUIUtility.singleLineHeight;

            // Auto-populate button
            if (GUI.Button(new Rect(position.x, yPos, position.width, EditorGUIUtility.singleLineHeight),
                          "Refresh Footsteps Layers"))
            {
                AutoPopulateFootsteps(property);
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
            float height = EditorGUIUtility.singleLineHeight * 4;

            // 累加“默认脚步声”字段的高度。
            var defaultStepProp = property.FindPropertyRelative("defaultStep");
            height += EditorGUI.GetPropertyHeight(defaultStepProp) + 10;

            // 遍历所有图层条目，按折叠状态累加每个条目的高度。
            var surfaceSoundsProp = property.FindPropertyRelative("surfaceSounds");
            for (int i = 0; i < surfaceSoundsProp.arraySize; i++)
            {
                var element = surfaceSoundsProp.GetArrayElementAtIndex(i);
                var soundsProp = element.FindPropertyRelative("sounds");

                string layerKey = element.FindPropertyRelative("layerName").stringValue;
                height += EditorGUIUtility.singleLineHeight + 2;

                if (!foldouts.ContainsKey(layerKey) || foldouts[layerKey])
                {
                    height += EditorGUIUtility.singleLineHeight + 2; // layer name
                    height += EditorGUI.GetPropertyHeight(soundsProp) + 10;
                }
            }

            return height;
        }

        // 自动填充脚步声列表：读取玩家设置的 whatIsGround 图层掩码，按掩码生成缺失的图层条目并应用修改。
        private void AutoPopulateFootsteps(SerializedProperty property)
        {
            var surfaceSoundsProp = property.FindPropertyRelative("surfaceSounds");
            LayerMask whatIsGround = GetWhatIsGroundLayerMask(property.serializedObject);
            PopulateFromLayerMask(surfaceSoundsProp, whatIsGround);
            property.serializedObject.ApplyModifiedProperties();
        }

        // 从序列化对象对应的 PlayerMovement 组件中读取 whatIsGround 图层掩码。
        private LayerMask GetWhatIsGroundLayerMask(SerializedObject serializedObject)
        {
            var target = (PlayerMovement)serializedObject.targetObject;
            return target.playerSettings.whatIsGround;
        }

        // 遍历 32 个图层位，凡被 whatIsGround 掩码选中且尚未存在的图层，就向列表追加一条对应条目。
        private void PopulateFromLayerMask(SerializedProperty surfaceSoundsProp, LayerMask whatIsGround)
        {
            for (int i = 0; i < 32; i++)
            {
                // 用位运算判断第 i 个图层是否被掩码选中。
                if ((whatIsGround & (1 << i)) != 0)
                {
                    string layerName = LayerMask.LayerToName(i);
                    if (string.IsNullOrEmpty(layerName))
                        layerName = $"Layer{i}";

                    if (layerName == "Default" || layerName == "Interactable")
                        continue;

                    if (!ContainsLayer(surfaceSoundsProp, layerName))
                        AddSurfaceEntry(surfaceSoundsProp, layerName, i);
                }
            }
        }

        // 检查列表中是否已经存在指定图层名的条目，避免重复添加。
        private bool ContainsLayer(SerializedProperty surfaceSoundsProp, string layerName)
        {
            for (int i = 0; i < surfaceSoundsProp.arraySize; i++)
            {
                var element = surfaceSoundsProp.GetArrayElementAtIndex(i);
                if (element.FindPropertyRelative("layerName").stringValue == layerName)
                    return true;
            }
            return false;
        }

        // 向列表末尾追加一条新条目，写入图层名与缓存图层索引，并清空音效数组、默认展开折叠。
        private void AddSurfaceEntry(SerializedProperty surfaceSoundsProp, string layerName, int layerIndex)
        {
            surfaceSoundsProp.arraySize++;
            var newEntry = surfaceSoundsProp.GetArrayElementAtIndex(surfaceSoundsProp.arraySize - 1);
            newEntry.FindPropertyRelative("layerName").stringValue = layerName;
            newEntry.FindPropertyRelative("cachedLayerIndex").intValue = layerIndex;

            var soundsProp = newEntry.FindPropertyRelative("sounds");
            soundsProp.ClearArray();

            foldouts[layerName] = true;
        }
    }
}
#endif
