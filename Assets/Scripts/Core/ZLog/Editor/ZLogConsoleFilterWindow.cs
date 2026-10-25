using System;
using System.Reflection;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEngine;

namespace Core.ZLog.Editor
{
    /// <summary>
    /// 在 Unity Console 顶栏加一个 ZLog 按钮。点开后按 Channel 和 Frequency 决定要不要打印。
    /// 勾选结果记在 EditorPrefs 里，脚本重新编译后会重新套用。
    /// </summary>
    [InitializeOnLoad]
    static class ZLogConsoleToolbar
    {
        const string ChannelPrefix = "ZLog.Channel.";
        const string FrequencyPrefix = "ZLog.Frequency.";

        static readonly MethodInfo DrawToolbarButtonMethod =
            typeof(ZLogConsoleToolbar).GetMethod(nameof(DrawToolbarButton), BindingFlags.Static | BindingFlags.NonPublic);

        static Delegate _toolbarHandler;

        static ZLogConsoleToolbar()
        {
            ApplySaved();
            EditorApplication.update += EnsureAttached;
        }

        [DidReloadScripts]
        static void OnScriptsReloaded()
        {
            ApplySaved();
            EditorApplication.delayCall += EnsureAttached;
        }

        static void EnsureAttached()
        {
            try
            {
                if (!AttachToConsoleToolbar()) return;
                EditorApplication.update -= EnsureAttached;
            }
            catch (Exception exception)
            {
                EditorApplication.update -= EnsureAttached;
                Debug.LogException(exception);
            }
        }

        static bool AttachToConsoleToolbar()
        {
            Type consoleType = typeof(EditorWindow).Assembly.GetType("UnityEditor.ConsoleWindow");
            EventInfo toolbarEvent = consoleType?.GetEvent(
                "drawCustomToolbarGui",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            MethodInfo add = toolbarEvent?.GetAddMethod(true);
            if (add == null || DrawToolbarButtonMethod == null)
            {
                return false;
            }

            if (_toolbarHandler == null)
            {
                _toolbarHandler = Delegate.CreateDelegate(toolbarEvent.EventHandlerType, DrawToolbarButtonMethod);
            }

            MethodInfo remove = toolbarEvent.GetRemoveMethod(true);
            remove?.Invoke(null, new object[] { _toolbarHandler });
            add.Invoke(null, new object[] { _toolbarHandler });
            return true;
        }

        static void DrawToolbarButton()
        {
            bool filtering = IsFiltering();
            var content = new GUIContent(
                filtering ? "ZLog*" : "ZLog",
                "关掉的 Channel 或 Frequency 不会打印到 Console");
            Rect rect = GUILayoutUtility.GetRect(content, EditorStyles.toolbarDropDown);
            if (EditorGUI.DropdownButton(rect, content, FocusType.Passive, EditorStyles.toolbarDropDown))
            {
                PopupWindow.Show(rect, new ZLogConsoleFilterPopup());
            }
        }

        static bool IsFiltering()
        {
            foreach (LogChannel channel in Enum.GetValues(typeof(LogChannel)))
            {
                if (!ZLog.IsChannelVisible(channel)) return true;
            }

            foreach (LogFrequency frequency in Enum.GetValues(typeof(LogFrequency)))
            {
                if (!ZLog.IsFrequencyVisible(frequency)) return true;
            }

            return false;
        }

        internal static void DrawFilterGui()
        {
            EditorGUILayout.LabelField("ZLog 过滤", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "勾选才会打印。关掉 Channel 或 Frequency 后，对应日志不会调用 Debug.Log，也不会补进 Console。",
                MessageType.Info);

            DrawChannels();
            EditorGUILayout.Space();
            DrawFrequencies();
        }

        static void DrawChannels()
        {
            EditorGUILayout.LabelField("Channel", EditorStyles.boldLabel);
            DrawBulkButtons(SetEveryChannel);
            foreach (LogChannel channel in Enum.GetValues(typeof(LogChannel)))
            {
                DrawToggle(
                    ChannelLabel(channel),
                    channel.ToString(),
                    ZLog.IsChannelVisible(channel),
                    visible => SetChannel(channel, visible));
            }
        }

        static void DrawFrequencies()
        {
            EditorGUILayout.LabelField("Frequency", EditorStyles.boldLabel);
            DrawBulkButtons(SetEveryFrequency);
            foreach (LogFrequency frequency in Enum.GetValues(typeof(LogFrequency)))
            {
                DrawToggle(
                    FrequencyLabel(frequency),
                    frequency.ToString(),
                    ZLog.IsFrequencyVisible(frequency),
                    visible => SetFrequency(frequency, visible));
            }
        }

        static void DrawBulkButtons(Action<bool> apply)
        {
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("全选", GUILayout.Width(80f)))
            {
                apply(true);
            }

            if (GUILayout.Button("全不选", GUILayout.Width(80f)))
            {
                apply(false);
            }

            EditorGUILayout.EndHorizontal();
        }

