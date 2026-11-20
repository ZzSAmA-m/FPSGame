using GamePlay.HoldSystem;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(KnifeSO)), CanEditMultipleObjects]
public sealed class KnifeSOEditor : Editor
{
    private SerializedProperty _controllerPrefab;
    private SerializedProperty _lightDamage;
    private SerializedProperty _heavyDamage;
    private SerializedProperty _prepareTicks;
    private SerializedProperty _lightAttackTicks;
    private SerializedProperty _heavyAttackTicks;

    private void OnEnable()
    {
        _controllerPrefab = serializedObject.FindProperty("_controllerPrefab");
        _lightDamage = serializedObject.FindProperty("_lightDamage");
        _heavyDamage = serializedObject.FindProperty("_heavyDamage");
        _prepareTicks = serializedObject.FindProperty("_prepareTicks");
        _lightAttackTicks = serializedObject.FindProperty("_lightAttackTicks");
        _heavyAttackTicks = serializedObject.FindProperty("_heavyAttackTicks");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        GUIContent icon = EditorGUIUtility.IconContent("cs Script Icon");
        var header = new GUIContent("  刀具配置", icon.image, "打开 KnifeSO 脚本");
        if (GUILayout.Button(header, EditorStyles.toolbarButton, GUILayout.Height(30)))
            AssetDatabase.OpenAsset(MonoScript.FromScriptableObject((KnifeSO)target));

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            DrawSection("资源配置", new Color(0.65f, 0.70f, 0.75f));
            EditorGUILayout.PropertyField(_controllerPrefab, new GUIContent("刀具模型预制体"));
        }
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            DrawSection("伤害配置", new Color(0.95f, 0.48f, 0.45f));
            EditorGUILayout.PropertyField(_lightDamage, new GUIContent("轻击伤害"));
            EditorGUILayout.PropertyField(_heavyDamage, new GUIContent("重击伤害"));
        }
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            DrawSection("动作时长", new Color(0.95f, 0.72f, 0.39f));
            EditorGUILayout.PropertyField(_prepareTicks, new GUIContent("拔刀 Tick"));
            EditorGUILayout.PropertyField(_lightAttackTicks, new GUIContent("轻击 Tick"));
            EditorGUILayout.PropertyField(_heavyAttackTicks, new GUIContent("重击 Tick"));
        }
        serializedObject.ApplyModifiedProperties();
    }

    private static void DrawSection(string title, Color color)
    {
        Rect rect = EditorGUILayout.GetControlRect(false, 22f);
        EditorGUI.DrawRect(new Rect(rect.x, rect.y + 2f, 3f, rect.height - 4f), color);
        rect.xMin += 10f;
        EditorGUI.LabelField(rect, title, EditorStyles.boldLabel);
    }
}
