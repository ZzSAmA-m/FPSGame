
using GamePlay.HoldSystem;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(GunSO), true)]
[CanEditMultipleObjects]
public sealed class GunSOEditor : Editor
{
    private SerializedProperty _resourcePack;
    // 弹药配置
    private SerializedProperty _initialTotalBullets;
    private SerializedProperty _capacity;

    // 伤害配置
    private SerializedProperty _bodyDamage;
    private SerializedProperty _legDamage;
    private SerializedProperty _headDamage;
    private SerializedProperty _penetrationDamageReductionRatio;

    // Tick 配置
    private SerializedProperty _prepareTicks;
    private SerializedProperty _shotIntervalTicks;
    private SerializedProperty _reloadTicks;

    // UI 样式
    private GUIStyle _titleStyle;
    private GUIStyle _sectionStyle;
    private GUIStyle _descriptionStyle;

    private static readonly Color AmmoColor =
        new Color(0.36f, 0.76f, 0.91f);

    private static readonly Color DamageColor =
        new Color(0.95f, 0.48f, 0.45f);

    private static readonly Color TickColor =
        new Color(0.95f, 0.72f, 0.39f);

    private void OnEnable()
    {
        _resourcePack = serializedObject.FindProperty("_resourcePack");
        _initialTotalBullets =
            serializedObject.FindProperty("_initialTotalBullets");

        _capacity =
            serializedObject.FindProperty("_capacity");

        _bodyDamage =
            serializedObject.FindProperty("_bodyDamage");

        _legDamage =
            serializedObject.FindProperty("_legDamage");

        _headDamage =
            serializedObject.FindProperty("_headDamage");

        _penetrationDamageReductionRatio =
            serializedObject.FindProperty("_penetrationDamageReductionRatio");

        _prepareTicks =
            serializedObject.FindProperty("_prepareTicks");

        _shotIntervalTicks =
            serializedObject.FindProperty("_shotIntervalTicks");

        _reloadTicks =
            serializedObject.FindProperty("_reloadTicks");
    }

    private void InitStyles()
    {
        if (_titleStyle != null) return;

        _titleStyle = new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize = 16,
            alignment = TextAnchor.MiddleLeft
        };
        _titleStyle.normal.textColor =
            new Color(0.48f, 0.86f, 0.88f);

