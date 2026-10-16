using GamePlay.Player;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(FbiAnimator))]
public class FbiAnimatorEditor : Editor
{
    public override void OnInspectorGUI()
    {
        EditorGUILayout.HelpBox(
            "真正播动画的是子物体 Model/ctm_fbi 上的 Animator。\n把 clip 拖到下面的「Clip」槽，再点编辑器顶部的 Play。\n不要拖到 Hierarchy 里的 Player 图标上（那里没有能播这套骨骼的 Animator）。",
            MessageType.Info);

        serializedObject.Update();
        EditorGUILayout.PropertyField(serializedObject.FindProperty("animator"), new GUIContent("Animator"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("controller"), new GUIContent("Controller"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("clip"), new GUIContent("动画（拖到这里）"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("applyRootMotion"), new GUIContent("根运动"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("defaultClip"), new GUIContent("未指定时默认片段"));
        serializedObject.ApplyModifiedProperties();

        var driver = (FbiAnimator)target;
        using (new EditorGUI.DisabledScope(driver.Animator == null))
        {
            if (GUILayout.Button(Application.isPlaying ? "重播" : "进 Play 并播放", GUILayout.Height(36)))
            {
                if (Application.isPlaying)
                    driver.PlayAssigned();
                else
                    EditorApplication.isPlaying = true;
            }
        }
    }
}
