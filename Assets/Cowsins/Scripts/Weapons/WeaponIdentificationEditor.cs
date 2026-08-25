// ===================================================================
// WeaponIdentificationEditor.cs —— WeaponIdentification 的自定义 Inspector 与 Scene 视图工具。
// 仅在编辑器（UNITY_EDITOR）下编译。主要功能：
// 1) 在 Inspector 中按 Basic / Attachments 标签页绘制武器引用、开火点、默认/兼容附件等字段；
// 2) 提供“自动分配附件”按钮与运行时附件状态可视化；
// 3) 在 Scene 视图右下角绘制浮窗，包含武器相机预览与动画列表/播放器（依赖 SceneView 回调）。
// ===================================================================
#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.AnimatedValues;
using System.Collections.Generic;

namespace cowsins
{
    [CustomEditor(typeof(WeaponIdentification))]
    [InitializeOnLoad]
    /// <summary>
    /// WeaponIdentification 的自定义 Inspector。
    /// 提供 Basic（基础引用）与 Attachments（附件）两个标签页，支持自动分配附件、
    /// 运行时附件状态可视化；同时在 Scene 视图中叠加相机预览与动画播放浮窗。
    /// </summary>
    public class WeaponIdentificationInspector : Editor
    {

        // 标签页名称数组（Basic / Attachments）。
        private string[] tabs = { "Basic", "Attachments" };
        // 当前标签页索引。
        private int currentTab = 0;

        // 缓存的目标 WeaponIdentification 组件。
        private WeaponIdentification wID;
        // 该武器预制体中是否包含 Camera 子物体（用于警告）。
        private bool hasCameraChild = false;
        // 该武器预制体中是否包含 Light 子物体（用于提示）。
        private bool hasLightChild = false;
        private Animator[] animatorChildren;
        // 控制“Animators List”折叠栏是否展开。
        private bool animatorsFoldout = true;
        // 控制“Additional Customization”折叠栏是否展开。
        private bool customizationFoldout = false;
        
        // 用于在 Scene 视图浮窗中渲染武器预览的相机（静态共享）。
        private static Camera previewCamera;
        // 预览相机的渲染目标纹理（静态共享）。
        private static RenderTexture previewTexture;
        // 是否显示相机预览（持久化到 EditorPrefs）。
        private static bool showPreview = true;
        // 是否显示动画列表。
        private static bool showAnims = false;
        // 预览显示状态是否已从 EditorPrefs 初始化。
        private static bool previewInitialized = false;
        private static Vector2 animScrollPos;
        // 是否正在播放预览动画。
        private static bool isPlayingAnim = false;
        // 上一次动画更新的编辑器时间（用于计算帧间隔）。
        private static double lastUpdateTime;
        // 当前正在预览的 Animator。
        private static Animator activeAnimator;
        // 当前正在预览的动画剪辑。
        private static AnimationClip activeClip;
        // 当前动画播放到的时间点（秒）。
        private static float currentClipTime;

        // 控制预览浮窗展开/收起动画的状态对象。
        private static AnimBool showPreviewAnim;
        // 控制动画列表展开/收起动画的状态对象。
        private static AnimBool showAnimsAnim;

        private static void InitializeAnimBools()
        {
            if (showPreviewAnim == null)
            {
                showPreviewAnim = new AnimBool(EditorPrefs.GetBool("Cowsins_ShowWeaponPreview", true));
                showPreviewAnim.valueChanged.AddListener(SceneView.RepaintAll);
                showPreviewAnim.speed = 4f;
            }
            if (showAnimsAnim == null)
            {
                showAnimsAnim = new AnimBool(false);
                showAnimsAnim.valueChanged.AddListener(SceneView.RepaintAll);
                showAnimsAnim.speed = 4f;
            }
        }

