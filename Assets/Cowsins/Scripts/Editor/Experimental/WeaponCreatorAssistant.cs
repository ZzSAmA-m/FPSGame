// =====================================================================
// WeaponCreatorAssistant.cs：武器创建助手（实验性功能）。
// 通过 GUI 引导用户输入武器名称、选择预设/模型/动画，最终在项目中创建
// Weapon_SO 资源与对应的 WeaponObject 预制体，并自动配置动画控制器与武器层级。
// 仅在编辑器环境（UNITY_EDITOR）下编译。
// =====================================================================
#if UNITY_EDITOR
using System;
using UnityEngine;
using UnityEditor;
using UnityEditor.Presets;
using UnityEditor.Animations;
namespace cowsins
{
    /// <summary>
    /// 武器创建助手：以 ScriptableObject 形式提供编辑器 GUI，引导并执行新武器的创建流程。
    /// </summary>
    public class WeaponCreatorAssistant : ScriptableObject
    {
        // 新武器的名称（默认为 "NewWeapon"）。
        private string weaponName = "NewWeapon";
        // 标题栏的背景颜色。
        private Color titleBackgroundColor = new Color(0.2f, 0.2f, 0.2f);
        // 可选的武器预设，用于将配置套用到新建的 Weapon_SO。
        private Preset weaponPreset;
        // 武器的 3D 视模型（ViewModel）。
        private GameObject weaponModel;

        // 各状态对应的动画片段引用（待分配进 AnimatorController）。
        private AnimationClip idleClip, shootClip, reloadClip, unholsterClip, holsterClip, emptyReloadClip, walkClip, runClip, startInspectClip, loopInspectClip, endInspectClip;

        // Used to control Foldout visibility
        private bool showName = true;
        private bool showPreset = false;
        private bool showModel = false;
        private bool showAnims = false;

        // 滚动视图的滚动位置。
        private Vector2 scrollPos;

        [MenuItem("Cowsins/Create/Weapon ( Weapon Creation Assistant )")]
        /// <summary>
        /// 菜单入口：打开武器创建助手所在的管理器武器标签页。
        /// </summary>
        public static void DisplayConfirmationDialog()
        {
            CustomTabEditorWindow.OpenWeaponsTab();
        }

