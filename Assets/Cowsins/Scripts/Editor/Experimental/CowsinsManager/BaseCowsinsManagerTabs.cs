// =====================================================================
// BaseCowsinsManagerTabs.cs：Cowsins Manager 编辑器窗口的“首页”标签页实现。
// 提供左侧导航栏（Home / Preferences / More by Cowsins）与各子页面的绘制逻辑，
// 包括欢迎标题、教程卡片、官方文档/教程/支持链接卡片及官方包推广卡片等 UI。
// 仅在编辑器环境（UNITY_EDITOR）下编译。
// =====================================================================
#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

namespace cowsins
{
    /// <summary>
    /// Cowsins Manager 的首页标签页：实现 ITab 接口，绘制导航栏与首页各子页面。
    /// </summary>
    public class HomeTab : ITab
    {
        // 标签页名称，显示在管理器顶部的标签栏中。
        public string TabName => "Home";

        // 首页教程卡片区域的滚动位置。
        private Vector2 scrollPosition = Vector2.zero;
        // 当前选中的子页面索引（0=Home, 1=Preferences, 2=More by Cowsins）。
        private int activeTab = 0;

        /// <summary>
        /// 标签页被切换选中时调用（当前实现为空，无需额外初始化）。
        /// </summary>
        public void StartTab() { }

        /// <summary>
        /// 绘制首页标签页：左侧为导航栏，右侧为当前选中子页面的内容。
        /// </summary>
        public void OnGUI()
        {
            GUILayout.BeginHorizontal();

            NavBar();

            GUILayout.Space(25);

            // 根据选中的子页面索引绘制对应的内容区域。
            switch (activeTab)
            {
                case 0: Home(); break;
                case 1: Preferences(); break;
                case 2: MoreByCowsins(); break;
                default: Home(); break;
            }

            GUILayout.EndHorizontal();
        }

        // 绘制左侧导航栏：三个导航按钮、可点击的推广图片（跳转 Asset Store）等。
        private void NavBar()
        {
            GUILayout.BeginVertical(GUI.skin.GetStyle("HelpBox"), GUILayout.Width(Screen.width * 0.25f));

            if (GUILayout.Button("Home", GUILayout.Height(25))) activeTab = 0;
            if (GUILayout.Button("Preferences", GUILayout.Height(25))) activeTab = 1;
            if (GUILayout.Button("More by Cowsins", GUILayout.Height(25))) activeTab = 2;

            // EditorGUILayout.HelpBox($"\nCowsins is facing issues with the YouTube channel, so some tutorials are temporarily available on cowsins.com.\n", MessageType.Warning);

            // 绘制可点击的推广图片，点击后打开 Asset Store 上的 FPS Engine 页面。
            GUILayout.Label(Resources.Load<Texture2D>("CustomEditor/CowsinsManager/reviewCowsinsManager"), GUILayout.Width(240), GUILayout.Height(330));
            Rect rect = GUILayoutUtility.GetLastRect();
            if (rect.Contains(Event.current.mousePosition))
            {
                if (Event.current.type == EventType.MouseUp && Event.current.button == 0)
                {
                    Application.OpenURL("https://assetstore.unity.com/packages/templates/systems/fps-engine-218594");
                }
            }

            GUILayout.EndVertical();
        }

