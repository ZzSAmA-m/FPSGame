using GamePlay.Player;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(PlayerMovementDataSO))]
[CanEditMultipleObjects]
public sealed class PlayerMovementDataSOEditor : Editor
{
    private SerializedProperty _moveSpeed;
    private SerializedProperty _jumpSpeed;
    private SerializedProperty _groundAcceleration;
    private SerializedProperty _groundFriction;
    private SerializedProperty _stopSpeed;
    private SerializedProperty _surfaceFriction;

    private GUIStyle _titleStyle;
    private GUIStyle _sectionStyle;
    private GUIStyle _descriptionStyle;

    private static readonly Color MovementColor = new(0.36f, 0.76f, 0.91f);
    private static readonly Color GroundColor = new(0.95f, 0.72f, 0.39f);

    private void OnEnable()
    {
        _moveSpeed = serializedObject.FindProperty("_moveSpeed");
        _jumpSpeed = serializedObject.FindProperty("_jumpSpeed");
        _groundAcceleration = serializedObject.FindProperty("_groundAcceleration");
        _groundFriction = serializedObject.FindProperty("_groundFriction");
        _stopSpeed = serializedObject.FindProperty("_stopSpeed");
        _surfaceFriction = serializedObject.FindProperty("_surfaceFriction");
    }

    private void InitStyles()
    {
        if (_titleStyle != null) return;

        _titleStyle = new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize = 16,
            alignment = TextAnchor.MiddleLeft
        };
        _titleStyle.normal.textColor = new Color(0.48f, 0.86f, 0.88f);
        _sectionStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 12 };
        _descriptionStyle = new GUIStyle(EditorStyles.miniLabel) { wordWrap = true };
    }

    public override void OnInspectorGUI()
    {
        InitStyles();
        serializedObject.Update();

        DrawConfigurationHeader();
        EditorGUILayout.Space(10);
        float previousLabelWidth = EditorGUIUtility.labelWidth;
        try
        {
            float minimumLabelWidth = EditorStyles.label.CalcSize(new GUIContent("低速摩擦控制速度")).x + 12f;
            EditorGUIUtility.labelWidth = Mathf.Max(minimumLabelWidth,
                Mathf.Min(180f, EditorGUIUtility.currentViewWidth * 0.5f));
            DrawMovementSection();
            EditorGUILayout.Space(8);
            DrawGroundSection();
        }
        finally
        {
            EditorGUIUtility.labelWidth = previousLabelWidth;
        }

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawConfigurationHeader()
    {
        Rect rect = EditorGUILayout.GetControlRect(false, 56);
        EditorGUI.DrawRect(rect, new Color(0.12f, 0.23f, 0.26f));
        DrawScriptIcon(new Rect(rect.x + 12, rect.y + 10, 36, 36));

        float textWidth = Mathf.Max(0f, rect.width - 72);
        GUI.Label(new Rect(rect.x + 60, rect.y + 6, textWidth, 25), "玩家移动配置", _titleStyle);
        GUI.Label(new Rect(rect.x + 60, rect.y + 33, textWidth, 18),
            targets.Length > 1 ? $"已选择 {targets.Length} 个移动配置" : "水平移动与起跳参数", EditorStyles.miniLabel);
    }

    private void DrawScriptIcon(Rect rect)
    {
        GUIContent icon = EditorGUIUtility.IconContent("cs Script Icon");
        GUI.Label(rect, new GUIContent(icon.image, $"双击打开 {target.GetType().Name}.cs"));
        EditorGUIUtility.AddCursorRect(rect, MouseCursor.Link);

        Event currentEvent = Event.current;
        if (currentEvent.type != EventType.MouseDown || currentEvent.button != 0 ||
            currentEvent.clickCount != 2 || !rect.Contains(currentEvent.mousePosition))
            return;

        MonoScript script = MonoScript.FromScriptableObject((ScriptableObject)target);
        if (script != null) AssetDatabase.OpenAsset(script);
        currentEvent.Use();
    }

    private void DrawMovementSection()
    {
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            DrawSectionTitle("基础移动", MovementColor);
            GUILayout.Label("满幅输入的移动速度与起跳初速度", _descriptionStyle);
            EditorGUILayout.Space(5);
            EditorGUILayout.PropertyField(_moveSpeed,
                new GUIContent("移动速度", "满幅移动输入对应的期望水平速度，单位：Unity 世界单位/秒。"));
            EditorGUILayout.PropertyField(_jumpSpeed,
                new GUIContent("起跳速度", "起跳时的初始竖直速度，单位：Unity 世界单位/秒。"));
        }
    }

    private void DrawGroundSection()
    {
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            DrawSectionTitle("地面响应", GroundColor);
            GUILayout.Label("接地时的水平加速、减速和表面响应", _descriptionStyle);
            EditorGUILayout.Space(5);
            EditorGUILayout.PropertyField(_groundAcceleration,
                new GUIContent("地面加速系数", "地面加速系数；数值越大，水平速度越快接近期望速度。"));
            EditorGUILayout.PropertyField(_groundFriction,
                new GUIContent("地面摩擦系数", "地面减速系数；数值越大，水平速度衰减越快。"));
            EditorGUILayout.PropertyField(_stopSpeed,
                new GUIContent("低速摩擦控制速度", "低速摩擦计算使用的速度下限，帮助角色停止滑动。单位：Unity 世界单位/秒。"));
            EditorGUILayout.PropertyField(_surfaceFriction,
                new GUIContent("表面摩擦倍率", "同时影响地面加速和地面摩擦；1 为标准表面，无单位。"));
            EditorGUILayout.Space(5);
            EditorGUILayout.HelpBox("速度统一使用 Unity 世界单位/秒；地面响应参数用于接地时的水平移动。", MessageType.Info);
        }
    }

    private void DrawSectionTitle(string title, Color color)
    {
        Rect rect = EditorGUILayout.GetControlRect(false, 25);
        EditorGUI.DrawRect(new Rect(rect.x, rect.y + 3, 3, 19), color);
        _sectionStyle.normal.textColor = color;
        GUI.Label(new Rect(rect.x + 10, rect.y, Mathf.Max(0f, rect.width - 10), rect.height), title, _sectionStyle);
    }
}