        static void DrawToggle(string label, string tooltip, bool visible, Action<bool> apply)
        {
            bool next = EditorGUILayout.ToggleLeft(new GUIContent(label, tooltip), visible);
            if (next != visible)
            {
                apply(next);
            }
        }

        static void SetEveryChannel(bool visible)
        {
            ZLog.SetAllChannelsVisible(visible);
            foreach (LogChannel channel in Enum.GetValues(typeof(LogChannel)))
            {
                EditorPrefs.SetBool(ChannelPrefix + channel, visible);
            }

            RepaintConsoles();
        }

        static void SetEveryFrequency(bool visible)
        {
            ZLog.SetAllFrequenciesVisible(visible);
            foreach (LogFrequency frequency in Enum.GetValues(typeof(LogFrequency)))
            {
                EditorPrefs.SetBool(FrequencyPrefix + frequency, visible);
            }

            RepaintConsoles();
        }

        static void SetChannel(LogChannel channel, bool visible)
        {
            ZLog.SetChannelVisible(channel, visible);
            EditorPrefs.SetBool(ChannelPrefix + channel, visible);
            RepaintConsoles();
        }

        static void SetFrequency(LogFrequency frequency, bool visible)
        {
            ZLog.SetFrequencyVisible(frequency, visible);
            EditorPrefs.SetBool(FrequencyPrefix + frequency, visible);
            RepaintConsoles();
        }

        static void ApplySaved()
        {
            foreach (LogChannel channel in Enum.GetValues(typeof(LogChannel)))
            {
                ZLog.SetChannelVisible(channel, EditorPrefs.GetBool(ChannelPrefix + channel, true));
            }

            foreach (LogFrequency frequency in Enum.GetValues(typeof(LogFrequency)))
            {
                ZLog.SetFrequencyVisible(frequency, EditorPrefs.GetBool(FrequencyPrefix + frequency, true));
            }
        }

        static void RepaintConsoles()
        {
            Type consoleType = typeof(EditorWindow).Assembly.GetType("UnityEditor.ConsoleWindow");
            if (consoleType == null) return;
            UnityEngine.Object[] windows = Resources.FindObjectsOfTypeAll(consoleType);
            foreach (UnityEngine.Object window in windows)
            {
                if (window is EditorWindow editorWindow)
                {
                    editorWindow.Repaint();
                }
            }
        }

        static string ChannelLabel(LogChannel channel)
        {
            switch (channel)
            {
                case LogChannel.Default: return "默认";
                case LogChannel.Input: return "Input";
                case LogChannel.Network: return "Network";
                case LogChannel.GamePlay: return "GamePlay";
                default: return channel.ToString();
            }
        }

        static string FrequencyLabel(LogFrequency frequency)
        {
            switch (frequency)
            {
                case LogFrequency.Default: return "默认";
                case LogFrequency.Low: return "低";
                case LogFrequency.Medium: return "中";
                case LogFrequency.High: return "高";
                case LogFrequency.Extreme: return "极高";
                default: return frequency.ToString();
            }
        }
    }

    sealed class ZLogConsoleFilterPopup : PopupWindowContent
    {
        public override Vector2 GetWindowSize()
        {
            return new Vector2(280f, 340f);
        }

        public override void OnGUI(Rect rect)
        {
            ZLogConsoleToolbar.DrawFilterGui();
        }
    }
}