        // 绘制首页子页面：欢迎标题、页头图片、教程卡片滚动区以及文档/教程/支持链接卡片。
        private void Home()
        {
            GUILayout.BeginVertical();

            GUILayout.Label("Welcome to FPS Engine by Cowsins!", CowsinsEditorWindowUtilities.BigHeadingStyle(), GUILayout.Height(30));

            GUILayout.Label(Resources.Load<Texture2D>("CustomEditor/CowsinsManager/CowsinsManagerHeader"), GUILayout.Width(EditorGUIUtility.currentViewWidth * 0.705f), GUILayout.Height(110));

            GUILayout.Space(5);

            scrollPosition = GUILayout.BeginScrollView(scrollPosition, GUILayout.Height(200));

            GUILayout.BeginHorizontal(GUI.skin.GetStyle("HelpBox"));

            CowsinsEditorWindowUtilities.DrawTutorialCard(Resources.Load<Texture2D>("CustomEditor/weapon-tutorial"), "https://www.cowsins.com/videos/OykOx8CgEMc", .7f);
            CowsinsEditorWindowUtilities.DrawTutorialCard(Resources.Load<Texture2D>("CustomEditor/BuiltIn"), "https://www.cowsins.com/videos/aXW34tlzNGE", .7f);
            CowsinsEditorWindowUtilities.DrawTutorialCard(Resources.Load<Texture2D>("CustomEditor/URP"), "https://www.cowsins.com/videos/PyAywKlqKrY", .7f);
            CowsinsEditorWindowUtilities.DrawTutorialCard(Resources.Load<Texture2D>("CustomEditor/hdrp"), "https://www.cowsins.com/videos/kyJFwmcs-6Q", .7f);

            GUILayout.EndHorizontal();
            GUILayout.EndScrollView();

            GUILayout.Space(10);
            GUILayout.BeginHorizontal();

            GUI.backgroundColor = new Color(.5f, .5f, .5f, 1);

            CowsinsEditorWindowUtilities.DrawLinkCard(Resources.Load<Texture2D>("CustomEditor/CowsinsManager/documentationIcon"), "Documentation", "https://cowsinss-organization.gitbook.io/fps-engine-documentation", .77f, .4f);
            GUILayout.FlexibleSpace();
            CowsinsEditorWindowUtilities.DrawLinkCard(Resources.Load<Texture2D>("CustomEditor/CowsinsManager/tutorialsIcon"), "Tutorials", "https://www.cowsins.com/", .77f, .4f);
            GUILayout.FlexibleSpace();
            CowsinsEditorWindowUtilities.DrawLinkCard(Resources.Load<Texture2D>("CustomEditor/CowsinsManager/supportIcon"), "Support", "https://discord.gg/759gSeTT9m", .77f, .4f);
            GUILayout.Space(10);

            GUILayout.EndHorizontal();

            GUILayout.EndVertical();
        }

        // 绘制偏好设置子页面：场景视图可拖拽按钮开关、Cowsins Manager 启动时显示开关。
        private void Preferences()
        {
            GUILayout.BeginVertical();
            GUILayout.Label("Preferences", CowsinsEditorWindowUtilities.BigHeadingStyle(), GUILayout.Height(30));

            GUILayout.Space(10);
            GUILayout.BeginVertical(GUI.skin.GetStyle("HelpBox"));
            GUILayout.Label("Scene View", GUILayout.Height(20));
            GUILayout.Space(2);
            bool newToggleValue = GUILayout.Toggle(DraggableButtonInSceneView.showDraggableButton, "Show Draggable Button in Scene View");
            if (newToggleValue != DraggableButtonInSceneView.showDraggableButton)
            {
                DraggableButtonInSceneView.SetShowDraggableButton(newToggleValue);
            }
            GUILayout.Space(5);
            GUILayout.EndVertical();

            GUILayout.Space(10);
            GUILayout.BeginVertical(GUI.skin.GetStyle("HelpBox"));
            GUILayout.Label("Cowsins Manager", GUILayout.Height(20));
            bool dontShowAgain = EditorPrefs.GetBool("FirstTimeShowAgain", false);
            dontShowAgain = GUILayout.Toggle(dontShowAgain, "Show On Start-Up");
            EditorPrefs.SetBool("FirstTimeShowAgain", dontShowAgain);
            GUILayout.Space(5);
            GUILayout.EndVertical();

            GUILayout.EndVertical();
        }