        // 静态构造函数：注册 Scene 视图 GUI 回调与编辑器更新回调（先移除再添加，避免重复注册），
        // 并在程序集重载前清理静态资源。
        static WeaponIdentificationInspector()
        {
            SceneView.duringSceneGui -= GlobalOnSceneGUI;
            SceneView.duringSceneGui += GlobalOnSceneGUI;
            EditorApplication.update -= EditorUpdate;
            EditorApplication.update += EditorUpdate;
            AssemblyReloadEvents.beforeAssemblyReload += CleanupStaticResources;
        }

        // 编辑器每帧更新：若正在播放预览动画，则按编辑器时间推进动画采样，
        // 处理循环（循环则取模、非循环则停在末尾），采样到 activeAnimator 上并重绘 Scene 视图。
        private static void EditorUpdate()
        {
            if (isPlayingAnim && activeAnimator != null && activeClip != null)
            {
                double now = EditorApplication.timeSinceStartup;
                float dt = (float)(now - lastUpdateTime);
                lastUpdateTime = now;

                currentClipTime += dt;
                
                if (currentClipTime >= activeClip.length)
                {
                    if (activeClip.isLooping)
                    {
                        currentClipTime %= activeClip.length;
                    }
                    else
                    {
                        currentClipTime = activeClip.length;
                    }
                }

                activeClip.SampleAnimation(activeAnimator.gameObject, currentClipTime);
                SceneView.RepaintAll();
            }
        }

        // 清理静态资源：销毁预览相机与渲染纹理，避免程序集重载/关闭时泄漏。
        private static void CleanupStaticResources()
        {
            if (previewCamera != null)
            {
                DestroyImmediate(previewCamera.gameObject);
            }
            if (previewTexture != null)
            {
                previewTexture.Release();
                DestroyImmediate(previewTexture);
            }
        }

        // 组件启用时缓存目标引用，检测子物体中是否含 Camera/Light，收集所有 Animator，
        // 并从 EditorPrefs 初始化预览显示状态（仅初始化一次）。
        private void OnEnable()
        {
            wID = (WeaponIdentification)target;
            hasCameraChild = wID.GetComponentInChildren<Camera>(true) != null;
            hasLightChild = wID.GetComponentInChildren<Light>(true) != null;
            animatorChildren = wID.GetComponentsInChildren<Animator>(true);
            if (!previewInitialized)
            {
                showPreview = EditorPrefs.GetBool("Cowsins_ShowWeaponPreview", true);
                previewInitialized = true;
            }
        }

