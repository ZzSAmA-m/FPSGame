// GetGameInformation（游戏信息 / 帧率显示）：
// 实时统计当前 FPS、平均 FPS 与最高 FPS，并把结果写入 TextMeshProUGUI 文本，
// 依据帧率高低用不同颜色区分显示。另含一个仅编辑器使用的自定义 Inspector（GetGameInformatioEditor）。
/// <summary>
/// This script belongs to cowsins™ as a part of the cowsins´ FPS Engine. All rights reserved. 
/// </summary>

#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;
using TMPro;
using System.Text;
namespace cowsins
{
    /// <summary>
    /// 帧率显示组件：统计并显示当前/平均/最高帧率，按帧率高低着色。
    /// </summary>
    public class GetGameInformation : MonoBehaviour
    {
        // 是否显示 FPS
        public bool showFPS;
        // 是否显示平均帧率与最高帧率
        public bool showAverageFrameRate, showMaximumFrameRate;

        // FPS 文本刷新间隔（秒）
        [SerializeField, Range(.01f, 1f)] private float fpsRefreshRate = 0.5f;
        // 显示 FPS 的文本组件
        [SerializeField] private TextMeshProUGUI fpsObject;

        // 帧率良好/中等/较差三种状态对应的颜色
        [SerializeField] private Color appropriateValueColor, intermediateValueColor, badValueColor;

        // FPS 刷新计时器
        private float fpsTimer;
        // 当前/平均/最高帧率
        private float fps, avgFPS, maxFps;
        // 环形缓冲区索引（用于滚动记录每帧耗时）
        private int lastFrameIndex;
        // 存储最近若干帧耗时的环形数组
        private float[] frameDeltaTimeArray;

        // 复用的字符串构建器，避免频繁分配
        private static readonly StringBuilder sb = new StringBuilder(128);

        // 初始化：开启 FPS 显示则设置计时器，否则销毁文本；并分配耗时数组
        private void Start()
        {
            if (showFPS)
                fpsTimer = fpsRefreshRate;
            else
                Destroy(fpsObject);

            frameDeltaTimeArray = new float[50];
        }

        // 每帧更新：记录帧耗时、计算实时帧率，并在刷新周期内更新显示文本
        private void Update()
        {
            if (!showFPS) return;

            frameDeltaTimeArray[lastFrameIndex] = Time.deltaTime;
            lastFrameIndex = (lastFrameIndex + 1) % frameDeltaTimeArray.Length;
            fps = CalculateFramerate();

            fpsTimer -= Time.deltaTime;

            if (fpsTimer <= 0)
            {
                // 平均帧率 = 总帧数 / 总时间
                avgFPS = Time.frameCount / Time.time;
                if (fps > maxFps) maxFps = fps;
                fpsTimer = fpsRefreshRate;

                sb.Clear();

                AppendFPS("Current FPS: ", fps);
                if (showAverageFrameRate) AppendFPS("\nAvg FPS: ", avgFPS);
                if (showMaximumFrameRate) AppendFPS("\nMax FPS: ", maxFps);

                fpsObject.text = sb.ToString();
            }
        }

        // 根据环形缓冲区的平均帧耗时计算帧率（帧数 / 总耗时）
        private float CalculateFramerate()
        {
            float total = 0;
            foreach (float deltaTime in frameDeltaTimeArray)
                total += deltaTime;
            return frameDeltaTimeArray.Length / total;
        }

        // 向字符串构建器追加一行带颜色的 FPS 文本
        private void AppendFPS(string label, float fpsValue)
        {
            // 按帧率高低选择颜色：<15 差、<45 中等、否则良好
            Color color = fpsValue < 15 ? badValueColor :
                          fpsValue < 45 ? intermediateValueColor :
                          appropriateValueColor;

            sb.Append(label)
              .Append("<color=#");
            AppendColorHex(sb, color);
            sb.Append(">")
              .Append(Mathf.RoundToInt(fpsValue))
              .Append("</color>");
        }

        // 把颜色转换为十六进制字符串并追加（用于富文本标签）
        private static void AppendColorHex(StringBuilder sb, Color color)
        {
            int r = (int)(color.r * 255f);
            int g = (int)(color.g * 255f);
            int b = (int)(color.b * 255f);

            AppendHexByte(sb, r);
            AppendHexByte(sb, g);
            AppendHexByte(sb, b);
        }

        // 追加一个字节的两位十六进制表示
        private static void AppendHexByte(StringBuilder sb, int b)
        {
            const string hexDigits = "0123456789ABCDEF";
            sb.Append(hexDigits[(b >> 4) & 0xF]);
            sb.Append(hexDigits[b & 0xF]);
        }
    }

#if UNITY_EDITOR
    [System.Serializable]
    [CustomEditor(typeof(GetGameInformation))]
    /// <summary>
    /// GetGameInformation 的自定义编辑器：按需显示 FPS 与颜色相关字段。
    /// </summary>
    public class GetGameInformatioEditor : Editor
    {

        // 自定义 Inspector：仅在开启 showFPS 时显示帧率相关字段，并单独展示颜色设置
        override public void OnInspectorGUI()
        {
            serializedObject.Update();
            GetGameInformation myScript = target as GetGameInformation;

            EditorGUILayout.LabelField("FPS", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("showFPS"));
            if (myScript.showFPS)
            {
                EditorGUILayout.PropertyField(serializedObject.FindProperty("fpsRefreshRate"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("fpsObject"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("showAverageFrameRate"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("showMaximumFrameRate"));
            }
            EditorGUILayout.Space(10f);
            EditorGUILayout.LabelField("COLOR", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("appropriateValueColor"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("intermediateValueColor"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("badValueColor"));

            serializedObject.ApplyModifiedProperties();

        }
    }
#endif
}