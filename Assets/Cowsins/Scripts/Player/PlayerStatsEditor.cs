// ============================================================================
// PlayerStatsEditor.cs —— PlayerStats 的自定义 Inspector 编辑器。
// 仅在编辑器环境下编译（#if UNITY_EDITOR）。将 PlayerStats 的序列化字段按
// 标签页（玩家属性 / 坠落伤害 / 自动回复 / 事件）分组展示，并根据开关状态
// 动态展开或收起相关子字段。
// ============================================================================
#if UNITY_EDITOR
/// <summary>
/// This script belongs to cowsins™ as a part of the cowsins´ FPS Engine. All rights reserved. 
/// </summary>
using UnityEngine;
using UnityEditor;
namespace cowsins
{
    /// <summary>
    /// PlayerStats 的自定义 Inspector：以标签页形式组织并绘制属性编辑界面。
    /// </summary>
    [System.Serializable]
    [CustomEditor(typeof(PlayerStats))]
    public class PlayerStatsEditor : Editor
    {
        private string[] tabs = { "Player Stats", "Fall Damage", "Auto Heal", "Events" }; // 标签页名称数组
        private int currentTab = 0; // 当前选中的标签页索引

        // 绘制 Inspector：先显示头部贴图与标签页工具栏，再根据当前标签页绘制对应字段。
        override public void OnInspectorGUI()
        {
            serializedObject.Update();
            PlayerStats myScript = target as PlayerStats; // 获取被编辑的 PlayerStats 实例

            Texture2D myTexture = Resources.Load<Texture2D>("CustomEditor/playerState_CustomEditor") as Texture2D; // 加载头部贴图
            GUILayout.Label(myTexture); // 显示贴图

            EditorGUILayout.BeginVertical();
            currentTab = GUILayout.Toolbar(currentTab, tabs); // 绘制标签页工具栏
            EditorGUILayout.Space(10f);
            EditorGUILayout.EndVertical();
            #region variables

            if (currentTab >= 0 || currentTab < tabs.Length) // 判断标签索引是否有效
            {
                switch (tabs[currentTab]) // 根据当前标签页名绘制对应内容
                {
                    case "Player Stats": // 玩家属性页
                        EditorGUILayout.LabelField("PLAYER STATES", EditorStyles.boldLabel);
                        EditorGUILayout.PropertyField(serializedObject.FindProperty("health"));
                        EditorGUILayout.PropertyField(serializedObject.FindProperty("shield"));
                        EditorGUILayout.PropertyField(serializedObject.FindProperty("maxHealth"));
                        EditorGUILayout.PropertyField(serializedObject.FindProperty("maxShield"));
                        EditorGUILayout.PropertyField(serializedObject.FindProperty("freezePlayerOnDeath"));
                        break;
                    case "Fall Damage": // 坠落伤害页
                        EditorGUILayout.LabelField("FALL DAMAGE", EditorStyles.boldLabel);
                        EditorGUILayout.PropertyField(serializedObject.FindProperty("takesFallDamage"));
                        if (myScript.TakesFallDamage) // 启用坠落伤害时才显示相关字段
                        {
                            EditorGUI.indentLevel++;
                            EditorGUILayout.PropertyField(serializedObject.FindProperty("minimumHeightDifferenceToApplyDamage"));
                            EditorGUILayout.PropertyField(serializedObject.FindProperty("fallDamageMultiplier"));
                            EditorGUI.indentLevel--;
                        }
                        break;
                    case "Auto Heal": // 自动回复页
                        EditorGUILayout.LabelField("AUTO HEAL", EditorStyles.boldLabel);
                        EditorGUILayout.PropertyField(serializedObject.FindProperty("enableAutoHeal"));
                        if (myScript.EnableAutoHeal) // 启用自动回复时才显示相关字段
                        {
                            EditorGUI.indentLevel++;
                            EditorGUILayout.PropertyField(serializedObject.FindProperty("healRate"));
                            EditorGUILayout.PropertyField(serializedObject.FindProperty("healAmount"));
                            EditorGUILayout.PropertyField(serializedObject.FindProperty("restartAutoHealAfterBeingDamaged"));
                            if (myScript.RestartAutoHealAfterBeingDamaged) // 启用受伤后重置时显示延迟时间
                            {
                                EditorGUI.indentLevel++;
                                EditorGUILayout.PropertyField(serializedObject.FindProperty("restartAutoHealTime"));
                                EditorGUI.indentLevel--;
                            }
                            EditorGUI.indentLevel--;
                        }
                        break;
                    case "Events": // 事件页
                        EditorGUILayout.PropertyField(serializedObject.FindProperty("userEvents"));
                        break;

                }
            }

            #endregion

            serializedObject.ApplyModifiedProperties(); // 应用序列化对象的修改

        }
    }

}
#endif
