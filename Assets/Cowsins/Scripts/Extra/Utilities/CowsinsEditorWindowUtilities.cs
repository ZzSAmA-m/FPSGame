// CowsinsEditorWindowUtilities（编辑器窗口工具类）：
// 仅在编辑器下编译。为 cowsins 引擎的自定义编辑器窗口提供可复用的 GUI 样式与绘制辅助方法，
// 包括标题/副标题样式、教程/链接卡片绘制，以及武器后坐力曲线图与准星预览的绘制。
#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
namespace cowsins
{
    /// <summary>
    /// 编辑器窗口工具：提供常用 GUIStyle 与各类 GUI 绘制辅助函数。
    /// </summary>
    public static partial class CowsinsEditorWindowUtilities
    {
        // 返回居中的粗体标题样式（字号 12）
        public static GUIStyle TitleStyle()
        {
            return new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.UpperCenter
            };

        }
        // 返回居中的副标题样式（字号 9，自动换行）
        public static GUIStyle SubtitleStyle()
        {
            return new GUIStyle(GUI.skin.label)
            {
                fontSize = 9,
                alignment = TextAnchor.UpperCenter,
                wordWrap = true
            };

        }
        // 返回大标题样式（字号 25，半透明白色粗体）
        public static GUIStyle BigHeadingStyle()
        {
            return new GUIStyle(GUI.skin.label)
            {
                fontSize = 25,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(1f, 1f, 1f, 0.3f) }
            };
        }

        // 绘制教程卡片：显示缩略图并在中心叠加播放按钮，点击后打开指定网址
        public static void DrawTutorialCard(Texture2D image, string url, float size)
        {
            GUILayout.Label(image, GUILayout.Width(400 * size), GUILayout.Height(250 * size));

            Rect imageRect = GUILayoutUtility.GetLastRect();
            float buttonSize = 50;
            float buttonX = imageRect.x + (imageRect.width / 2) - (buttonSize / 2);
            float buttonY = imageRect.y + (imageRect.height / 2) - (buttonSize / 2);

            if (string.IsNullOrWhiteSpace(url)) return;

            var playButtonStyle = new GUIStyle(GUI.skin.button) { normal = { textColor = Color.white } };
            GUI.backgroundColor = new Color(0, 0, 0, 0.5f);

            if (GUI.Button(new Rect(buttonX, buttonY, buttonSize, buttonSize), "▶", playButtonStyle))
            {
                Application.OpenURL(url);
            }
        }

        // 绘制链接卡片：显示图标与标题按钮，点击后打开指定网址
        public static void DrawLinkCard(Texture2D icon, string title, string url, float sizeX, float sizeY)
        {
            GUILayout.BeginVertical(GUI.skin.GetStyle("HelpBox"), GUILayout.Width(300 * sizeX), GUILayout.Height(300 * sizeY));
            GUILayout.FlexibleSpace();

            // Center the icon horizontally
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            GUILayout.Label(icon, GUILayout.Width(40 * sizeX), GUILayout.Height(40 * sizeX));
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            // Add some space before the title
            GUILayout.Space(5);

            // Center the button horizontally
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button(title, GUILayout.Width(100), GUILayout.Height(30)))
                Application.OpenURL(url);
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            GUILayout.FlexibleSpace();
            GUILayout.EndVertical();
        }

        // 绘制单条后坐力散布图：把弹匣内每发子弹的后坐力偏移累计成散点并绘制
        public static void DrawRecoilPlot(Weapon_SO myScript, bool aimed, string label, bool isAimPlot, bool showLabels)
        {
            Rect area = GUILayoutUtility.GetRect(200, 230);
            int magazineSize = myScript.magazineSize;

            AnimationCurve xCurve = myScript.recoilX;
            AnimationCurve yCurve = myScript.recoilY;
            float xAmount = aimed ? myScript.xRecoilAmountOnAiming : myScript.xRecoilAmount;
            float yAmount = aimed ? myScript.yRecoilAmountOnAiming : myScript.yRecoilAmount;

            Handles.BeginGUI();

            Vector2[] positions = new Vector2[magazineSize];
            Vector2 position = Vector2.zero;
            float minX = 0f, maxX = 0f, minY = 0f, maxY = 0f;

            for (int i = 0; i < magazineSize; i++)
            {
                float t = i / (float)(magazineSize - 1);
                position.x -= xCurve.Evaluate(t) * xAmount;
                position.y += yCurve.Evaluate(t) * yAmount;
                positions[i] = position;
                if (i == 0 || position.x < minX) minX = position.x;
                if (i == 0 || position.x > maxX) maxX = position.x;
                if (i == 0 || position.y < minY) minY = position.y;
                if (i == 0 || position.y > maxY) maxY = position.y;
            }

            float padding = 10f;
            float width = maxX - minX;
            float height = maxY - minY;
            float scaleX = (area.width - padding * 2) / (width != 0 ? width : 1);
            float scaleY = (area.height - padding * 2 - 20) / (height != 0 ? height : 1);
            float scale = Mathf.Min(scaleX, scaleY);

            Vector2 origin = new Vector2(area.x + area.width / 2f, area.yMax - padding - 20);

            Handles.color = Color.gray;
            Handles.DrawLine(new Vector3(area.x, origin.y), new Vector3(area.xMax, origin.y));
            Handles.DrawLine(new Vector3(origin.x, area.y), new Vector3(origin.x, origin.y));

            if (showLabels)
            {
                GUI.color = Color.white;
                GUIStyle labelStyle = new GUIStyle(GUI.skin.label);
                labelStyle.alignment = TextAnchor.UpperCenter;

                Vector2 labelPos0 = origin + new Vector2(0, 5);
                GUI.Label(new Rect(labelPos0.x - 15, labelPos0.y, 30, 20), "(0,0)", labelStyle);

                Vector2 labelMinX = origin + new Vector2((minX - (minX + width / 2f)) * scale, 5);
                GUI.Label(new Rect(labelMinX.x - 60, labelMinX.y, 60, 20), $"(-{xAmount:F1})", labelStyle);

                Vector2 labelMaxX = origin + new Vector2((maxX - (minX + width / 2f)) * scale, 5);
                GUI.Label(new Rect(labelMaxX.x, labelMaxX.y, 60, 20), $"({xAmount:F1})", labelStyle);

                Vector2 labelMaxY = origin + new Vector2(5, -(maxY - minY) * scale);
                GUI.Label(new Rect(labelMaxY.x, labelMaxY.y - 10, 60, 20), $"({yAmount:F1})", labelStyle);
            }

            Handles.color = isAimPlot ? Color.blue : Color.red;
            for (int i = 0; i < magazineSize; i++)
            {
                Vector2 offset = (positions[i] - new Vector2(minX + width / 2f, minY)) * scale;
                Vector2 drawPos = origin + new Vector2(offset.x, -offset.y);
                Handles.DrawSolidDisc(drawPos, Vector3.forward, 2f);
            }

            Handles.EndGUI();

            GUI.color = Color.white;
            GUIStyle bottomLabelStyle = new GUIStyle(GUI.skin.label);
            bottomLabelStyle.alignment = TextAnchor.UpperCenter;
            bottomLabelStyle.fontStyle = FontStyle.Bold;
            GUI.Label(new Rect(area.x, area.yMax - 10, area.width, 20), label, bottomLabelStyle);
        }

        // 绘制合并的后坐力散布图：同时绘制默认与瞄准两种状态的后坐力散点（红/蓝两色）
        public static void DrawCombinedRecoilPlot(Weapon_SO myScript, bool showLabels)
        {
            Rect area = GUILayoutUtility.GetRect(200, 230);
            int magazineSize = myScript.magazineSize;

            AnimationCurve xCurve = myScript.recoilX;
            AnimationCurve yCurve = myScript.recoilY;

            float xAmountDefault = myScript.xRecoilAmount;
            float yAmountDefault = myScript.yRecoilAmount;
            float xAmountAimed = myScript.applyDifferentRecoilOnAiming ? myScript.xRecoilAmountOnAiming : myScript.xRecoilAmount;
            float yAmountAimed = myScript.applyDifferentRecoilOnAiming ? myScript.yRecoilAmountOnAiming : myScript.yRecoilAmount;

            Handles.BeginGUI();

            Vector2[] positionsDefault = new Vector2[magazineSize];
            Vector2[] positionsAimed = new Vector2[magazineSize];
            Vector2 positionDefault = Vector2.zero;
            Vector2 positionAimed = Vector2.zero;

            float minX = 0f, maxX = 0f, minY = 0f, maxY = 0f;

            for (int i = 0; i < magazineSize; i++)
            {
                float t = i / (float)(magazineSize - 1);
                positionDefault.x -= xCurve.Evaluate(t) * xAmountDefault;
                positionDefault.y += yCurve.Evaluate(t) * yAmountDefault;
                positionAimed.x -= xCurve.Evaluate(t) * xAmountAimed;
                positionAimed.y += yCurve.Evaluate(t) * yAmountAimed;

                positionsDefault[i] = positionDefault;
                positionsAimed[i] = positionAimed;

                if (i == 0 || positionDefault.x < minX) minX = positionDefault.x;
                if (i == 0 || positionDefault.x > maxX) maxX = positionDefault.x;
                if (i == 0 || positionDefault.y < minY) minY = positionDefault.y;
                if (i == 0 || positionDefault.y > maxY) maxY = positionDefault.y;

                if (positionAimed.x < minX) minX = positionAimed.x;
                if (positionAimed.x > maxX) maxX = positionAimed.x;
                if (positionAimed.y < minY) minY = positionAimed.y;
                if (positionAimed.y > maxY) maxY = positionAimed.y;
            }

            float padding = 10f;
            float width = maxX - minX;
            float height = maxY - minY;
            float scaleX = (area.width - padding * 2) / (width != 0 ? width : 1);
            float scaleY = (area.height - padding * 2 - 20) / (height != 0 ? height : 1);
            float scale = Mathf.Min(scaleX, scaleY);

            Vector2 origin = new Vector2(area.x + area.width / 2f, area.yMax - padding - 20);

            Handles.color = Color.gray;
            Handles.DrawLine(new Vector3(area.x, origin.y), new Vector3(area.xMax, origin.y));
            Handles.DrawLine(new Vector3(origin.x, area.y), new Vector3(origin.x, origin.y));

            if (showLabels)
            {
                GUI.color = Color.white;
                GUIStyle labelStyle = new GUIStyle(GUI.skin.label);
                labelStyle.alignment = TextAnchor.UpperCenter;

                Vector2 labelPos0 = origin + new Vector2(0, 5);
                GUI.Label(new Rect(labelPos0.x - 15, labelPos0.y, 30, 20), "(0,0)", labelStyle);

                Vector2 labelMinX = origin + new Vector2((minX - (minX + width / 2f)) * scale, 5);
                GUI.Label(new Rect(labelMinX.x - 60, labelMinX.y, 60, 20), $"(-{Mathf.Max(xAmountDefault, xAmountAimed):F1})", labelStyle);

                Vector2 labelMaxX = origin + new Vector2((maxX - (minX + width / 2f)) * scale, 5);
                GUI.Label(new Rect(labelMaxX.x, labelMaxX.y, 60, 20), $"({Mathf.Max(xAmountDefault, xAmountAimed):F1})", labelStyle);

                Vector2 labelMaxY = origin + new Vector2(5, -(maxY - minY) * scale);
                GUI.Label(new Rect(labelMaxY.x, labelMaxY.y - 10, 60, 20), $"({Mathf.Max(yAmountDefault, yAmountAimed):F1})", labelStyle);
            }

            Handles.color = Color.red;
            for (int i = 0; i < magazineSize; i++)
            {
                Vector2 offset = (positionsDefault[i] - new Vector2(minX + width / 2f, minY)) * scale;
                Vector2 drawPos = origin + new Vector2(offset.x, -offset.y);
                Handles.DrawSolidDisc(drawPos, Vector3.forward, 2f);
            }

            Handles.color = Color.blue;
            for (int i = 0; i < magazineSize; i++)
            {
                Vector2 offset = (positionsAimed[i] - new Vector2(minX + width / 2f, minY)) * scale;
                Vector2 drawPos = origin + new Vector2(offset.x, -offset.y);
                Handles.DrawSolidDisc(drawPos, Vector3.forward, 2f);
            }

            Handles.EndGUI();

            GUI.color = Color.white;
            GUIStyle bottomLabelStyle = new GUIStyle(GUI.skin.label);
            bottomLabelStyle.alignment = TextAnchor.UpperCenter;
            bottomLabelStyle.fontStyle = FontStyle.Bold;
            GUI.Label(new Rect(area.x, area.yMax - 10, area.width, 20), "Combined Recoil", bottomLabelStyle);
        }
        // 绘制准星编辑区块：左侧为属性字段，右侧为准星预览
        public static void DrawCrosshairEditorSection(SerializedObject serializedObject, CrosshairParts parts, string propertyName)
        {
            EditorGUILayout.BeginHorizontal();

            EditorGUILayout.BeginVertical(GUILayout.Width(160));
            EditorGUILayout.PropertyField(serializedObject.FindProperty(propertyName), true);
            EditorGUILayout.EndVertical();

            EditorGUILayout.BeginVertical(GUILayout.ExpandWidth(true));
            DrawCrosshairPreview(parts);
            EditorGUILayout.EndVertical();

            EditorGUILayout.EndHorizontal();
        }
        // 根据 CrosshairParts 的各开关，用纯色纹理绘制准星预览图
        public static void DrawCrosshairPreview(CrosshairParts parts)
        {
            float previewSize = 100f;
            float lineLength = 12f;
            float thickness = 3f;
            float bracketSpread = 20f;
            float bracketSize = 8f;

            Rect previewRect = GUILayoutUtility.GetRect(previewSize, previewSize, GUILayout.ExpandWidth(true));

            Vector2 center = previewRect.center;

            float offset = lineLength / 2;

            Color originalColor = GUI.color;
            GUI.color = Color.green;

            if (parts.topPart)
                GUI.DrawTexture(new Rect(center.x - thickness / 2, center.y - offset - lineLength, thickness, lineLength), Texture2D.whiteTexture);

            if (parts.downPart)
                GUI.DrawTexture(new Rect(center.x - thickness / 2, center.y + offset, thickness, lineLength), Texture2D.whiteTexture);

            if (parts.leftPart)
                GUI.DrawTexture(new Rect(center.x - offset - lineLength, center.y - thickness / 2, lineLength, thickness), Texture2D.whiteTexture);

            if (parts.rightPart)
                GUI.DrawTexture(new Rect(center.x + offset, center.y - thickness / 2, lineLength, thickness), Texture2D.whiteTexture);

            if (parts.center)
                GUI.DrawTexture(new Rect(center.x - thickness / 2, center.y - thickness / 2, thickness, thickness), Texture2D.whiteTexture);

            if (parts.topLeftBracket)
            {
                GUI.DrawTexture(new Rect(center.x - bracketSpread, center.y - bracketSpread, bracketSize, thickness), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(center.x - bracketSpread, center.y - bracketSpread, thickness, bracketSize), Texture2D.whiteTexture);
            }

            if (parts.topRightBracket)
            {
                GUI.DrawTexture(new Rect(center.x + bracketSpread - bracketSize, center.y - bracketSpread, bracketSize, thickness), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(center.x + bracketSpread - thickness, center.y - bracketSpread, thickness, bracketSize), Texture2D.whiteTexture);
            }

            if (parts.bottomLeftBracket)
            {
                GUI.DrawTexture(new Rect(center.x - bracketSpread, center.y + bracketSpread - thickness, bracketSize, thickness), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(center.x - bracketSpread, center.y + bracketSpread - bracketSize, thickness, bracketSize), Texture2D.whiteTexture);
            }

            if (parts.bottomRightBracket)
            {
                GUI.DrawTexture(new Rect(center.x + bracketSpread - bracketSize, center.y + bracketSpread - thickness, bracketSize, thickness), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(center.x + bracketSpread - thickness, center.y + bracketSpread - bracketSize, thickness, bracketSize), Texture2D.whiteTexture);
            }

            GUI.color = originalColor;
        }

    }
}
#endif