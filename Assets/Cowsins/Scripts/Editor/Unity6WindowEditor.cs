// =====================================================================
// Unity6WindowEditor.cs：Unity 6 设置引导窗口。
// 当项目运行在 Unity 6 上时，首次打开项目会弹出该窗口，提供 URP / Built-In / HDRP
// 渲染管线的配置教程链接，并支持“不再显示”设置。仅在编辑器环境（UNITY_EDITOR）下编译。
// =====================================================================
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
namespace cowsins
{
    [InitializeOnLoad]
    /// <summary>
    /// Unity 6 设置引导窗口：展示当前版本提示与各渲染管线的配置教程。
    /// </summary>
    public class Unity6EditorWindow : EditorWindow
    {
        // 页头图片。
        private Texture2D headerImage;
        // URP / Built-In / HDRP 三个渲染管线教程的缩略图。
        private Texture2D urpImage;
        private Texture2D builtInImage;
        private Texture2D hdrpImage;

        // 教程画廊区域的滚动位置。
        private Vector2 scrollPosition;

        // 是否正在使用 Unity 6。
        private bool isUsingUnity6;

        // 静态构造函数：延迟调用 ShowWindowOnce，实现首次打开项目时自动弹窗。
        static Unity6EditorWindow()
        {
            EditorApplication.delayCall += ShowWindowOnce;
        }

        // 每个会话首次初始化时，若使用 Unity 6 且未勾选“不再显示”，则自动打开窗口。
        private static void ShowWindowOnce()
        {
            if (!SessionState.GetBool("FirstInit", false))
            {
                if (!CowsinsUtilities.IsUsingUnity6() || EditorPrefs.GetBool("Unity6EditorWindowDontShowAgain") == true) return;
                ShowWindow();
                SessionState.SetBool("FirstInit", true);
            }
        }

        [MenuItem("Cowsins/Unity 6 Set Up")]
        /// <summary>
        /// 打开 Unity 6 设置窗口（菜单入口）。
        /// </summary>
        public static void ShowWindow()
        {
            Unity6EditorWindow window = GetWindow<Unity6EditorWindow>("FPS ENGINE | Unity 6 Set Up");
            window.minSize = new Vector2(500, 550);
            window.maxSize = new Vector2(500, 550);
        }

        // 窗口启用时加载所需图片，并检测当前是否使用 Unity 6。
        private void OnEnable()
        {
            headerImage = Resources.Load<Texture2D>("CustomEditor/Unity6Logo");
            urpImage = Resources.Load<Texture2D>("CustomEditor/URP");
            builtInImage = Resources.Load<Texture2D>("CustomEditor/BuiltIn");
            hdrpImage = Resources.Load<Texture2D>("CustomEditor/HDRP");

            isUsingUnity6 = CowsinsUtilities.IsUsingUnity6();
        }

        // 绘制窗口内容：页头、版本提示、教程画廊与底部“不再显示”页脚。
        private void OnGUI()
        {
            // Header
            GUILayout.Label(headerImage, GUILayout.Width(position.width * 0.98f), GUILayout.Height(headerImage.height * (position.width / headerImage.width)));
            GUILayout.Space(10);
            // Unity 6 Information
            if (!isUsingUnity6) EditorGUILayout.HelpBox("You are not using Unity 6.", MessageType.Error);
            else EditorGUILayout.HelpBox("You are using FPS Engine in Unity 6. Explore helpful tutorials to learn how to configure FPS Engine for your preferred Render Pipeline.", MessageType.Info);
            DrawTutorialGallery();

            // We need to use Flexible Space to push the footer content to the bottom
            GUILayout.FlexibleSpace();

            DrawFooter();
        }


        // 绘制底部页脚：深色背景条与“不再显示”开关。
        private void DrawFooter()
        {
            Color footerColor = new Color(0.2f, 0.2f, 0.2f, 1f);
            Rect footerRect = new Rect(0, position.height - 21, position.width, 50);

            GUI.color = footerColor;
            GUI.DrawTexture(footerRect, Texture2D.whiteTexture);
            GUI.color = Color.white;

            bool dontShowAgain = EditorPrefs.GetBool("Unity6EditorWindowDontShowAgain", false);
            dontShowAgain = EditorGUILayout.Toggle("Don't show again", dontShowAgain, GUILayout.Width(position.width - 20));

            EditorPrefs.SetBool("Unity6EditorWindowDontShowAgain", dontShowAgain);
        }

        // 绘制教程画廊：标题与三个渲染管线教程按钮组成的横向滚动区。
        private void DrawTutorialGallery()
        {
            GUILayout.Space(10);
            GUILayout.BeginVertical("Box");
            GUILayout.Label("Tutorials", EditorStyles.boldLabel);
            GUILayout.EndVertical();

            scrollPosition = GUILayout.BeginScrollView(scrollPosition);
            GUILayout.BeginHorizontal();

            DrawTutorialButton(urpImage, "https://www.cowsins.com/videos/PyAywKlqKrY");
            DrawTutorialButton(builtInImage, "https://www.cowsins.com/videos/aXW34tlzNGE");
            DrawTutorialButton(hdrpImage, "https://www.cowsins.com/videos/kyJFwmcs-6Q");

            GUILayout.EndHorizontal();
            GUILayout.EndScrollView();
        }

        // 绘制单个教程按钮：缩略图 + 居中的播放按钮，点击后打开对应教程链接。
        private void DrawTutorialButton(Texture2D image, string link)
        {
            float imageWidth = 300;
            float imageHeight = 200;
            float buttonSize = 50;

            GUILayout.BeginVertical();
            GUILayout.Label(image, GUILayout.Width(imageWidth), GUILayout.Height(imageHeight));
            Rect imageRect = GUILayoutUtility.GetLastRect();
            float buttonX = imageRect.x + (imageRect.width / 2) - (buttonSize / 2);
            float buttonY = imageRect.y + (imageRect.height / 2) - (buttonSize / 2);

            GUIStyle playButtonStyle = new GUIStyle(GUI.skin.button);
            Color halfOpacityBlack = new Color(0, 0, 0, 0.5f);
            GUI.backgroundColor = halfOpacityBlack;
            playButtonStyle.normal.textColor = Color.white;

            if (GUI.Button(new Rect(buttonX, buttonY, buttonSize, buttonSize), "▶", playButtonStyle))
            {
                Application.OpenURL(link);
            }

            GUI.backgroundColor = Color.white;

            GUILayout.EndVertical();
        }

    }
}
#endif