        // 绘制“更多作品”子页面：展示多个官方包的推广卡片。
        private void MoreByCowsins()
        {
            GUILayout.BeginVertical();
            GUILayout.Label("More by Cowsins", CowsinsEditorWindowUtilities.BigHeadingStyle(), GUILayout.Height(30));

            PackageCard(Resources.Load<Texture2D>("CustomEditor/CowsinsManager/InventorySaveLoadAddOnThumbnail"), "Inventory Pro + Save & Load", 
                "The definitive solution to add Advanced systems like Inventories, Chests, Shops, Crafting, Save & Load & much more in FPS Engine!",
                "https://assetstore.unity.com/packages/templates/systems/inventory-pro-add-on-for-fps-engine-318131", "FPS Engine Official Add-On","", Color.green, Color.grey);
            PackageCard(Resources.Load<Texture2D>("CustomEditor/CowsinsManager/SaveLoadAddOnThumbnail"), "Save & Load",
                "A powerful all-in-one Save & Load solution built specifically for FPS Engine! Effortlessly scalable and customizable, it easily allows you to save and load your game data!",
                "https://assetstore.unity.com/packages/templates/systems/save-load-add-on-for-fps-engine-316848", "FPS Engine Official Add-On", "", Color.green, Color.grey);
            PackageCard(Resources.Load<Texture2D>("CustomEditor/CowsinsManager/PlatformerEngineThumbnail"), "Platformer Engine",
                "An advanced and powerful solution for 2D and 2.5D Platformer games, adaptable,easily scalable and suitable for any kind of Platformer game, supporting a wide variety of settings and features.",
                "https://assetstore.unity.com/packages/templates/systems/platformer-engine-2d-2-5d-266973", "Cowsins Official Package", string.Empty, Color.yellow, Color.grey);
            PackageCard(Resources.Load<Texture2D>("CustomEditor/CowsinsManager/BulletHellEngineThumbnail"), "Bullet Hell Engine",
                "A powerful solution for Bullet Hell games. Highly customizable and easy to work with, supporting a wide variety of settings and features.",
                "https://assetstore.unity.com/packages/tools/game-toolkits/bullet-hell-engine-249720", "Cowsins Official Package", string.Empty, Color.yellow, Color.grey);

            GUILayout.EndVertical();
        }

        // 绘制单个包的推广卡片：缩略图、标签徽章、标题、描述以及“查看包 / 即将推出”按钮。
        private void PackageCard(Texture2D cardImage,string title,string description, string url, string label1, string label2, Color label1Color, Color label2Color)
        {
            GUI.backgroundColor = Color.white;
            GUILayout.BeginHorizontal(GUI.skin.GetStyle("HelpBox"));
            GUILayout.Space(5);
            CowsinsEditorWindowUtilities.DrawTutorialCard(cardImage, url, .7f);
            GUILayout.Space(10);
            GUILayout.BeginVertical();
            GUILayout.Space(5);
            GUILayout.BeginHorizontal();
            if(!string.IsNullOrEmpty(label1))
                Label(label1, 120, label1Color);
            GUILayout.Space(5);
            if (!string.IsNullOrEmpty(label2))
                Label(label2, 80, label2Color);
            GUILayout.EndHorizontal();
            GUILayout.Label(title, CowsinsEditorWindowUtilities.BigHeadingStyle(), GUILayout.Height(30));

            GUIStyle style = new GUIStyle(GUI.skin.label);
            style.wordWrap = true; 
            style.fontSize = 12; 
            style.normal.textColor = Color.white; 
            GUILayout.Label(description, style, GUILayout.Width(300), GUILayout.Height(60));

            if(string.IsNullOrEmpty(url))
            {
                GUI.backgroundColor = new Color(0, 0, 0, 0.5f);
                if (GUILayout.Button("Coming Soon", GUILayout.Height(25))) { }
            }
            else
            {
                GUI.backgroundColor = Color.white;
                if (GUILayout.Button("View Package", GUILayout.Height(25))) Application.OpenURL(url);
            }
            GUILayout.EndVertical();

            GUILayout.EndHorizontal();
        }

        // 绘制一个带背景色的标签徽章（用于包卡片上的“官方 Add-On”等标记）。
        private void Label(string label, int width, Color labelColor)
        {
            GUI.color = labelColor;
            GUILayout.BeginVertical(GUI.skin.GetStyle("HelpBox"), GUILayout.Width(width));
            GUILayout.Label(label, GUILayout.Height(17));
            GUILayout.EndVertical();
            GUI.color = Color.white;
        }
    }
}
#endif
