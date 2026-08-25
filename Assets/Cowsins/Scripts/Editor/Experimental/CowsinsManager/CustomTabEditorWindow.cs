// =====================================================================
// CustomTabEditorWindow.cs：Cowsins Manager 编辑器窗口的主窗口类。
// 通过反射自动发现并加载所有实现 ITab 接口的标签页，绘制顶部标签栏，
// 并渲染当前选中标签页的内容；同时提供启动时自动弹窗与菜单入口。
// 仅在编辑器环境（UNITY_EDITOR）下编译。
// =====================================================================
#if UNITY_EDITOR

using UnityEditor;
using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;

namespace cowsins
{
    [InitializeOnLoad]
    /// <summary>
    /// Cowsins Manager 主编辑器窗口：管理标签页集合并渲染当前选中的标签页。
    /// </summary>
    public class CustomTabEditorWindow : EditorWindow
    {
        // 通过反射加载的所有标签页实例集合。
        private List<ITab> tabs;
        // 当前选中的标签页索引。
        private int selectedTabIndex;
        // 内容区域的滚动位置。
        private Vector2 scrollPosition;
        // 窗口顶部深色背景颜色。
        Color darkColor = new Color(.15f, .15f, .15f);

        // 静态构造函数：在编辑器加载后延迟调用 ShowWindowOnce，实现启动时自动弹窗。
        static CustomTabEditorWindow()
        {
            // 延迟到编辑器空闲时执行，避免在加载过程中过早弹窗。
            EditorApplication.delayCall += ShowWindowOnce;
        }
        // 每个会话首次初始化时，根据 EditorPrefs 的“启动时显示”设置决定是否自动打开窗口。
        private static void ShowWindowOnce()
        {
            if (!SessionState.GetBool("First_Init", false))
            {
                if (!EditorPrefs.GetBool("FirstTimeShowAgain")) return;
                OpenWindow();
                SessionState.SetBool("First_Init", true);
            }
        }

        [MenuItem("Cowsins/Cowsins Manager [Home] %q")]
        /// <summary>
        /// 打开 Cowsins Manager 窗口（默认显示首页），并初始化全部标签页。
        /// </summary>
        public static void OpenWindow()
        {
            var window = GetWindow<CustomTabEditorWindow>();
            window.titleContent = new GUIContent("Cowsins Manager");
            window.minSize = new Vector2(600, 450);
            window.maxSize = new Vector2(1000, 600);
            window.InitializeTabs();
            window.Show();
        }

        [MenuItem("Cowsins/Cowsins Manager [Weapons]")]
        /// <summary>
        /// 打开 Cowsins Manager 窗口，并定位到“Weapons”标签页。
        /// </summary>
        public static void OpenWeaponsTab()
        {
            var window = GetWindow<CustomTabEditorWindow>();
            window.titleContent = new GUIContent("Cowsins Manager");
            window.minSize = new Vector2(600, 450);
            window.maxSize = new Vector2(1000, 600);
            window.InitializeTabs();

            // Find the "Weapons" tab and set it as the selected tab
            var weaponsTabIndex = window.tabs.FindIndex(tab => tab.TabName == "Weapons");
            if (weaponsTabIndex >= 0)
            {
                window.selectedTabIndex = weaponsTabIndex;
            }

            window.Show();
        }
        /// <summary>
        /// 关闭 Cowsins Manager 窗口。
        /// </summary>
        public static void CloseWindow()
        {
            var window = GetWindow<CustomTabEditorWindow>();
            window.Close();
        }
        // 通过反射加载程序集中所有实现 ITab 接口的类并实例化，填充 tabs 列表。
        private void InitializeTabs()
        {
            tabs = new List<ITab>();

            // Load all tabs that implement ITab
            var tabTypes = typeof(ITab).Assembly.GetTypes()
                .Where(t => typeof(ITab).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract);

            foreach (var type in tabTypes)
            {
                ITab tabInstance = (ITab)Activator.CreateInstance(type);
                tabs.Add(tabInstance);
            }
        }

        // 绘制窗口内容：无标签页时显示刷新提示，否则绘制顶部标签栏与选中标签页的内容。
        private void OnGUI()
        {
            if (tabs == null || tabs.Count == 0)
            {
                GUILayout.FlexibleSpace(); // Push content down to center vertically

                GUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace(); // Center content horizontally

                GUILayout.BeginVertical();
                GUILayout.Label(AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Cowsins/UI/Logo/FPS_Engine_Logo_White.png"), GUILayout.Width(200), GUILayout.Height(30));
                GUILayout.Label("Please refresh the Cowsins Manager", GUILayout.Width(250));

                if (GUILayout.Button("Refresh", GUILayout.Width(100)))
                {
                    InitializeTabs();
                }

                GUILayout.EndVertical();

                GUILayout.FlexibleSpace(); // Center content horizontally
                GUILayout.EndHorizontal();

                GUILayout.FlexibleSpace(); // Push content up to center vertically
                return;
            }


            GUIStyle darkBackgroundStyle = new GUIStyle(GUI.skin.box)
            {
                normal = { background = CreateColoredTexture(darkColor) }
            };

            GUILayout.BeginVertical(darkBackgroundStyle);

            GUILayout.Label(AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Cowsins/UI/Logo/FPS_Engine_Logo_White.png"), GUILayout.Width(200), GUILayout.Height(30));

            GUILayout.BeginHorizontal();

            for (int i = 0; i < tabs.Count; i++)
            {
                if (GUILayout.Toggle(selectedTabIndex == i, tabs[i].TabName, "Button", GUILayout.ExpandWidth(true)))
                {
                    if (selectedTabIndex != i) // Only call Start if the tab changes
                    {
                        selectedTabIndex = i;
                        tabs[selectedTabIndex].StartTab(); // Call Start method of the new tab
                    }
                }
            }

            GUILayout.EndHorizontal();

            GUILayout.Space(5);

            GUILayout.EndVertical();

            GUILayout.Space(5);

            scrollPosition = GUILayout.BeginScrollView(scrollPosition);
            tabs[selectedTabIndex].OnGUI();
            GUILayout.EndScrollView();
        }
        // 创建 1x1 纯色纹理并填充指定颜色，用于绘制自定义背景色。
        Texture2D CreateColoredTexture(Color color)
        {
            Texture2D texture = new Texture2D(1, 1);
            texture.SetPixel(0, 0, color);
            texture.Apply();
            return texture;
        }
    }
}
#endif