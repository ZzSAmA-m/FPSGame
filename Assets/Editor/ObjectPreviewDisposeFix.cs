#if UNITY_EDITOR
using System.Collections;
using System.Reflection;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Unity 6 在编译/进出 Play 时，Inspector 里的 ObjectPreview
/// （含 UnityEvent 预览和 Mirror NetworkInformationPreview）可能没走到 Cleanup。
/// 在域重载前主动清掉，避免 Finalize 报错。
/// </summary>
[InitializeOnLoad]
static class ObjectPreviewDisposeFix
{
    static readonly BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    static ObjectPreviewDisposeFix()
    {
        AssemblyReloadEvents.beforeAssemblyReload += CleanupInspectorPreviews;
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
        EditorApplication.quitting += CleanupInspectorPreviews;
    }

    static void OnPlayModeChanged(PlayModeStateChange change)
    {
        if (change == PlayModeStateChange.ExitingEditMode || change == PlayModeStateChange.ExitingPlayMode)
            CleanupInspectorPreviews();
    }

    static void CleanupInspectorPreviews()
    {
        var editorAsm = typeof(Editor).Assembly;
        var propertyEditorType = editorAsm.GetType("UnityEditor.PropertyEditor");
        if (propertyEditorType == null)
            return;

        var previewsField = propertyEditorType.GetField("m_Previews", Flags);
        var selectedField = propertyEditorType.GetField("m_SelectedPreview", Flags);
        var cachedField = propertyEditorType.GetField("m_cachedPreviewEditor", Flags);
        var dummyField = typeof(Editor).GetField("m_DummyPreview", Flags);
        var trackerField = propertyEditorType.GetField("m_Tracker", Flags);

        foreach (var window in Resources.FindObjectsOfTypeAll(propertyEditorType))
        {
            CleanupValue(previewsField?.GetValue(window));
            CleanupValue(selectedField?.GetValue(window));
            CleanupValue(cachedField?.GetValue(window));

            if (trackerField?.GetValue(window) is ActiveEditorTracker tracker)
            {
                var editors = tracker.activeEditors;
                if (editors == null)
                    continue;
                    foreach (var editor in editors)
                    {
                        if (editor == null)
                            continue;
                        CleanupValue(dummyField?.GetValue(editor));
                    }
            }
        }
    }

    static void CleanupValue(object value)
    {
        if (value == null)
            return;

        if (value is ObjectPreview preview)
        {
            TryCleanup(preview);
            return;
        }

        if (value is IEnumerable list && value is not string)
        {
            foreach (var item in list)
            {
                if (item is ObjectPreview itemPreview)
                    TryCleanup(itemPreview);
            }
        }
    }

    static void TryCleanup(ObjectPreview preview)
    {
        if (preview == null)
            return;
        try
        {
            preview.Cleanup();
        }
        catch
        {
        }
    }
}
#endif
