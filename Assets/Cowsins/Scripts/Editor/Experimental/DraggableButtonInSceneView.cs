// =====================================================================
// DraggableButtonInSceneView.cs：在场景视图（Scene View）中显示一个可拖拽的悬浮按钮。
// 按钮可吸附到最近边缘，点击后展开菜单（定位武器/武器SO文件夹、打开武器创建助手、教程链接），
// 并允许外部通过事件注册自定义按钮。仅在编辑器环境（UNITY_EDITOR）下编译。
// =====================================================================
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace cowsins
{
    [InitializeOnLoad]
    /// <summary>
    /// 场景视图可拖拽悬浮按钮：负责绘制按钮、处理拖拽/边缘吸附，并渲染展开的菜单。
    /// </summary>
    public static class DraggableButtonInSceneView
    {
        // 菜单中的按钮数量（外部扩展时可覆盖该值以调整布局）。
        public static int buttonCount = 4;

        // EditorPrefs 中保存“是否显示可拖拽按钮”的键名。
        private const string ShowDraggableButtonKey = "Cowsins_ShowDraggableButton";
        // 是否在场景视图中显示该按钮。
        public static bool showDraggableButton;

        // 按钮在场景视图中的位置（初始为 (10, 10)）。
        private static Vector2 buttonPosition = new Vector2(10, 10);
        // 是否正在拖拽按钮。
        private static bool isDragging = false;
        // 按钮的 GUI 样式。
        private static GUIStyle buttonStyle;
        // 按钮中央显示的 Logo 图标。
        private static Texture2D logoIcon;
        // 按钮的尺寸。
        private static Vector2 buttonSize = new Vector2(40, 35);
        // Logo 图标的尺寸。
        private static Vector2 imageSize = new Vector2(21, 21);
        // 是否展开菜单。
        private static bool showMenu = false;

        // 菜单中四个内置选项对应的图标。
        private static Texture2D option1Image, option2Image, option3Image, option4Image;

        // 委托类型：允许外部在菜单中追加自定义按钮。
        public delegate void AddButtonDelegate(Rect menuRect);
        // 外部注册自定义按钮的事件（在菜单绘制时触发）。
        public static event AddButtonDelegate OnAddExternalButtons;

        // 静态构造函数：读取显示设置、订阅场景视图绘制事件，并加载按钮与菜单图标。
        static DraggableButtonInSceneView()
        {
            showDraggableButton = EditorPrefs.GetBool(ShowDraggableButtonKey, true);
            SceneView.duringSceneGui += OnSceneGUI;

            logoIcon = Resources.Load<Texture2D>("CustomEditor/LogoIcon");

            option1Image = Resources.Load<Texture2D>("CustomEditor/FolderWeapon");
            option2Image = Resources.Load<Texture2D>("CustomEditor/FolderSO");
            option3Image = Resources.Load<Texture2D>("CustomEditor/AddWeapon");
            option4Image = Resources.Load<Texture2D>("CustomEditor/Tutorials");
        }

        // 场景视图绘制回调：绘制按钮、处理拖拽与菜单展开逻辑。
        private static void OnSceneGUI(SceneView sceneView)
        {
            if (!showDraggableButton)
                return;

            if (buttonStyle == null)
                buttonStyle = new GUIStyle(GUI.skin.button);

            Handles.BeginGUI();

            // 计算按钮在场景视图中的绘制区域。
            Rect buttonRect = new Rect(buttonPosition.x, buttonPosition.y, buttonSize.x, buttonSize.y);

            HandleDragging(buttonRect, sceneView);

            // 左键点击按钮时切换菜单的展开/收起状态。
            if (Event.current.type == EventType.MouseDown && Event.current.button == 0 && buttonRect.Contains(Event.current.mousePosition))
            {
                showMenu = !showMenu;
                Event.current.Use();
                sceneView.Repaint();
            }

            GUI.Button(buttonRect, GUIContent.none, buttonStyle);
            // 计算 Logo 图标居中的绘制区域。
            Rect imageRect = new Rect(
                buttonRect.x + (buttonSize.x - imageSize.x) / 2,
                buttonRect.y + (buttonSize.y - imageSize.y) / 2,
                imageSize.x,
                imageSize.y
            );
            if (logoIcon) GUI.DrawTexture(imageRect, logoIcon, ScaleMode.ScaleToFit);

            // Draw the menu
            if (showMenu && !isDragging)
            {
                // 根据按钮位于场景视图左半边还是右半边，决定菜单向左或向右展开。
                Rect menuRect = new Rect(
                    buttonRect.x + (buttonPosition.x < sceneView.position.width / 2 ? buttonSize.x : -40 * buttonCount),
                    buttonRect.y,
                    100,
                    buttonSize.y
                );
                DrawMenu(menuRect);
            }

            Handles.EndGUI();
        }

        // 绘制展开的菜单：四个内置选项按钮，并触发外部注册按钮事件。
        private static void DrawMenu(Rect menuRect)
        {
            GUI.Box(menuRect, GUIContent.none, GUI.skin.box);

            GUIContent option1Content = new GUIContent(option1Image);
            GUIContent option2Content = new GUIContent(option2Image);
            GUIContent option3Content = new GUIContent(option3Image);
            GUIContent option4Content = new GUIContent(option4Image);

            float itemWidth = menuRect.width / 3;
            float itemHeight = menuRect.height;

            if (GUI.Button(new Rect(GetButtonPosition(menuRect, 0), menuRect.y, itemWidth, itemHeight), option1Content))
            {
                PingFolder("Assets/Cowsins/Prefabs/Weapons");
            }

            if (GUI.Button(new Rect(GetButtonPosition(menuRect, 1), menuRect.y, itemWidth, itemHeight), option2Content))
            {
                PingFolder("Assets/Cowsins/ScriptableObjects/Weapons");
            }

            if (GUI.Button(new Rect(GetButtonPosition(menuRect, 2), menuRect.y, itemWidth, itemHeight), option3Content))
            {
                CustomTabEditorWindow.OpenWeaponsTab();
            }

            if (GUI.Button(new Rect(GetButtonPosition(menuRect, 3), menuRect.y, itemWidth, itemHeight), option4Content))
            {
                Application.OpenURL("https://www.cowsins.com/");
            }

            OnAddExternalButtons?.Invoke(menuRect);
        }

        // 在 Project 窗口中定位并高亮指定文件夹，找不到时输出错误。
        private static void PingFolder(string folderPath)
        {
            Object folder = AssetDatabase.LoadAssetAtPath<Object>(folderPath);
            if (folder != null)
            {
                EditorGUIUtility.PingObject(folder);
                Selection.activeObject = folder;
            }
            else
            {
                Debug.LogError($"Folder is empty or not found: {folderPath}. Did you remove or rename the folder?");
            }
        }

        // 处理拖拽：右键按下开始、拖动更新位置并限制范围、右键松开结束并吸附到最近边缘。
        private static void HandleDragging(Rect buttonRect, SceneView sceneView)
        {
            Event e = Event.current;

            if (e.type == EventType.MouseDown && e.button == 1 && buttonRect.Contains(e.mousePosition))
            {
                isDragging = true;
                showMenu = false;
                e.Use();
            }

            if (isDragging && e.type == EventType.MouseDrag)
            {
                buttonPosition += e.delta;
                ClampButtonPosition(sceneView);
                e.Use();
                sceneView.Repaint();
            }

            if (e.type == EventType.MouseUp && e.button == 1)
            {
                isDragging = false;
                SnapToNearestEdge(sceneView);
                sceneView.Repaint();
            }
        }

        // 将按钮位置限制在场景视图可见范围内（四周各保留 10 像素边距）。
        private static void ClampButtonPosition(SceneView sceneView)
        {
            Vector2 viewSize = new Vector2(sceneView.position.width, sceneView.position.height);
            buttonPosition.x = Mathf.Clamp(buttonPosition.x, 10, viewSize.x - buttonSize.x - 10);
            buttonPosition.y = Mathf.Clamp(buttonPosition.y, 10, viewSize.y - buttonSize.y - 10);
        }

        // 松开拖拽后将按钮吸附到距离最近的边缘。
        private static void SnapToNearestEdge(SceneView sceneView)
        {
            Vector2 viewSize = new Vector2(sceneView.position.width, sceneView.position.height);

            float left = buttonPosition.x - 10;
            float right = viewSize.x - (buttonPosition.x + buttonSize.x + 10);
            float top = buttonPosition.y - 10;
            float bottom = viewSize.y - (buttonPosition.y + buttonSize.y + 10);

            float min = Mathf.Min(left, right, top, bottom);

            if (min == left) buttonPosition.x = 10;
            else if (min == right) buttonPosition.x = viewSize.x - buttonSize.x - 10;
            else if (min == top) buttonPosition.y = 10;
            else if (min == bottom) buttonPosition.y = viewSize.y - buttonSize.y - 10;
        }

        /// <summary>
        /// 计算菜单中第 index 个按钮的 X 坐标，供外部添加自定义按钮时定位使用。
        /// </summary>
        public static float GetButtonPosition(Rect menuRect, int index)
        {
            float spacing = 5;
            float itemWidth = menuRect.width / 3;
            float offset = buttonPosition.x < menuRect.width ? 5 : -4;

            return menuRect.x + spacing * (index + 1) + itemWidth * index + offset;
        }

        /// <summary>
        /// 设置是否在场景视图中显示按钮，并将设置持久化到 EditorPrefs。
        /// </summary>
        public static void SetShowDraggableButton(bool value)
        {
            showDraggableButton = value;
            EditorPrefs.SetBool(ShowDraggableButtonKey, value);
        }
    }
}
#endif