        _sectionStyle = new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize = 12
        };

        _descriptionStyle = new GUIStyle(EditorStyles.miniLabel)
        {
            wordWrap = true
        };
    }

    public override void OnInspectorGUI()
    {
        InitStyles();

        serializedObject.Update();

        DrawConfigurationHeader();

        EditorGUILayout.Space(10);

        DrawBaseProperties();

        EditorGUILayout.Space(8);

        DrawAmmoSection();

        EditorGUILayout.Space(8);

        DrawDamageSection();

        EditorGUILayout.Space(8);

        DrawTickSection();

        serializedObject.ApplyModifiedProperties();
    }

    /// <summary>绘制带脚本跳转图标的枪械配置标题。</summary>
    private void DrawConfigurationHeader()
    {
        
        Rect rect = EditorGUILayout.GetControlRect(
            false, 56
        );

        EditorGUI.DrawRect(
            rect,
            new Color(0.12f, 0.23f, 0.26f)
        );

        DrawScriptIcon(new Rect(rect.x + 12, rect.y + 10, 36, 36));

        Rect titleRect = new Rect(
            rect.x + 60,
            rect.y + 6,
            rect.width - 72,
            25
        );

        GUI.Label(
            titleRect,
            "GUN CONFIGURATION",
            _titleStyle
        );

        Rect descRect = new Rect(
            rect.x + 60,
            rect.y + 33,
            rect.width - 72,
            18
        );

        GUI.Label(
            descRect,
            $"枪械属性配置 · {target.GetType().Name}",
            EditorStyles.miniLabel
        );
    }

    /// <summary>显示原生资源图标，双击打开当前资源的脚本。</summary>
    private void DrawScriptIcon(Rect rect)
    {
        GUIContent content = EditorGUIUtility.IconContent("cs Script Icon");
        GUI.Label(rect, new GUIContent(content.image, $"双击打开 {target.GetType().Name}.cs"));
        EditorGUIUtility.AddCursorRect(rect, MouseCursor.Link);

        Event currentEvent = Event.current;
        if (currentEvent.type != EventType.MouseDown || currentEvent.button != 0 ||
            currentEvent.clickCount != 2 || !rect.Contains(currentEvent.mousePosition))
            return;

        MonoScript script = MonoScript.FromScriptableObject((ScriptableObject)target);
        if (script != null)
            AssetDatabase.OpenAsset(script);

        currentEvent.Use();
    }

    // 绘制 WeaponSO 继承的属性
    private void DrawBaseProperties()
    {
        using (new EditorGUILayout.VerticalScope(
            EditorStyles.helpBox))
        {
            DrawSectionTitle(
                "资源配置 · RESOURCES",
                new Color(0.65f, 0.7f, 0.75f)
            );

            EditorGUILayout.PropertyField(_resourcePack, new GUIContent("枪械资源包"));

            DrawPropertiesExcluding(
                serializedObject,
                "m_Script",
                "_controllerPrefab",
                "_resourcePack",
                "_initialTotalBullets",
                "_capacity",
                "_bodyDamage",
                "_legDamage",
                "_headDamage",
                "_penetrationDamageReductionRatio",
                "_prepareTicks",
                "_shotIntervalTicks",
                "_reloadTicks"
            );
        }
    }

    // 弹药配置区域
    private void DrawAmmoSection()
    {
        using (new EditorGUILayout.VerticalScope(
            EditorStyles.helpBox))
        {
            DrawSectionTitle(
                "弹药配置 · AMMO",
                AmmoColor
            );

            GUILayout.Label(
                "弹药数量与单个弹匣容量",
                _descriptionStyle
            );

            EditorGUILayout.Space(5);

            EditorGUILayout.PropertyField(
                _initialTotalBullets,
                new GUIContent("初始总弹药量")
            );

            EditorGUILayout.PropertyField(
                _capacity,
                new GUIContent("单个弹匣容量")
            );
        }
    }

    // 伤害配置区域
    private void DrawDamageSection()
    {
        using (new EditorGUILayout.VerticalScope(
            EditorStyles.helpBox))
        {
            DrawSectionTitle(
                "伤害配置 · DAMAGE",
                DamageColor
            );

            GUILayout.Label(
                "命中部位伤害与穿射伤害衰减",
                _descriptionStyle
            );

            EditorGUILayout.Space(5);

            EditorGUILayout.PropertyField(
                _bodyDamage,
                new GUIContent("身体伤害")
            );

            EditorGUILayout.PropertyField(
                _legDamage,
                new GUIContent("腿部伤害")
            );

            EditorGUILayout.PropertyField(
                _headDamage,
                new GUIContent("头部伤害")
            );

            EditorGUILayout.PropertyField(
                _penetrationDamageReductionRatio,
                new GUIContent("穿射伤害减少比例", "0 不减少伤害，1 减少全部伤害。")
            );
        }
    }

    // Tick 配置区域
    private void DrawTickSection()
    {
        using (new EditorGUILayout.VerticalScope(
            EditorStyles.helpBox))
        {
            DrawSectionTitle(
                "Tick 配置 · TIMING",
                TickColor
            );

            GUILayout.Label(
                "游戏逻辑时序参数",
                _descriptionStyle
            );

            EditorGUILayout.Space(5);

            EditorGUILayout.PropertyField(
                _prepareTicks,
                new GUIContent("准备动作 Tick")
            );

            EditorGUILayout.PropertyField(
                _shotIntervalTicks,
                new GUIContent("射击间隔 Tick")
            );

            EditorGUILayout.PropertyField(
                _reloadTicks,
                new GUIContent("换弹动作 Tick")
            );

            EditorGUILayout.Space(5);

            EditorGUILayout.HelpBox(
                "所有时间参数均以 Tick 为单位。",
                MessageType.Info
            );
        }
    }

    // 通用分组标题
    private void DrawSectionTitle(
        string title,
        Color color)
    {
        Rect rect = EditorGUILayout.GetControlRect(
            false, 25
        );

        EditorGUI.DrawRect(
            new Rect(rect.x, rect.y + 3, 3, 19),
            color
        );

        _sectionStyle.normal.textColor = color;

        GUI.Label(
            new Rect(
                rect.x + 10,
                rect.y,
                rect.width - 10,
                rect.height
            ),
            title,
            _sectionStyle
        );
    }
}
