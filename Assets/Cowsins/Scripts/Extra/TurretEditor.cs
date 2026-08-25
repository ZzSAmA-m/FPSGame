// TurretEditor（炮塔自定义 Inspector）：
// 仅在编辑器下编译。为 Turret 组件提供自定义 Inspector 界面，
// 根据目标类型与射程形状动态显示对应字段，并把序列化属性分组展示，方便美术/策划配置炮塔参数。
#if UNITY_EDITOR

using UnityEngine;
using UnityEditor;

namespace cowsins
{
    [CustomEditor(typeof(Turret))]
    /// <summary>
    /// Turret 组件的自定义编辑器：按分组与射程形状动态绘制 Inspector。
    /// </summary>
    public class TurretEditor : Editor
    {
        // 以下均为与 Turret 组件中各字段对应的序列化属性缓存
        SerializedProperty targetType;
        SerializedProperty displayGizmos;
        SerializedProperty animator;
        SerializedProperty turretHead;
        SerializedProperty rangeShape;
        SerializedProperty detectionRadius;
        SerializedProperty cubeBounds;
        SerializedProperty coneRange;
        SerializedProperty coneAngle;
        SerializedProperty coneStaticDirection;
        SerializedProperty coneHorizontalAngle;
        SerializedProperty coneVerticalAngle;
        SerializedProperty allowVerticalMovement;
        SerializedProperty lerpSpeed;
        SerializedProperty requireLineOfSight;
        SerializedProperty obstacleLayers;
        SerializedProperty projectilePrefab;
        SerializedProperty projectileSpeed;
        SerializedProperty projectileDamage;
        SerializedProperty projectileDuration;
        SerializedProperty firePoint;
        SerializedProperty muzzleFlash;
        SerializedProperty fireRate;
        SerializedProperty projectilePoolSize;

        // 编辑器启用时：一次性查找并缓存所有序列化属性，供 Inspector 绘制使用
        private void OnEnable()
        {
            targetType = serializedObject.FindProperty("targetType");
            displayGizmos = serializedObject.FindProperty("displayGizmos");
            animator = serializedObject.FindProperty("animator");
            turretHead = serializedObject.FindProperty("turretHead");
            rangeShape = serializedObject.FindProperty("rangeShape");
            detectionRadius = serializedObject.FindProperty("detectionRadius");
            cubeBounds = serializedObject.FindProperty("cubeBounds");
            coneRange = serializedObject.FindProperty("coneRange");
            coneAngle = serializedObject.FindProperty("coneAngle");
            coneStaticDirection = serializedObject.FindProperty("coneStaticDirection");
            coneHorizontalAngle = serializedObject.FindProperty("coneHorizontalAngle");
            coneVerticalAngle = serializedObject.FindProperty("coneVerticalAngle");
            allowVerticalMovement = serializedObject.FindProperty("allowVerticalMovement");
            lerpSpeed = serializedObject.FindProperty("lerpSpeed");
            requireLineOfSight = serializedObject.FindProperty("requireLineOfSight");
            obstacleLayers = serializedObject.FindProperty("obstacleLayers");
            projectilePrefab = serializedObject.FindProperty("projectilePrefab");
            projectileSpeed = serializedObject.FindProperty("projectileSpeed");
            projectileDamage = serializedObject.FindProperty("projectileDamage");
            projectileDuration = serializedObject.FindProperty("projectileDuration");
            firePoint = serializedObject.FindProperty("firePoint");
            muzzleFlash = serializedObject.FindProperty("muzzleFlash");
            fireRate = serializedObject.FindProperty("fireRate");
            projectilePoolSize = serializedObject.FindProperty("projectilePoolSize");
        }

        // 自定义 Inspector 绘制入口：分组绘制炮塔的各配置字段
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            // Target Settings
            EditorGUILayout.Space(5);
            EditorGUILayout.PropertyField(targetType);
            EditorGUILayout.Space(10);

            // References
            EditorGUILayout.PropertyField(animator);
            EditorGUILayout.PropertyField(turretHead);
            EditorGUILayout.Space(10);

            // Range Shape
            EditorGUILayout.PropertyField(rangeShape);
            EditorGUILayout.PropertyField(displayGizmos);

            // Shape specific settings
            Turret.RangeShape currentShape = (Turret.RangeShape)rangeShape.enumValueIndex;

            switch (currentShape)
            {
                case Turret.RangeShape.Sphere:
                    EditorGUILayout.PropertyField(detectionRadius, new GUIContent("Detection Radius"));
                    break;

                case Turret.RangeShape.Cube:
                    EditorGUILayout.PropertyField(cubeBounds, new GUIContent("Cube Bounds"));
                    break;

                case Turret.RangeShape.Cone:
                    EditorGUILayout.PropertyField(coneRange, new GUIContent("Cone Range"));
                    EditorGUILayout.PropertyField(coneAngle, new GUIContent("Cone Angle"));
                    EditorGUILayout.PropertyField(coneStaticDirection, new GUIContent("Static Direction"));

                    if (coneStaticDirection.boolValue)
                    {
                        EditorGUILayout.PropertyField(coneHorizontalAngle, new GUIContent("Horizontal Angle"));
                        EditorGUILayout.PropertyField(coneVerticalAngle, new GUIContent("Vertical Angle"));
                    }
                    break;
            }

            EditorGUILayout.Space(10);

            // Basic Settings
            EditorGUILayout.PropertyField(allowVerticalMovement);
            EditorGUILayout.PropertyField(lerpSpeed);
            EditorGUILayout.PropertyField(requireLineOfSight);

            EditorGUILayout.PropertyField(obstacleLayers, new GUIContent("Obstacle Layers"));

            EditorGUILayout.Space(10);

            // Projectile Settings
            EditorGUILayout.PropertyField(projectilePrefab);
            EditorGUILayout.PropertyField(projectileSpeed);
            EditorGUILayout.PropertyField(projectileDamage);
            EditorGUILayout.PropertyField(projectileDuration);
            EditorGUILayout.PropertyField(firePoint);
            EditorGUILayout.PropertyField(muzzleFlash);
            EditorGUILayout.Space(10);

            // Shooting
            EditorGUILayout.PropertyField(fireRate);
            EditorGUILayout.Space(10);

            // Pool Settings
            EditorGUILayout.PropertyField(projectilePoolSize);

            serializedObject.ApplyModifiedProperties();
        }
    }
}

#endif