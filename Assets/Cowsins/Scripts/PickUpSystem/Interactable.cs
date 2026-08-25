/// <summary>
/// This script belongs to cowsins™ as a part of the cowsins´ FPS Engine. All rights reserved. 
/// </summary>
// ----------------------------------------------------------------------
// Interactable.cs
// 作用：所有“可交互对象”的基类。挂载到场景中任何可被玩家交互的物体上，
//       提供交互文本、即时交互开关、交互事件以及 Interact / Highlight /
//       Unhighlight / IsForbiddenInteraction 等虚方法，供 DoorInteractable、
//       PickUp 等子类重写以实现各自不同的交互行为。
//       文件同时包含编辑器自定义 Inspector（InteractableEditor）用于校验配置。
// ----------------------------------------------------------------------
using UnityEngine;
using UnityEngine.Events;
using UnityEditor;
namespace cowsins
{
    /// <summary>
    /// 可交互对象基类。挂载到场景中任何可被玩家交互的物体上，提供交互文本、
    /// 即时交互开关、交互事件以及 Interact / Highlight / Unhighlight 等虚方法，
    /// 供子类（如 DoorInteractable、PickUp）重写以实现各自不同的交互行为。
    /// </summary>
    /// <summary>
    /// This component must be add to ANY interactable object in your environment.
    /// However, you might want to check other examples such as DoorInteractable.cs or PickUps
    /// They are different things, they do different things, but they both are INTERACTABLES and you can interact with both of them.
    /// Then, how does that work? Their custom scripts both inherit from interactable.cs, which inherits from MonoBehaviour, so even though they do 
    /// different things they are actually the same kind of thing.
    /// Since Interact() is a virtual void you can override it on your custom script inheriting from this one
    /// 
    /// For any doubts check the documentation or contact the support.
    /// 
    /// </summary>
    public partial class Interactable : Identifiable
    {
        // 标记该对象是否可交互（HideInInspector 使其不在 Inspector 中显示，通常由系统在运行时控制）。
        [HideInInspector] public bool interactable = false;

        // 交互时显示在交互 UI 上的提示文本。
        [Tooltip("Text that will be displayed on the Interaction UI"), Title("INTERACTABLE")]
        public string interactText;

        // 是否为“即时交互”：为 true 时无需长按即可立即触发交互。
        [SerializeField] private bool instantInteraction = false;

        // 只读属性，供外部判断该交互物是否为即时交互。
        public bool InstantInteraction => instantInteraction;

        [System.Serializable]
        /// <summary>
        /// 交互事件内部类，用于存放可序列化的交互事件。
        /// </summary>
        public class InteractableEvents // Store your events
        {
            // 交互发生时触发的事件。
            public UnityEvent OnInteract;
        }

        // 交互事件实例，交互发生时用于触发订阅的回调。
        public InteractableEvents interactableEvents;

        // 是否已被交互过（SaveField 标记表示其参与存档，用于记录拾取/交互状态）。
        [SaveField] protected bool alreadyInteracted = false;

        /// <summary>
        /// Make sure to override this on your new custom class.
        /// If you still wanna call this method, make sure to write the following line:
        /// base.Interact(player);
        /// </summary>
        /// <summary>
        /// 执行交互：触发交互事件、将对象标记为已交互，并在存档插件启用时保存数据。
        /// 参数 player 为执行交互的玩家 Transform。子类重写时应按需调用 base.Interact(player)。
        /// </summary>
        public virtual void Interact(Transform player)
        {
            // 触发交互事件（若已订阅）。
            interactableEvents.OnInteract?.Invoke();
            // 标记该对象已完成交互。
            alreadyInteracted = true;
#if SAVE_LOAD_ADD_ON
            StoreData();
#endif
        }

        // 高亮该交互物（虚方法，默认空实现，供子类重写以显示高亮效果）。
        public virtual void Highlight()
        {

        }
        // 取消高亮该交互物（虚方法，默认空实现，供子类重写以清除高亮效果）。
        public virtual void Unhighlight()
        {

        }

        // 判断当前交互是否被禁止（如武器不兼容等）。默认返回 false 表示允许交互，子类可重写。
        public virtual bool IsForbiddenInteraction(IWeaponReferenceProvider weaponController)
        {
            return false; 
        }
    }


#if UNITY_EDITOR
    [System.Serializable]
    [CustomEditor(typeof(Interactable))]
    /// <summary>
    /// Interactable 的自定义 Inspector：校验图层与 Collider 配置，并绘制交互文本与交互事件字段。
    /// </summary>
    public class InteractableEditor : Editor
    {

        // 自定义 Inspector 绘制逻辑：校验 Interactable 图层与 Collider 组件，并绘制交互字段。
        override public void OnInspectorGUI()
        {
            serializedObject.Update();
            Interactable myScript = target as Interactable;
            EditorGUILayout.Space(5);
            // 校验对象所在图层是否为 “Interactable”，否则给出错误提示。
            if (myScript.gameObject.layer != LayerMask.NameToLayer("Interactable"))
            {
                EditorGUILayout.HelpBox("Ensure your custom interactable object has the `Interactable` layer assigned.", MessageType.Error);
            }
            EditorGUILayout.Space(5);
            // 校验对象是否挂载了 Collider 组件，否则给出错误提示。
            if (!myScript.gameObject.GetComponent<Collider>())
            {
                EditorGUILayout.HelpBox("Ensure your custom interactable object has a COLLIDER component assigned.", MessageType.Error);
            }
            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("INTERACTABLE", EditorStyles.boldLabel);

            EditorGUILayout.PropertyField(serializedObject.FindProperty("interactText"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("interactableEvents"));
            serializedObject.ApplyModifiedProperties();

        }
    }
#endif
}