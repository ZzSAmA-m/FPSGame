// ======================================================================
// InteractManagerEditor.cs —— InteractManager 的自定义 Inspector
// 仅在 Unity 编辑器下编译，将 InteractManager 的众多序列化字段按 "References /
// Interaction / Dropping / Inspect / Events" 五个页签分组展示，便于在 Inspector 中配置。
// ======================================================================
#if UNITY_EDITOR
/// <summary>
/// This script belongs to cowsins™ as a part of the cowsins´ FPS Engine. All rights reserved. 
/// </summary>
using UnityEngine;
using UnityEditor;
namespace cowsins
{
    /// <summary>InteractManager 的自定义 Inspector：使用页签分组展示引用、交互、丢弃、检视与事件等可配置字段。</summary>
    [System.Serializable]
    [CustomEditor(typeof(InteractManager))]
    public class InteractManagerEditor : Editor
    {
        // 顶部页签名称数组，对应各配置分组。
        private string[] tabs = { "References", "Interaction", "Dropping", "Inspect", "Events" };
        // 当前选中的页签索引。
        private int currentTab = 0;

        /// <summary>绘制自定义 Inspector：顶部显示贴图与页签工具栏，并根据当前页签展示对应的序列化字段，最后应用修改。</summary>
        override public void OnInspectorGUI()
        {
            serializedObject.Update();
            InteractManager myScript = target as InteractManager;

            Texture2D myTexture = Resources.Load<Texture2D>("CustomEditor/interactManager_CustomEditor") as Texture2D;
            GUILayout.Label(myTexture);

            EditorGUILayout.BeginVertical();
            currentTab = GUILayout.Toolbar(currentTab, tabs);
            EditorGUILayout.Space(10f);
            EditorGUILayout.EndVertical();
            #region variables

            if (currentTab >= 0 || currentTab < tabs.Length)
            {
                switch (tabs[currentTab])
                {
                    case "References":
                        EditorGUILayout.LabelField("REFERENCES", EditorStyles.boldLabel);
                        EditorGUILayout.PropertyField(serializedObject.FindProperty("mask"));
                        EditorGUILayout.PropertyField(serializedObject.FindProperty("weaponGenericPickeable"));
                        EditorGUILayout.PropertyField(serializedObject.FindProperty("attachmentGenericPickeable"));
                        break;
                    case "Interaction":
                        EditorGUILayout.LabelField("INTERACTION", EditorStyles.boldLabel);
                        EditorGUILayout.PropertyField(serializedObject.FindProperty("detectInteractionDistance"));
                        EditorGUILayout.PropertyField(serializedObject.FindProperty("progressRequiredToInteract"));
                        EditorGUILayout.PropertyField(serializedObject.FindProperty("interactInterval"));
                        if (myScript.DuplicateWeaponAddsBullets)
                        {
                            EditorGUILayout.Space(10);
                            EditorGUILayout.LabelField("This feature is only applicable to weapons with limited magazines.", EditorStyles.helpBox);
                            EditorGUILayout.Space(5);
                        }
                        EditorGUILayout.PropertyField(serializedObject.FindProperty("duplicateWeaponAddsBullets"));
                        break;
                    case "Inspect":
                        EditorGUILayout.LabelField("INSPECTION & REALTIME CUSTOMIZATION", EditorStyles.boldLabel);
                        EditorGUILayout.PropertyField(serializedObject.FindProperty("canInspect"));
                        if (myScript.CanInspect)
                        {
                            EditorGUI.indentLevel++;
                            EditorGUILayout.PropertyField(serializedObject.FindProperty("realtimeAttachmentCustomization"));
                            if (myScript.RealtimeAttachmentCustomization)
                            {
                                EditorGUI.indentLevel++;
                                EditorGUILayout.PropertyField(serializedObject.FindProperty("dropAttachmentOnDettachUI"));
                                EditorGUILayout.PropertyField(serializedObject.FindProperty("displayCurrentAttachmentsOnly"));
                                EditorGUI.indentLevel--;
                            }
                            EditorGUI.indentLevel--;
                        }
                        break;
                    case "Dropping":
                        EditorGUILayout.LabelField("DROP WEAPONS", EditorStyles.boldLabel);
                        EditorGUILayout.PropertyField(serializedObject.FindProperty("canDrop"));
                        if (myScript.CanDrop)
                        {
                            EditorGUI.indentLevel++;
                            EditorGUILayout.PropertyField(serializedObject.FindProperty("droppingDistance"));
                            EditorGUILayout.PropertyField(serializedObject.FindProperty("randomDropOffset"));
                            EditorGUI.indentLevel--;
                        }
                    break;
                    case "Events":
                        EditorGUILayout.LabelField("OTHERS", EditorStyles.boldLabel);
                        EditorGUILayout.PropertyField(serializedObject.FindProperty("userEvents"));
                        break;

                }
            }

            #endregion
            EditorGUILayout.Space(10f);
            serializedObject.ApplyModifiedProperties();

        }
    }
}
#endif