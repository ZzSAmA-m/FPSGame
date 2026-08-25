// ===================================================================
// WeaponControllerEditor.cs —— WeaponController 的自定义 Inspector。
// 仅在编辑器（UNITY_EDITOR）下编译。把武器控制器的设置（settings）按标签页
// （Inventory / References / Variables / Secondary Attack / Effects / Events）分组展示，
// 提供更直观的编辑体验，并根据开关状态（如 autoReload、canMelee）动态折叠/展开关联字段。
// ===================================================================
#if UNITY_EDITOR
/// <summary>
/// This script belongs to cowsins� as a part of the cowsins� FPS Engine. All rights reserved. 
/// </summary>
using UnityEditor;
using UnityEngine;

namespace cowsins
{
    [System.Serializable]
    [CustomEditor(typeof(WeaponController))]
    /// <summary>
    /// WeaponController 的自定义 Inspector 绘制器。
    /// 通过标签页（Toolbar）把 WeaponControllerSettings 的各项配置分区显示，
    /// 并根据某些开关（如 autoReload、canMelee）动态显示/隐藏关联字段。
    /// </summary>
    public class WeaponControllerEditor : Editor
    {
        // 标签页名称数组，对应 Toolbar 中的各个分区。
        private string[] tabs = { "Inventory", "References", "Variables", "Secondary Attack", "Effects", "Events" };
        // 当前选中的标签页索引。
        private int currentTab = 0;