        // Scene 视图全局回调：当选中物体的父级含 WeaponIdentification 时，
        // 在其 Scene 视图右下角绘制浮窗——包含“相机预览/动画”工具栏、
        // 武器相机实时预览，以及动画剪辑列表与播放/停止按钮。
        private static void GlobalOnSceneGUI(SceneView sceneView)
        {
            GameObject activeGO = Selection.activeGameObject;
            if (activeGO == null) return;

            WeaponIdentification activeWID = activeGO.GetComponentInParent<WeaponIdentification>();
            if (activeWID == null) return;

            if (!previewInitialized)
            {
                showPreview = EditorPrefs.GetBool("Cowsins_ShowWeaponPreview", true);
                previewInitialized = true;
            }
            InitializeAnimBools();

            showPreviewAnim.target = showPreview;
            showAnimsAnim.target = showAnims;

            Handles.BeginGUI();
            
            float width = 340f;
            float previewHeight = 190f;
            float animListHeight = 160f;
            float margin = 20f;
            
            float totalHeight = 22f; // Toolbar height
            totalHeight += previewHeight * showPreviewAnim.faded;
            totalHeight += animListHeight * showAnimsAnim.faded;
            
            // Anchor from Bottom-Right
            Rect areaRect = new Rect(sceneView.position.width - width - margin, sceneView.position.height - totalHeight - margin - 30f, width, totalHeight);
            
            // Draw window background
            GUI.Box(areaRect, GUIContent.none, "window");
            
            GUILayout.BeginArea(new Rect(areaRect.x + 2, areaRect.y + 2, areaRect.width - 4, areaRect.height - 4));

            // Custom Toolbar
            EditorGUI.BeginChangeCheck();
            GUILayout.BeginHorizontal(EditorStyles.toolbar);
            showPreview = GUILayout.Toggle(showPreview, " Camera Preview", EditorStyles.toolbarButton);
            showAnims = GUILayout.Toggle(showAnims, " Animations", EditorStyles.toolbarButton);
            GUILayout.EndHorizontal();

            if (EditorGUI.EndChangeCheck())
            {
                EditorPrefs.SetBool("Cowsins_ShowWeaponPreview", showPreview);
            }

            if (showPreviewAnim.faded > 0.001f)
            {
                if (showPreview && Event.current.type == EventType.Repaint)
                {
                    if (previewCamera == null)
                    {
                        GameObject camObj = new GameObject("WeaponPreviewCamera");
                        camObj.hideFlags = HideFlags.HideAndDontSave;
                        previewCamera = camObj.AddComponent<Camera>();
                        previewCamera.cameraType = CameraType.Preview;
                        previewCamera.clearFlags = CameraClearFlags.Skybox;
                        previewCamera.fieldOfView = 60f;
                        previewCamera.nearClipPlane = 0.01f;
                        previewCamera.enabled = false;
                        
                        previewCamera.scene = activeWID.gameObject.scene;
                    }

                    if (previewCamera.scene != activeWID.gameObject.scene)
                    {
                        previewCamera.scene = activeWID.gameObject.scene;
                    }

                    if (previewTexture == null)
                    {
                        previewTexture = new RenderTexture((int)width, (int)previewHeight, 24, RenderTextureFormat.ARGB32);
                        previewTexture.antiAliasing = 1;
                    }

                    previewCamera.transform.position = activeWID.transform.position;
                    previewCamera.transform.rotation = activeWID.transform.rotation;
                    
                    previewCamera.targetTexture = previewTexture;
                    previewCamera.Render();
                    previewCamera.targetTexture = null;
                }

                GUI.BeginGroup(new Rect(0, 22, width - 4, previewHeight * showPreviewAnim.faded));
                if (previewTexture != null)
                {
                    GUI.DrawTexture(new Rect(2, 2, width - 8, previewHeight - 8), previewTexture, ScaleMode.StretchToFill, false);
                }
                GUI.EndGroup();
                
                GUILayout.Space(previewHeight * showPreviewAnim.faded);
            }

            if (showAnimsAnim.faded > 0.001f)
            {
                Animator currentAnimator = activeWID.Animator;
                if (currentAnimator == null) 
                {
                    currentAnimator = activeWID.GetComponentInChildren<Animator>(true);
                }

                if (currentAnimator != null)
                {
                    AnimatorController ac = null;
                    if (currentAnimator.runtimeAnimatorController is AnimatorOverrideController aoc)
                        ac = aoc.runtimeAnimatorController as AnimatorController;
                    else
                        ac = currentAnimator.runtimeAnimatorController as AnimatorController;

                    if (ac != null)
                    {
                        GUILayout.Space(4);
                        
                        GUIStyle listItemStyle = new GUIStyle(EditorStyles.helpBox);
                        listItemStyle.padding = new RectOffset(4, 4, 4, 4);

                        animScrollPos = GUILayout.BeginScrollView(animScrollPos, GUILayout.Height(animListHeight - 28f));
                        
                        var addedClips = new HashSet<AnimationClip>();
                        foreach (var clip in ac.animationClips)
                        {
                            if (clip != null && addedClips.Add(clip))
                            {
                                GUILayout.BeginHorizontal(listItemStyle);
                                GUILayout.Label(clip.name, EditorStyles.boldLabel);
                                GUILayout.FlexibleSpace();
                                if (GUILayout.Button("► Play", EditorStyles.miniButtonRight, GUILayout.Width(60)))
                                {
                                    activeClip = clip;
                                    currentClipTime = 0f;
                                    if (currentAnimator != null) currentAnimator.enabled = false;
                                    
                                    activeAnimator = currentAnimator;
                                    lastUpdateTime = EditorApplication.timeSinceStartup;
                                    isPlayingAnim = true;
                                }
                                GUILayout.EndHorizontal();
                            }
                        }
                        GUILayout.EndScrollView();

                        GUILayout.Space(2);
                        GUILayout.BeginHorizontal();
                        GUILayout.FlexibleSpace();
                        GUI.enabled = isPlayingAnim || (currentAnimator != null && !currentAnimator.enabled);
                        if (GUILayout.Button("■ Stop Player", EditorStyles.miniButton, GUILayout.Width(100)))
                        {
                            isPlayingAnim = false;
                            activeClip = null;
                            if (currentAnimator != null)
                            {
                                currentAnimator.Rebind();
                                currentAnimator.Update(0f);
                                currentAnimator.enabled = true;
                            }
                        }
                        GUI.enabled = true;
                        GUILayout.EndHorizontal();
                    }
                    else
                    {
                        EditorGUILayout.HelpBox("Requires an assigned Animator Controller.", MessageType.Warning);
                    }
                }
                else
                {
                    EditorGUILayout.HelpBox("Animator not found on children.", MessageType.Error);
                }
            }
            
            GUILayout.EndArea();
            Handles.EndGUI();
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            Texture2D myTexture = Resources.Load<Texture2D>("CustomEditor/weaponIdentification_CustomEditor") as Texture2D;
            GUILayout.Label(myTexture);

            if (hasCameraChild)
            {
                EditorGUILayout.HelpBox("A Camera has been found in this Weapon Prefab. This is not allowed.", MessageType.Error);
                GUILayout.Space(10);
            }
            if (hasLightChild)
            {
                EditorGUILayout.HelpBox("A Light Source has been found in this Weapon Prefab. Ignore this message if this is intentional.", MessageType.Warning);
                GUILayout.Space(10);
            }
            if (animatorChildren != null && animatorChildren.Length > 0)
            {
                EditorGUILayout.HelpBox($"Found {animatorChildren.Length} Animator(s) in children.", MessageType.Info);
                GUILayout.Space(10);
            }

            currentTab = GUILayout.Toolbar(currentTab, tabs);

            if (currentTab >= 0 || currentTab < tabs.Length)
            {
                switch (tabs[currentTab])
                {
                    case "Basic":
                        EditorGUILayout.Space(20f);
                        EditorGUILayout.PropertyField(serializedObject.FindProperty("weapon"));

                        EditorGUILayout.PropertyField(serializedObject.FindProperty("FirePoint"));
                        EditorGUILayout.PropertyField(serializedObject.FindProperty("aimPoint"));
                        EditorGUILayout.Space(10f);
                        EditorGUILayout.LabelField("You can leave �headBone� unassigned if your camera does not move during your Weapon Animations.", EditorStyles.helpBox);
                        EditorGUILayout.PropertyField(serializedObject.FindProperty("headBone"));
                        if (animatorChildren != null && animatorChildren.Length > 0)
                        {
                            animatorsFoldout = EditorGUILayout.Foldout(animatorsFoldout, "Animators List", true);

                            if (animatorsFoldout)
                            {
                                EditorGUI.indentLevel++;

                                foreach (Animator animator in animatorChildren)
                                {
                                    EditorGUILayout.BeginHorizontal();

                                    EditorGUI.BeginDisabledGroup(true);
                                    // non-selectable
                                    EditorGUILayout.ObjectField(animator.gameObject.name, animator, typeof(Animator), true);
                                    EditorGUI.EndDisabledGroup();

                                    if (GUILayout.Button("See", GUILayout.Width(40)))
                                    {
                                        Selection.activeObject = animator.gameObject;
                                        EditorGUIUtility.PingObject(animator.gameObject);
                                    }

                                    EditorGUILayout.EndHorizontal();
                                }

                                EditorGUI.indentLevel--;
                            }
                        }

                        EditorGUILayout.Space(10f);
                        customizationFoldout = EditorGUILayout.Foldout(customizationFoldout, "Additional Customization", true);

                        if (customizationFoldout)
                        {
                            EditorGUI.indentLevel++;
                            EditorGUILayout.HelpBox("Shell Eject Point is optional. If left empty, Fire Point will be used.", MessageType.Info);
                            EditorGUILayout.PropertyField(serializedObject.FindProperty("shellEjectPoint"));
                            EditorGUI.indentLevel--;
                        }
                        break;
                    case "Attachments":
                        EditorGUILayout.Space(5f);

                        GUILayout.BeginHorizontal();
                        CowsinsEditorWindowUtilities.DrawLinkCard(Resources.Load<Texture2D>("CustomEditor/CowsinsManager/documentationIcon"), "Documentation", "https://cowsinss-organization.gitbook.io/fps-engine-documentation/how-to-use/working-with-attachments", .77f, .4f);
                        GUILayout.FlexibleSpace();
                        CowsinsEditorWindowUtilities.DrawLinkCard(Resources.Load<Texture2D>("CustomEditor/CowsinsManager/tutorialsIcon"), "Tutorial", "https://www.cowsins.com/videos/BuJ3d4ssMdQ", .77f, .4f);
                        GUILayout.FlexibleSpace();
                        CowsinsEditorWindowUtilities.DrawLinkCard(Resources.Load<Texture2D>("CustomEditor/CowsinsManager/supportIcon"), "Support", "https://discord.gg/759gSeTT9m", .77f, .4f);
                        GUILayout.Space(10);
                        GUILayout.EndHorizontal();

                        EditorGUILayout.Space(20f);
                        EditorGUILayout.HelpBox("Original / Default Attachments: Iron Sights, Original Magazines, etc...", MessageType.Info);
                        EditorGUILayout.Space(5f);
                        EditorGUILayout.PropertyField(serializedObject.FindProperty("defaultAttachments").FindPropertyRelative("defaultAttachmentsList"));

                        EditorGUILayout.Space(10f);
                        EditorGUILayout.HelpBox("Define All Available Attachments for this Weapon, including Default Attachments.", MessageType.Info);
                        EditorGUILayout.Space(5f);
                        EditorGUILayout.PropertyField(serializedObject.FindProperty("compatibleAttachments").FindPropertyRelative("compatibleAttachmentsList"));

                        EditorGUILayout.Space(10f);
                        AutomaticAttachmentButton();

                        // Attachment State Visualization during the game
                        if (Application.isPlaying)
                        {
                            EditorGUILayout.Space(20f);
                            DrawRuntimeAttachmentState();
                        }

                        break;
                }
            }

            serializedObject.ApplyModifiedProperties();
        }

