// ============================================================
// ProceduralShot_SO.cs —— 程序化开枪（后坐 / 抖动）特效配置资产
// 这是一个 ScriptableObject，用于描述武器开枪瞬间的位移与旋转动画。
// 通过 X/Y/Z 三轴的 AnimationCurve（0~1 时间轴）分别定义位移
// （Translation）与旋转（Rotation）轨迹，配合幅度上限与瞄准倍率等参数，
// 供武器射击系统在开枪时采样播放，实现可配置的程序化后坐表现。
// 文件末尾还包含一段仅在编辑器下编译的自定义 Inspector 绘制代码。
// ============================================================
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif
namespace cowsins
{
    [CreateAssetMenu(fileName = "NewProceduralShot", menuName = "COWSINS/New Procedural Shot Effect", order = 2)]
    /// <summary>程序化开枪特效配置：定义开枪时武器在 X/Y/Z 轴的位移与旋转曲线，以及幅度、瞄准缩放等参数。</summary>
    public class ProceduralShot_SO : ScriptableObject
    {
        // 特效播放速度（0~2），控制位移 / 旋转曲线采样进度的快慢。
        [Range(0, 2)] public float playSpeed;

        [System.Serializable]
        /// <summary>三轴位移曲线结构：分别以 X/Y/Z 轴的 AnimationCurve 描述位移随时间的变化。</summary>
        public struct Translation
        {
            [Tooltip("Translation on the X axis. The movement follows this Animation Curves from 0 to 1")] public AnimationCurve xTranslation;

            [Tooltip("Translation on the Y axis. The movement follows this Animation Curves from 0 to 1")] public AnimationCurve yTranslation;

            [Tooltip("Translation on the Z axis. The movement follows this Animation Curves from 0 to 1")] public AnimationCurve zTranslation;
        }
        [System.Serializable]
        /// <summary>三轴旋转曲线结构：分别以 X/Y/Z 轴的 AnimationCurve 描述旋转随时间的变化。</summary>
        public struct Rotation
        {
            [Tooltip("Rotation on the X axis. The movement follows this Animation Curves from 0 to 1")] public AnimationCurve xRotation;

            [Tooltip("Rotation on the Y axis. The movement follows this Animation Curves from 0 to 1")] public AnimationCurve yRotation;

            [Tooltip("Rotation on the Z axis. The movement follows this Animation Curves from 0 to 1")] public AnimationCurve zRotation;
        }

        // 开枪位移曲线配置（三轴位移曲线集合）。
        public Translation translation;

        // 位移幅度上限（各轴不小于 0），用于缩放位移曲线产生的偏移量。
        [Min(0)] public Vector3 translationDistance;

        // 瞄准状态下的位移缩放倍率（0~1），用于在瞄准时削弱位移幅度。
        [Range(0, 1)] public float aimingTranslationMultiplier;

        // 开枪旋转曲线配置（三轴旋转曲线集合）。
        public Rotation rotation;

        // 旋转幅度上限（各轴不小于 0），用于缩放旋转曲线产生的角度。
        [Min(0)] public Vector3 rotationDistance;

        // 瞄准状态下的旋转缩放倍率（0~1），用于在瞄准时削弱旋转幅度。
        [Range(0, 1)] public float aimingRotationMultiplier;
    }


#if UNITY_EDITOR
    [System.Serializable]
    [CustomEditor(typeof(ProceduralShot_SO))]
    /// <summary>ProceduralShot_SO 的自定义 Inspector：按分组展示并编辑各项开枪特效参数。</summary>
    public class ProceduralShot_SOEditor : Editor
    {
        /// <summary>绘制自定义 Inspector：加载标题贴图，并按“变量 / 特效定制”分组显示各项序列化属性。</summary>
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            Weapon_SO myScript = target as Weapon_SO;

            Texture2D myTexture = Resources.Load<Texture2D>("CustomEditor/proceduralShot_CustomEditor") as Texture2D;
            GUILayout.Label(myTexture);

            EditorGUILayout.BeginVertical();


            EditorGUILayout.LabelField("VARIABLES", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("playSpeed"));
            EditorGUILayout.Space(10f);

            EditorGUILayout.LabelField("EFFECT CUSTOMIZATION", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("translation"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("translationDistance"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("aimingTranslationMultiplier"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("rotation"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("rotationDistance"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("aimingRotationMultiplier"));
            EditorGUILayout.EndVertical();

            serializedObject.ApplyModifiedProperties();
        }
    }
#endif
}