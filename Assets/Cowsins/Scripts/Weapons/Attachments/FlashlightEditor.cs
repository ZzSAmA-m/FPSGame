#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

// ===========================================================================================
// FlashlightEditor.cs —— 手电筒自定义 Inspector 编辑器
// 职责：为 Flashlight 组件提供自定义 Inspector，当未指定光源时显示“Create Spotlight”按钮，
//       一键创建并配置聚光灯作为手电筒光源。
// ===========================================================================================
namespace cowsins
{
    // 手电筒编辑器：辅助用户快速创建一个聚光灯（Spotlight）作为手电筒光源。
    /// <summary>
    /// Simple Editor script for the Flashlight.
    /// Assists the user to easily create a Spotlight for a flashlight.
    /// </summary>
    [CustomEditor(typeof(Flashlight))]
    public class FlashlightEditor : Editor
    {
        /// <summary>
        /// 绘制自定义 Inspector：默认绘制所有字段；若未分配光源则显示“Create Spotlight”按钮，
        /// 点击后在挂载对象下创建聚光灯并绑定到 lightSource 字段。
        /// </summary>
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            DrawDefaultInspector();

            // 获取目标 Flashlight 组件与其 lightSource 序列化属性
            var flashlight = (Flashlight)target;
            SerializedProperty lightProp = serializedObject.FindProperty("lightSource");

            Light assignedLight = lightProp.objectReferenceValue as Light;

            // 若层级中未找到已分配的光源，则显示“创建聚光灯”按钮
            if (assignedLight == null)
            {
                if (GUILayout.Button("Create Spotlight"))
                {
                    // 记录撤销操作，并创建一个新的光源物体作为手电筒光源
                    Undo.RecordObject(flashlight, "Create Spotlight");
                    GameObject lightGO = new GameObject("Flashlight_Spotlight");
                    Undo.RegisterCreatedObjectUndo(lightGO, "Create Spotlight");
                    lightGO.transform.SetParent(flashlight.gameObject.transform, false);
                    // 添加 Light 组件并配置为聚光灯
                    Light l = lightGO.AddComponent<Light>();
                    l.type = LightType.Spot;
                    l.spotAngle = 60f;
                    l.range = 10f;
                    l.intensity = 1f;

                    // 把创建的光源绑定到 lightSource 字段并应用修改
                    lightProp.objectReferenceValue = l;
                    serializedObject.ApplyModifiedProperties();

                    // 标记对象为已修改，确保修改被保存
                    EditorUtility.SetDirty(flashlight);
                    EditorUtility.SetDirty(lightGO);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(flashlight);
                }
            }
            serializedObject.ApplyModifiedProperties();
        }
    }
}
#endif

