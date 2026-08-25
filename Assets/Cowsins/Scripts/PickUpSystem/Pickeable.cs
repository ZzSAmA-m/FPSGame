// ======================================================================
// Pickeable.cs —— 可拾取物品抽象基类
// 继承自 Interactable，是所有可拾取对象(武器、配件、通用物品等)的公共基类。
// 负责管理拾取物在世界中的展示效果(旋转、上下浮动)、掉落时的物理表现，
// 以及在玩家完成交互时触发 OnPickUp 事件。
// ======================================================================
/// <summary>
/// This script belongs to cowsins™ as a part of the cowsins´ FPS Engine. All rights reserved. 
/// </summary>
using UnityEngine.UI;
using UnityEngine;
using UnityEngine.Events;
namespace cowsins
{
    /// <summary>可拾取物品的抽象基类：继承自 Interactable，提供拾取物的特效展示、掉落物理与拾取事件等通用能力，具体物品需派生实现。</summary>
    public abstract class Pickeable : Interactable
    {
        /// <summary>可序列化的事件容器：存放拾取成功时触发的回调事件，便于在 Inspector 中配置。</summary>
        [System.Serializable]
        public class Events
        {
            // 拾取成功时触发的事件回调。
            public UnityEvent OnPickUp;
        }
        // 拾取物的事件实例，Inspector 中序列化保存，用于运行时触发 OnPickUp。
        [SerializeField] private Events events;

        // 是否启用旋转 / 上下浮动特效开关。
        [Tooltip("Apply the selected effect")]
        public bool rotates, translates;

        // 旋转与上下浮动的速度参数。
        [Tooltip("Change the speed of the selected effect"), SerializeField]
        private float rotationSpeed, translationSpeed;

        // 用于显示拾取物图标/贴图的 UI Image 引用。
        [SerializeField] protected Image image;

        // 实例化后用于存放拾取物视觉模型(图形)的父 Transform。
        [Tooltip("Transform under which the graphics will be stored at when instantiated"), SerializeField]
        protected Transform graphics;

        // 标记该拾取物是否已被玩家丢弃到场景中。
        [HideInInspector] public bool dropped;

        // 标记该拾取物当前是否处于可被拾取的状态。
        [HideInInspector] protected bool pickeable;

        // 上下浮动动画使用的累计计时器。
        private float timer = 0f;

        // 缓存拾取物自身的刚体组件，用于施加掉落后的物理冲量。
        private Rigidbody rb;

        /// <summary>初始化：将拾取状态置为不可拾取，并缓存刚体组件引用。</summary>
        public virtual void Awake()
        {
            pickeable = false;
            rb = GetComponent<Rigidbody>();
        }
        /// <summary>固定帧更新：当启用了旋转或浮动特效时，调用 Movement 处理逐物理帧的运动效果。</summary>
        public virtual void FixedUpdate()
        {
            if (rotates || translates) Movement();
        }

        /// <summary>玩家交互入口：直接触发 OnPickUp 事件，由具体子类或事件监听者完成实际拾取逻辑。</summary>
        public override void Interact(Transform player) => events.OnPickUp.Invoke();

        // 特效运动逻辑：对刚体施加向下的持续力模拟悬浮重力，并根据开关执行旋转与上下正弦浮动，营造卡通/风格化动画效果。
        /// <summary>
        /// Apply effects, usually for more cartoony, stylized, anime approaches
        /// </summary>
        private void Movement()
        {
            rb?.AddForce(Vector3.down * 15, ForceMode.Force);
            if (graphics == null) return;

            // Rotation
            if (rotates)
                graphics.Rotate(Vector3.up * rotationSpeed * Time.fixedDeltaTime);

            // Translation (Up and Down Motion)
            if (translates)
            {
                timer += Time.deltaTime * translationSpeed;
                float translateMotion = Mathf.Sin(timer) / 500f;
                Vector3 localPos = graphics.localPosition;
                localPos.y += translateMotion;
                graphics.localPosition = localPos;
            }
        }

        /// <summary>处理拾取物被丢弃：标记 dropped 状态，并沿玩家朝向施加一个向前的速度冲量让物品飞出。</summary>
        public virtual void Drop(PlayerDependencies playerDependencies, PlayerOrientation orientation)
        {
            dropped = true;

            Vector3 force = orientation.Forward * 4;

            if (rb == null)
            {
                CowsinsUtilities.LogError("<b><color=yellow>Rigidbody component not found!</color></b> " +
                    "Please assign a <b><color=cyan>Rigidbody Component</color></b> to your Pickeable Object to fix this error.", this);
            }
            else rb.AddForce(force, ForceMode.VelocityChange);
        }

        /// <summary>销毁 graphics 容器下的第一个子物体，即移除当前拾取物的视觉模型。</summary>
        public virtual void DestroyGraphics() => Destroy(graphics.GetChild(0).gameObject);
    }
}