        private void AutomaticAttachmentButton()
        {
            if (GUILayout.Button("Automatically Assign Attachments", GUILayout.Height(35)))
            {
                var attachments = wID.GetComponentsInChildren<Attachment>(true);

                // Create a Temporary Dictionary for the found attachments and map them to their corresponding Attachment Type
                var groupedAttachments = new Dictionary<AttachmentType, List<Attachment>>();

                foreach (var attachment in attachments)
                {
                    var atcId = attachment.attachmentIdentifier;
                    if (atcId == null)
                    {
                        Debug.LogError($"<color=red>[COWSINS]</color> Attachment Identifier is null in {attachment}. Please, assign an attachment Identifier.", attachment);
                        continue;
                    }
                    var type = attachment.attachmentIdentifier.attachmentType;

                    if (!groupedAttachments.ContainsKey(type))
                        groupedAttachments[type] = new List<Attachment>();

                    groupedAttachments[type].Add(attachment);
                }

                // Gather Compatible Attachments 
                SerializedProperty compatibleListProp = serializedObject.FindProperty("compatibleAttachments")
                    .FindPropertyRelative("compatibleAttachmentsList");

                // Clear Compatible Attachments
                compatibleListProp.ClearArray();

                // Repopulate Compatible Attachments based on the Temporary Dictionary we just created
                int index = 0;
                foreach (var kvp in groupedAttachments)
                {
                    compatibleListProp.InsertArrayElementAtIndex(index);
                    SerializedProperty entryProp = compatibleListProp.GetArrayElementAtIndex(index);

                    entryProp.FindPropertyRelative("type").enumValueIndex = (int)kvp.Key;

                    var attachmentsArray = entryProp.FindPropertyRelative("attachments");
                    attachmentsArray.ClearArray();

                    for (int i = 0; i < kvp.Value.Count; i++)
                    {
                        attachmentsArray.InsertArrayElementAtIndex(i);
                        attachmentsArray.GetArrayElementAtIndex(i).objectReferenceValue = kvp.Value[i];
                    }

                    index++;
                }

                // Save the Changes
                serializedObject.ApplyModifiedProperties();
                EditorUtility.SetDirty(wID);

            }
        }