        /// <summary>
        /// Inspector 主绘制入口。流程：
        /// 1) 更新序列化对象并取得目标 WeaponController；
        /// 2) 显示自定义横幅图片与标签页工具栏；
        /// 3) 根据当前标签页绘制对应配置分区；
        /// 4) 应用修改后的属性。
        /// </summary>
        override public void OnInspectorGUI()
        {
            // 同步序列化对象，确保读取到最新的 Inspector 修改。
            serializedObject.Update();
            // 取得当前被检视的 WeaponController 组件。
            WeaponController myScript = target as WeaponController;


            // 加载并显示自定义横幅图片。
            Texture2D myTexture = Resources.Load<Texture2D>("CustomEditor/weaponController_CustomEditor") as Texture2D;
            GUILayout.Label(myTexture);

            EditorGUILayout.BeginVertical();
            // 显示标签页工具栏，返回当前选中的标签索引。
            currentTab = GUILayout.Toolbar(currentTab, tabs);
            EditorGUILayout.Space(10f);
            EditorGUILayout.EndVertical();

            if (currentTab >= 0 || currentTab < tabs.Length)
            {
                // 获取 settings 序列化属性，供后续各分区绘制使用。
                SerializedProperty settings = serializedObject.FindProperty("settings");

                switch (tabs[currentTab])
                {
                    case "Inventory":
                        EditorGUILayout.LabelField("INVENTORY", EditorStyles.boldLabel);

                        // 背包容量（最大可携带武器数量）字段。
                        EditorGUILayout.PropertyField(settings.FindPropertyRelative("inventorySize"));
                        // 读取背包容量数值，用于下面裁剪初始武器数组。
                        int inventorySize = settings.FindPropertyRelative("inventorySize").intValue;

                        EditorGUILayout.Space(5);
                        EditorGUILayout.LabelField("Select the weapons you want to spawn with", EditorStyles.helpBox);
                        EditorGUILayout.Space(5);

                        // 初始武器数组。
                        SerializedProperty initialWeapons = settings.FindPropertyRelative("initialWeapons");

                        // 若初始武器数量超过背包容量，则裁剪到背包容量以内。
                        if (initialWeapons.arraySize > inventorySize)
                        {
                            initialWeapons.arraySize = inventorySize;
                        }

                        // 显示初始武器数组（可展开编辑）。
                        EditorGUILayout.PropertyField(initialWeapons, true);

                        // 当初始武器数组已达容量上限时，提示无法继续添加。
                        if (initialWeapons.arraySize >= inventorySize)
                        {
                            EditorGUILayout.LabelField(
                                "You can�t add more initial weapons. This array can�t be bigger than the inventory size",
                                EditorStyles.helpBox
                            );
                        }
                        // 是否允许使用鼠标滚轮切换武器。
                        EditorGUILayout.PropertyField(settings.FindPropertyRelative("allowMouseWheelWeaponSwitch"));
                        // 是否允许使用数字键切换武器。
                        EditorGUILayout.PropertyField(settings.FindPropertyRelative("allowNumberKeyWeaponSwitch"));
                        break;
                    case "References":
                        EditorGUILayout.LabelField("REFERENCES", EditorStyles.boldLabel);
                        //var weaponProperty = serializedObject.FindProperty("weapon");
                        //EditorGUILayout.PropertyField(weaponProperty);
                        // 获取武器控制器引用属性（此处仅声明，未参与绘制）。
                        SerializedProperty weaponControllerReferences = serializedObject.FindProperty("weaponControllerReferences");
                        // 主相机引用。
                        EditorGUILayout.PropertyField(settings.FindPropertyRelative("mainCamera"));
                        // 相机轴心引用。
                        EditorGUILayout.PropertyField(settings.FindPropertyRelative("cameraPivot"));
                        // 武器挂点引用。
                        EditorGUILayout.PropertyField(settings.FindPropertyRelative("weaponHolder"));
                        break;
                    case "Variables":
         
                        EditorGUILayout.Space(10f);
                        EditorGUILayout.LabelField("RELOAD", EditorStyles.boldLabel);
                        EditorGUILayout.Space(2f);
                        // 自动换弹开关属性。
                        SerializedProperty autoReloadProp = settings.FindPropertyRelative("autoReload");
                        // 读取自动换弹开关值。
                        bool autoReload = autoReloadProp.boolValue;

                        EditorGUILayout.PropertyField(autoReloadProp);

                        // 开启自动换弹时，额外显示自动换弹延迟字段。
                        if(autoReload)
                        {
                            EditorGUI.indentLevel++;
                            EditorGUILayout.PropertyField(settings.FindPropertyRelative("autoReloadDelay"));
                            EditorGUI.indentLevel--;
                        }
                        // 是否允许在收枪过程中换弹。
                        EditorGUILayout.PropertyField(settings.FindPropertyRelative("allowReloadWhileUnholstering"));

                        EditorGUILayout.Space(5f);
                        EditorGUILayout.LabelField("HOLSTER", EditorStyles.boldLabel);
                        EditorGUILayout.Space(2f);
                        // 收枪行为。
                        EditorGUILayout.PropertyField(settings.FindPropertyRelative("holsterBehaviour"));
                        EditorGUILayout.Space(5f);
                        EditorGUILayout.LabelField("AIM SETTINGS", EditorStyles.boldLabel);
                        EditorGUILayout.Space(2f);
                        // 瞄准模式（按住式/切换式）。
                        EditorGUILayout.PropertyField(settings.FindPropertyRelative("alternateAiming"));

                        EditorGUILayout.Space(5f);
                        EditorGUILayout.LabelField("HIT SETTINGS", EditorStyles.boldLabel);
                        EditorGUILayout.Space(2f);
                        // 可命中图层。
                        EditorGUILayout.PropertyField(settings.FindPropertyRelative("hitLayer"));
                        break;
                    case "Secondary Attack":
                    
                        // 近战开关属性。
                        SerializedProperty canMeleeProp = settings.FindPropertyRelative("canMelee");
                        // 读取近战开关值。
                        bool canMelee = canMeleeProp.boolValue;
                        EditorGUILayout.PropertyField(canMeleeProp);
                        // 启用近战时，展开近战相关字段。
                        if (canMelee)
                        {
                            EditorGUI.indentLevel++;
                            // 近战物体。
                            EditorGUILayout.PropertyField(settings.FindPropertyRelative("meleeObject"));
                            // 若已指定近战物体，则进一步显示头部骨骼字段。
                            if(myScript.settings.meleeObject)
                            {
                                EditorGUI.indentLevel++;
                                // 提示：若相机不随近战动画移动，可留空头部骨骼。
                                EditorGUILayout.LabelField("You can leave �meleeHeadBone� unassigned if your camera does not move during your Melee Animations.", EditorStyles.helpBox);
                                // 近战头部骨骼。
                                EditorGUILayout.PropertyField(settings.FindPropertyRelative("meleeHeadBone"));
                                EditorGUI.indentLevel--;
                            }
                            // 近战延迟属性。
                            SerializedProperty meleeDelayProp = settings.FindPropertyRelative("meleeDelay");
                            // 读取近战延迟值。
                            float meleeDelay = meleeDelayProp.floatValue;
                            EditorGUILayout.PropertyField(meleeDelayProp);
                            // 近战伤害。
                            EditorGUILayout.PropertyField(settings.FindPropertyRelative("meleeAttackDamage"));
                            // 近战范围。
                            EditorGUILayout.PropertyField(settings.FindPropertyRelative("meleeRange"));
                            // 近战相机抖动强度。
                            EditorGUILayout.PropertyField(settings.FindPropertyRelative("meleeCamShakeAmount"));
                            // 近战音效。
                            EditorGUILayout.PropertyField(settings.FindPropertyRelative("meleeAudioClip"));
                            // 近战动作结束后重新启用的等待时间。
                            EditorGUILayout.PropertyField(settings.FindPropertyRelative("reEnableMeleeAfterAction"));

                            // 将近战延迟限制在 1 秒以内。
                            if (meleeDelayProp.floatValue > 1f)
                            {
                                meleeDelayProp.floatValue = 1f;
                            }
                            EditorGUI.indentLevel--;
                        }
                        break;
                    case "Effects":
                        EditorGUILayout.Space(2f);
                        // 撞击特效配置。
                        EditorGUILayout.PropertyField(settings.FindPropertyRelative("impactEffects"));
                        EditorGUILayout.Space(2f);
                        break;
                    case "Events":
                        // 用户事件集合。
                        EditorGUILayout.PropertyField(settings.FindPropertyRelative("userEvents"));
                        // 自定义主射击方法数组。
                        EditorGUILayout.PropertyField(settings.FindPropertyRelative("customPrimaryShot"));
                        break;
                }
                EditorGUILayout.Space(10f);

                // 应用所有修改，写回序列化对象。
                serializedObject.ApplyModifiedProperties();
            }
        }
    }
}
#endif