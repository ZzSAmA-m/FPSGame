// =====================================================================
// WeaponsCowsinsManagerTab.cs：Cowsins Manager 的“武器”标签页实现。
// 提供武器使用入门教程、Discord 支持入口、武器创建助手（WeaponCreatorAssistant），
// 以及项目中所有 Weapon_SO 的搜索与网格展示（悬停高亮、点击编辑）。
// 仅在编辑器环境（UNITY_EDITOR）下编译。
// =====================================================================
#if UNITY_EDITOR

using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

namespace cowsins
{
    /// <summary>
    /// 武器标签页：实现 ITab 接口，展示武器教程、创建助手与武器资产列表。
    /// </summary>
    public class WeaponsTab : ITab
    {
        // 标签页名称，显示在窗口顶部的标签栏中。
        public string TabName => "Weapons";

        // 教程与支持入口所使用的两张图片。
        private Texture2D weaponsImage, supportImage;

        #region WEAPON_LISTING_VARIABLES

        // 武器列表网格的列数。
        private int gridColumns = 4;

        // 武器搜索关键字。
        private string searchQuery = "";
        // 根据搜索关键字过滤后的武器列表（用于显示）。
        private List<Weapon_SO> filteredWeaponList;
        // 项目中所有 Weapon_SO 的完整列表（从资源数据库加载）。
        private List<Weapon_SO> weaponSOList = new List<Weapon_SO>();
        
        // 列表区域的滚动位置。
        private Vector2 scrollPos;

        #endregion

        #region WEAPON_CREATION_ASSISTANT_VARIABLES

        // 当前子工具栏索引（0=Get Started，1=Weapon Creation Assistant）。
        private int tabIndex = 1;
        // 武器创建助手实例（首次需要时懒加载创建）。
        private WeaponCreatorAssistant weaponCreatorAssistantInstance;

        #endregion

        /// <summary>
        /// 标签页被切换选中时调用（当前实现为空，无需额外初始化）。
        /// </summary>
        public void StartTab() { }

        /// <summary>
        /// 绘制武器标签页：左侧为教程/创建助手子页面，右侧为武器列表网格。
        /// </summary>
        public void OnGUI()
        {
            // 首次绘制且列表为空时，从资源数据库懒加载所有 Weapon_SO。
            if (weaponSOList.Count == 0)
            {
                LoadWeaponSOFiles();
            }

            if(weaponsImage == null) 
            {
                weaponsImage = Resources.Load<Texture2D>("CustomEditor/weapon-tutorial");   
                supportImage = Resources.Load<Texture2D>("CustomEditor/discord-support");
            }

            GUILayout.BeginHorizontal();

            GUILayout.BeginVertical(GUILayout.Width(EditorGUIUtility.currentViewWidth * .5f));

            string shortcutKey = Application.platform == RuntimePlatform.OSXEditor ? "Cmd+Q" : "Ctrl+Q";
            EditorGUILayout.HelpBox($"\nTIP: You can open the Cowsins Manager directly by pressing {shortcutKey}.\n", MessageType.Info);

            tabIndex = GUILayout.Toolbar(tabIndex, new string[] { "Get Started", "Weapon Creation Assistant" });

            switch (tabIndex)
            {
                case 0:
                    EditorGUILayout.HelpBox($"\nDo you need help to get started? No worries! Learn how to make new Weapons in FPS Engine: \n", MessageType.Info);

                    CowsinsEditorWindowUtilities.DrawTutorialCard(weaponsImage, "https://www.cowsins.com/videos/OykOx8CgEMc", 1);

                    GUI.backgroundColor = Color.white;

                    GUILayout.Space(10);
                    EditorGUILayout.HelpBox($"\nDo you still need help? Join us on Discord and ask for support!\n", MessageType.Info);

                    GUILayout.Space(5);
                    GUILayout.Label(supportImage, GUILayout.Width(400), GUILayout.Height(105));
                    if (GUILayout.Button("Join Discord Server")) Application.OpenURL("https://discord.gg/759gSeTT9m");
                    GUILayout.Space(5);
                    break;
                case 1:
                    if (weaponCreatorAssistantInstance == null)
                    {
                        weaponCreatorAssistantInstance = ScriptableObject.CreateInstance<WeaponCreatorAssistant>();
                    }
                    weaponCreatorAssistantInstance.OnGUI();
                    break;
            }


            GUILayout.EndVertical();

            GUILayout.Space(5);

            GUILayout.BeginVertical(GUILayout.Width(EditorGUIUtility.currentViewWidth * .49f));

            GUILayout.Label("WEAPONS LIST", EditorStyles.boldLabel);

            int buttonSize = (int)(EditorGUIUtility.currentViewWidth * 0.4f / gridColumns);
            int padding = 5;
            int hoveredIndex = -1;
            Rect hoveredRect = new Rect();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Search Weapons:", GUILayout.Width(100));
            searchQuery = GUILayout.TextField(searchQuery);
            GUILayout.EndHorizontal();

            // Filter the weapon list
            filteredWeaponList = weaponSOList
            .Where(weapon => weapon != null && weapon.name.ToLower().Contains(searchQuery.ToLower()))
            .ToList();

            GUILayout.Space(10);
            // Create the grid
            for (int i = 0; i < filteredWeaponList.Count; i += gridColumns)
            {
                GUILayout.BeginHorizontal();

                for (int j = i; j < i + gridColumns && j < filteredWeaponList.Count; j++)
                {
                    Weapon_SO weapon = filteredWeaponList[j];
                    Texture2D icon = AssetPreview.GetAssetPreview(weapon.icon);

                    Rect buttonRect = GUILayoutUtility.GetRect(buttonSize, buttonSize);
                    if (GUI.Button(buttonRect, new GUIContent(icon, weapon.name)))
                    {
                        WeaponButtonClicked(j);
                    }

                    // Detect hover
                    if (buttonRect.Contains(Event.current.mousePosition))
                    {
                        hoveredIndex = j;
                        hoveredRect = buttonRect;
                    }

                    GUILayout.Space(padding);
                }

                GUILayout.EndHorizontal();
                GUILayout.Space(padding);
            }

            // Draw overlay and weapon name if the button is currently hovered
            if (hoveredIndex != -1)
            {
                Weapon_SO hoveredWeapon = weaponSOList[hoveredIndex];

                Color overlayColor = new Color(0, 0, 0, 0.5f);
                EditorGUI.DrawRect(hoveredRect, overlayColor);
                GUIStyle labelStyle = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontStyle = FontStyle.Bold,
                    normal = new GUIStyleState() { textColor = Color.white }
                };

                GUI.Label(hoveredRect, $"Edit {hoveredWeapon.name}", labelStyle);

                // Repaint the editor window 
                GUIUtility.ExitGUI();
            }


            GUILayout.EndVertical();
            GUILayout.EndHorizontal();
        }

        // 点击某个武器按钮时，在属性编辑器中打开对应的 Weapon_SO。
        private void WeaponButtonClicked(int index)
        {
            Weapon_SO selectedWeapon = weaponSOList[index];
            EditorUtility.OpenPropertyEditor(selectedWeapon);
        }
        
        // 通过 AssetDatabase 查找项目中所有 Weapon_SO 资源并加载到 weaponSOList 列表。
        private void LoadWeaponSOFiles()
        {
            string[] guids = AssetDatabase.FindAssets("t:Weapon_SO");

            weaponSOList.Clear();

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                Weapon_SO weapon = AssetDatabase.LoadAssetAtPath<Weapon_SO>(path);
                if (weapon != null)
                {
                    weaponSOList.Add(weapon);
                }
            }
        }
    }

}
#endif