        /// <summary>
        /// 绘制武器创建助手界面：名称、预设、模型、动画四个折叠面板与“创建”按钮。
        /// </summary>
        public void OnGUI()
        {
            DrawTitleBackground("Create New Weapon | FPS Engine by Cowsins");
            GUILayout.Space(10);

            EditorGUILayout.HelpBox("This feature is experimental and still a work in progress. We welcome your feedback.", MessageType.Warning);

            scrollPos = EditorGUILayout.BeginScrollView(scrollPos);
            GUILayout.BeginHorizontal();
            GUILayout.Space(10);

            GUILayout.BeginVertical();
            GUILayout.Space(10);
            // FPS Engine Logo
            GUILayout.Label(AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Cowsins/UI/Logo/FPS_Engine_Logo_White.png"), GUILayout.Width(375), GUILayout.Height(30));
            GUILayout.Space(5);

            // Weapon Name Foldout
            EditorGUILayout.BeginVertical(GUI.skin.GetStyle("HelpBox"));
            {
                showName = EditorGUILayout.Foldout(showName, "Name", true);
                if (showName)
                {
                    EditorGUI.indentLevel++;
                    EditorGUILayout.LabelField("Enter a name for the new weapon:");
                    weaponName = EditorGUILayout.TextField(weaponName);
                    EditorGUI.indentLevel--;
                }
            }
            EditorGUILayout.EndVertical();
            GUILayout.Space(10);

            // Weapon Preset Foldout
            EditorGUILayout.BeginVertical(GUI.skin.GetStyle("HelpBox"));
            {
                showPreset = EditorGUILayout.Foldout(showPreset, "Preset", true);
                if (showPreset)
                {
                    EditorGUI.indentLevel++;
                    EditorGUILayout.LabelField("(Recommended) ");
                    weaponPreset = (Preset)EditorGUILayout.ObjectField("Select Preset:", weaponPreset, typeof(Preset), false);

                    if (weaponPreset && !(weaponPreset.GetTargetTypeName() is "Weapon_SO"))
                    {
                        EditorGUILayout.HelpBox("Weapon Preset is not a valid type. Please ensure Weapon Preset is of type Weapon_SO", MessageType.Error);
                    }
                    EditorGUI.indentLevel--;
                }
            }
            EditorGUILayout.EndVertical();
            GUILayout.Space(10);

            // Weapon Model Foldout
            EditorGUILayout.BeginVertical(GUI.skin.GetStyle("HelpBox"));
            {
                showModel = EditorGUILayout.Foldout(showModel, "Model", true);
                if (showModel)
                {
                    EditorGUI.indentLevel++;
                    weaponModel = (GameObject)EditorGUILayout.ObjectField("Select Weapon Model:", weaponModel, typeof(GameObject), false);
                    if (weaponModel == null)
                    {
                        EditorGUILayout.HelpBox("Weapon Model is null. Please assign a 3D Viewmodel Weapon model to proceed.", MessageType.Error);
                    }
                    else
                    {
                        // Model Preview 
                        GUILayout.BeginHorizontal();
                        GUILayout.FlexibleSpace();
                        Rect previewRect = GUILayoutUtility.GetRect(50, 50, GUILayout.Width(150), GUILayout.Height(150));
                        if (AssetPreview.GetAssetPreview(weaponModel))
                            EditorGUI.DrawPreviewTexture(previewRect, AssetPreview.GetAssetPreview(weaponModel));
                        else
                            EditorGUILayout.HelpBox("Preview Not Available", MessageType.Warning);
                        GUILayout.FlexibleSpace();
                        GUILayout.EndHorizontal();
                    }
                    EditorGUI.indentLevel--;
                }
            }
            EditorGUILayout.EndVertical();
            GUILayout.Space(10);

            // Weapon Animations Foldout
            EditorGUILayout.BeginVertical(GUI.skin.GetStyle("HelpBox"));
            {
                showAnims = EditorGUILayout.Foldout(showAnims, "Animations", true);
                if (showAnims)
                {
                    EditorGUI.indentLevel++;

                    idleClip = (AnimationClip)EditorGUILayout.ObjectField("Idle:", idleClip, typeof(AnimationClip), false);
                    shootClip = (AnimationClip)EditorGUILayout.ObjectField("Shoot:", shootClip, typeof(AnimationClip), false);
                    reloadClip = (AnimationClip)EditorGUILayout.ObjectField("Reload:", reloadClip, typeof(AnimationClip), false);
                    emptyReloadClip = (AnimationClip)EditorGUILayout.ObjectField("Empty Reload:", emptyReloadClip, typeof(AnimationClip), false);
                    holsterClip = (AnimationClip)EditorGUILayout.ObjectField("Holster:", holsterClip, typeof(AnimationClip), false);
                    unholsterClip = (AnimationClip)EditorGUILayout.ObjectField("Unholster:", unholsterClip, typeof(AnimationClip), false);
                    walkClip = (AnimationClip)EditorGUILayout.ObjectField("Walk:", walkClip, typeof(AnimationClip), false);
                    runClip = (AnimationClip)EditorGUILayout.ObjectField("Run:", runClip, typeof(AnimationClip), false);
                    startInspectClip = (AnimationClip)EditorGUILayout.ObjectField("Start Inspect:", startInspectClip, typeof(AnimationClip), false);
                    loopInspectClip = (AnimationClip)EditorGUILayout.ObjectField("Loop Inspect:", loopInspectClip, typeof(AnimationClip), false);
                    endInspectClip = (AnimationClip)EditorGUILayout.ObjectField("End Inspect:", endInspectClip, typeof(AnimationClip), false);

                    EditorGUI.indentLevel--;
                }
            }
            EditorGUILayout.EndVertical();
            GUILayout.Space(10);
            // 点击“Create”后先校验预设类型与模型引用，再弹出确认对话框。
            if (GUILayout.Button("Create"))
            {
                if (weaponPreset && !(weaponPreset.GetTargetTypeName() is "Weapon_SO"))
                {
                    EditorUtility.DisplayDialog("Weapon Creation Failed",
                                                              "Weapon Preset is not a valid type. Please ensure Weapon Preset is of type Weapon_SO",
                                                              "Ok");
                    return;
                }
                if (weaponModel == null)
                {
                    EditorUtility.DisplayDialog("Weapon Model is null",
                                                             "Weapon Model Reference is null. Please attach your 3D Viewmodel Weapon model. \n\n"
                                                             , "OK");
                    return;
                }

                // Display confirmation dialog before creating the weapon
                bool confirmed = EditorUtility.DisplayDialog("Create Weapon",
                                                              "Are you sure you want to create a new weapon with the name \"" + weaponName + "\"?\n\n" +
                                                              "This action will create a new ScriptableObject and a corresponding WeaponObject prefab in the project.\n\n" +
                                                              "This WeaponObject will be assigned to your new ScriptableObject automatically.\n\n" +
                                                              "This ScriptableObject will be automatically assigned to the weapon slot in the WeaponIdentification component of the WeaponObject prefab.",
                                                              "Yes", "No");
                if (confirmed)
                {
                    CreateWeapon();
                    CustomTabEditorWindow.CloseWindow();
                }

            }

            GUILayout.Space(10);
            GUIStyle hyperlinkStyle = new GUIStyle(GUI.skin.label);
            hyperlinkStyle.normal.textColor = new Color(0.1f, 0.5f, 1.0f);
            hyperlinkStyle.hover.textColor = new Color(0.1f, 0.7f, 1.0f);
            if (GUILayout.Button("Need Help? Join our Discord server", hyperlinkStyle))
            {
                Application.OpenURL("https://discord.gg/759gSeTT9m");
            }
            GUILayout.EndVertical();

            GUILayout.Space(10);
            GUILayout.EndHorizontal();

            GUILayout.Space(10);

            EditorGUILayout.EndScrollView();
        }

        // 执行武器创建流程：创建文件夹、生成 Weapon_SO、复制模板预制体并配置动画与层级。
        private void CreateWeapon()
        {

            string folderPath = "Assets/NewWeapons";

            // Check if the folder exists, create it if it doesnt
            if (!AssetDatabase.IsValidFolder(folderPath))
            {
                AssetDatabase.CreateFolder("Assets", "NewWeapons");
                AssetDatabase.Refresh();
            }

            folderPath = "Assets/NewWeapons/" + weaponName;

            // Check if the folder exists, create it if it doesnt, taking into account the name of the weapon
            if (!AssetDatabase.IsValidFolder(folderPath))
            {
                AssetDatabase.CreateFolder("Assets/NewWeapons", weaponName);
                AssetDatabase.Refresh();
            }
            else
            {
                EditorUtility.DisplayDialog("Folder Already Exists",
                                                "A folder with the name \"" + weaponName + "\" already exists. Please choose a different name.",
                                                "OK");
                return;
            }
            // CREATING A NEW SCRIPTABLE OBJECT INSTANCE
            Weapon_SO newWeapon = ScriptableObject.CreateInstance<Weapon_SO>();

            string assetPath = AssetDatabase.GenerateUniqueAssetPath(folderPath + "/" + weaponName + ".asset");

            AssetDatabase.CreateAsset(newWeapon, assetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            EditorUtility.FocusProjectWindow();
            Selection.activeObject = newWeapon;

            // CREATING A NEW WEAPON PREFAB OBJECT
            string newPath = folderPath + "/" + weaponName + "_WeaponObject.prefab";

            GameObject originalPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Cowsins/Prefabs/Weapons/CowsinsBlankWeaponTemplate.prefab");

            if (originalPrefab == null)
            {
                Debug.LogError("Original prefab not found at specified path.");
                EditorUtility.DisplayDialog("Weapon Creation Failed.", "CowsinsBlankWeaponTemplate not found at path: Assets/Cowsins/Prefabs/Weapons/CowsinsBlankWeaponTemplate.prefab", "OK");
                return;
            }
            GameObject duplicatedPrefab = Instantiate(originalPrefab);
            Transform parent = duplicatedPrefab.transform.GetChild(0);

            if (parent == null)
            {
                Debug.LogError("Original prefab has been modified. `Weapon` inside CowsinsBlankWeaponTemplate could not be found.");
                EditorUtility.DisplayDialog("Weapon Creation Failed.", "CowsinsBlankWeaponTemplate has been modified.", "OK");
                return;
            }
            GameObject instantiatedWeaponModel = Instantiate(weaponModel, duplicatedPrefab.transform);
            instantiatedWeaponModel.transform.SetParent(parent);

            // 取得名为 "Weapons" 的层索引，用于设置武器模型的层级。
            int weaponsLayer = LayerMask.NameToLayer("Weapons");
            SetLayerRecursively(instantiatedWeaponModel, weaponsLayer);

            // Remove Animator from the root of the prefab
            Animator rootAnimator = duplicatedPrefab.GetComponent<Animator>();
            if (rootAnimator != null)
            {
                DestroyImmediate(rootAnimator);
            }

            // Add Animator to the instantiatedWeaponModel
            Animator newAnimator = instantiatedWeaponModel.GetComponent<Animator>();
            if (newAnimator == null)
                newAnimator = instantiatedWeaponModel.AddComponent<Animator>();

            string originalControllerPath = "Assets/Cowsins/Animations/Weapons/BlankWeaponAnimatorTemplate.controller";


            AnimatorController originalController = AssetDatabase.LoadAssetAtPath<AnimatorController>(originalControllerPath);

            if (originalController != null)
            {
                string controllerPath = folderPath + "/" + weaponName + "_AnimatorController.controller";
                AnimatorController newController = DuplicateAnimatorController(originalControllerPath, controllerPath);

                AssetDatabase.SaveAssets();

                // Assign Animator Controller to the new Animator
                newAnimator.runtimeAnimatorController = newController;

                AssignAnimationClipsToStates(newController);
            }
            else
            {
                Debug.LogError("Original Animator Controller not found at specified path.");
                EditorUtility.DisplayDialog("Weapon Creation Failed.", "BlankWeaponAnimatorTemplate.controller not found at path: Assets/Cowsins/Animations/Weapons/BlankWeaponAnimatorTemplate.controller", "OK");
                return;
            }


            PrefabUtility.SaveAsPrefabAsset(duplicatedPrefab, newPath);
            DestroyImmediate(duplicatedPrefab);
            AssetDatabase.Refresh();

            WeaponIdentification weaponIdentification = AssetDatabase.LoadAssetAtPath<GameObject>(newPath).GetComponent<WeaponIdentification>();
            if (weaponIdentification == null)
            {
                Debug.LogError("WeaponIdentification component not found on the duplicated prefab.");
                EditorUtility.DisplayDialog("Weapon Creation Failed.", "WeaponIdentification component not found on the duplicated prefab.", "OK");
                return;
            }

            // Gather weapon
            weaponIdentification.weapon = newWeapon;

            if (weaponPreset)
                CowsinsUtilities.ApplyPreset(weaponPreset, newWeapon);

            // Assign the WeaponIdentification component to the weaponObject property of the newWeapon
            newWeapon.weaponObject = weaponIdentification;

            // Mark the Weapon_SO asset as dirty and save it
            EditorUtility.SetDirty(newWeapon);
            EditorUtility.SetDirty(weaponIdentification);
            AssetDatabase.SaveAssets();

            EditorUtility.DisplayDialog("Weapon created.", weaponName + " successfully created at path: " + newPath, "OK");
        }

        /// <summary>
        /// 将 AnimatorController 复制到目标路径，并返回加载后的副本。
        /// </summary>
        public AnimatorController DuplicateAnimatorController(string originalPath, string targetPath)
        {
            // Check if the original file exists
            if (!AssetDatabase.CopyAsset(originalPath, targetPath))
            {
                Debug.LogError($"Failed to copy AnimatorController from {originalPath} to {targetPath}");
                return null;
            }

            // Ensure the new asset is properly imported
            AssetDatabase.Refresh();

            // Load and return the duplicated controller
            AnimatorController duplicatedController = AssetDatabase.LoadAssetAtPath<AnimatorController>(targetPath);
            if (duplicatedController != null)
            {
                return duplicatedController;
            }
            else
            {
                Debug.LogError($"Could not load the duplicated AnimatorController at {targetPath}");
                return null;
            }
        }


        // 将用户指定的动画片段分配到 AnimatorController 的各状态（含 Locomotion 混合树）。
        private void AssignAnimationClipsToStates(AnimatorController controller)
        {
            // 局部函数：按名称（忽略大小写）在状态数组中查找 AnimatorState。
            AnimatorState FindStateByName(ChildAnimatorState[] states, string stateName)
            {
                foreach (var state in states)
                {
                    if (state.state.name.Equals(stateName, StringComparison.OrdinalIgnoreCase))
                    {
                        return state.state;
                    }
                }
                return null;
            }

            var states = controller.layers[0].stateMachine.states;

            // Finds the animation states and applies the clips
            AnimatorState locomotionState = FindStateByName(states, "Locomotion");
            AnimatorState shootState = FindStateByName(states, "shooting");
            AnimatorState reloadState = FindStateByName(states, "reloading");
            AnimatorState unholsterState = FindStateByName(states, "Unholster");
            AnimatorState holsterState = FindStateByName(states, "holster");
            AnimatorState emptyReloadState = FindStateByName(states, "emptyReloading");
            AnimatorState startInspectState = FindStateByName(states, "StartInspection");
            AnimatorState loopInspectState = FindStateByName(states, "InspectLoop");
            AnimatorState endInspectState = FindStateByName(states, "StopInspection");

            // Handle Locomotion Blend Tree
            if (locomotionState != null && locomotionState.motion is BlendTree)
            {
                BlendTree blendTree = (BlendTree)locomotionState.motion;
                var children = blendTree.children;

                // Clear existing children to avoid duplicates and ensure correct order
                for (int i = children.Length - 1; i >= 0; i--)
                {
                    blendTree.RemoveChild(i);
                }

                // Add clips to Blend Tree with thresholds: Idle (0), Walk (0.5), Run (1)
                if (idleClip != null) blendTree.AddChild(idleClip, 0f);
                if (walkClip != null) blendTree.AddChild(walkClip, 0.5f);
                if (runClip != null) blendTree.AddChild(runClip, 1f);
            }

            // When only one of reload/empty reload clips is assigned, use it for both states.
            if (reloadClip == null && emptyReloadClip != null) reloadClip = emptyReloadClip;
            else if (emptyReloadClip == null && reloadClip != null) emptyReloadClip = reloadClip;

            if (shootState != null && shootClip != null) shootState.motion = shootClip;
            if (reloadState != null && reloadClip != null) reloadState.motion = reloadClip;
            if (unholsterState != null && unholsterClip != null) unholsterState.motion = unholsterClip;
            if (holsterState != null && holsterClip != null) holsterState.motion = holsterClip;
            if (emptyReloadState != null && emptyReloadClip != null) emptyReloadState.motion = emptyReloadClip;
            if (startInspectState != null && startInspectClip != null) startInspectState.motion = startInspectClip;
            if (loopInspectState != null && loopInspectClip != null) loopInspectState.motion = loopInspectClip;
            if (endInspectState != null && endInspectClip != null) endInspectState.motion = endInspectClip;
        }

        // 绘制带深色背景的标题栏。
        private void DrawTitleBackground(string title)
        {
            Rect rect = GUILayoutUtility.GetRect(new GUIContent(title), EditorStyles.boldLabel);
            EditorGUI.DrawRect(new Rect(0, rect.y, EditorGUIUtility.currentViewWidth, rect.height), titleBackgroundColor);
            GUI.Label(rect, title, EditorStyles.boldLabel);
        }

        // Loops through the children of obj to assign the selected layer
        // 递归地将 obj 及其所有子物体设置为 newLayer 层。
        private void SetLayerRecursively(GameObject obj, int newLayer)
        {
            if (null == obj) return;

            obj.layer = newLayer;

            foreach (Transform child in obj.transform)
            {
                if (null == child) continue;
                SetLayerRecursively(child.gameObject, newLayer);
            }
        }
    }

}

#endif
