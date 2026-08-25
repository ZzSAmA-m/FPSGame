// =====================================================================
// ExternalButtonRegister.cs：示例脚本，演示如何向场景视图中的可拖拽按钮菜单
// 注册额外的自定义按钮。通过订阅 DraggableButtonInSceneView.OnAddExternalButtons
// 事件，在菜单展开时追加按钮。仅在编辑器环境（UNITY_EDITOR）下编译。
// =====================================================================
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace cowsins
{
    [InitializeOnLoad]
    /// <summary>
    /// 外部按钮注册示例：订阅可拖拽按钮的扩展事件，以在菜单中添加自定义按钮。
    /// </summary>
    public static class ExternalButtonRegister
    {
        // 静态构造函数：订阅 OnAddExternalButtons 事件，菜单绘制时回调 AddCustomButtons。
        static ExternalButtonRegister()
        {
            DraggableButtonInSceneView.OnAddExternalButtons += AddCustomButtons;
        }

        // 在可拖拽按钮展开的菜单中追加自定义按钮（此处为示例占位逻辑）。
        private static void AddCustomButtons(Rect menuRect)
        {
            // 单个菜单项的宽度（菜单宽度的三分之一）。
            float itemWidth = menuRect.width / 3;
            // 单个菜单项的高度（与菜单高度一致）。
            float itemHeight = menuRect.height;


            /// OVERRIDE BUTTON COUNT ( NECESSARY ) 
            //DraggableButtonInSceneView.buttonCount = 5;

            /// ADD 5TH BUTTON
            /*
            Texture2D customButtonImage = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/CustomButton.png");
            // Add custom button
            if (GUI.Button(new Rect(DraggableButtonInSceneView.GetButtonPosition(menuRect, 5), menuRect.y, itemWidth, itemHeight), customButtonImage))
            {
                Debug.Log("Custom Button Clicked");
            }
            */

            /// ADD 6TH BUTTON
            /*
            Texture2D customSecondButtonImage = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/CustomButton.png");
            if (GUI.Button(new Rect(DraggableButtonInSceneView.GetButtonPosition(menuRect, 6), menuRect.y, itemWidth, itemHeight), customSecondButtonImage))
            {
                Debug.Log("Custom Button Clicked");
            }
            */

        }

    }

}
#endif