        // Shows current, default, and compatible attachments during runtime
        private void DrawRuntimeAttachmentState()
        {
            if (wID.AttachmentState == null) return;

            var state = wID.AttachmentState;

            // Calculate summary
            int totalSlots = System.Enum.GetValues(typeof(AttachmentType)).Length;
            int equippedCount = state.GetCurrentCount();
            int defaultCount = 0;
            int customCount = 0;

            foreach (AttachmentType type in System.Enum.GetValues(typeof(AttachmentType)))
            {
                var attachmentState = state.GetState(type);
                if (attachmentState == AttachmentState.Default) defaultCount++;
                else if (attachmentState == AttachmentState.Custom) customCount++;
            }

            int emptyCount = totalSlots - equippedCount;
            string summaryText = $"Attachments: {equippedCount}/{totalSlots} equipped ({defaultCount} default, {customCount} custom, {emptyCount} empty)";

            EditorGUILayout.LabelField("Runtime Attachment State", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(summaryText, MessageType.Info);

            EditorGUILayout.Space(5f);

            // Draw each attachment type
            foreach (AttachmentType type in System.Enum.GetValues(typeof(AttachmentType)))
            {
                DrawAttachmentTypeState(state, type);
            }

            EditorGUILayout.Space(10f);

            Repaint();
        }

        private void DrawAttachmentTypeState(AttachmentStateManager state, AttachmentType type)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            var attachmentState = state.GetState(type);
            var current = state.GetCurrent(type);
            var compatibleCount = state.GetCompatibleCount(type);

            Color stateColor = attachmentState switch
            {
                AttachmentState.None => Color.gray,
                AttachmentState.Default => new Color(0.5f, 0.8f, 1f),
                AttachmentState.Custom => new Color(0.5f, 1f, 0.5f),
                _ => Color.white
            };

            var originalColor = GUI.color;
            GUI.color = stateColor;

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"  {type}", EditorStyles.boldLabel, GUILayout.Width(100));
            EditorGUILayout.LabelField($"{attachmentState}", GUILayout.Width(70));

            GUI.color = originalColor;

            // Show current attachment
            if (current != null)
            {
                EditorGUI.BeginDisabledGroup(true);
                EditorGUILayout.ObjectField(current, typeof(Attachment), true);
                EditorGUI.EndDisabledGroup();
            }
            else
            {
                EditorGUILayout.LabelField($"Empty ({compatibleCount} compatible)", EditorStyles.miniLabel);
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }

    }
}
#endif