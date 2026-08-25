// Trigger（通用触发器基类）：
// 继承自 Identifiable，封装了物理触发器（OnTriggerEnter/Stay/Exit）的通用逻辑，
// 提供可序列化的 UnityEvent 回调与可继承的虚方法，并支持“只对玩家触发”“记住触发状态”及存档集成。
using UnityEngine;
using UnityEngine.Events;

namespace cowsins
{
    /// <summary>
    /// 通用触发器：把物理触发事件转发为可序列化事件与可重写虚方法，供子类复用。
    /// </summary>
    public partial class Trigger : Identifiable
    {
        [System.Serializable]
        // 触发器三阶段（进入/停留/退出）的 UnityEvent 集合
        public class Events
        {
            // 进入、停留、退出分别对应的事件
            public UnityEvent onEnter, onStay, onExit;
        }

        // 触发器事件集合实例
        [SerializeField] private Events events;

        // 是否记住触发状态（退出后是否保持 triggered 为 true）
        [SerializeField] private bool rememberTriggerState;
        // 是否只对玩家触发（默认只对玩家）
        [SerializeField] private bool onlyTriggerPlayer = true;

        // 当前是否已触发（带存档标记）
        [SaveField] protected bool triggered;

        // 碰撞体进入时：满足条件且未触发则触发进入事件与虚方法
        private void OnTriggerEnter(Collider other)
        {
            if (CanTrigger(other) && !triggered)
            {
                events.onEnter?.Invoke();
                triggered = true;
                TriggerEnter(other);
#if SAVE_LOAD_ADD_ON
                SaveTrigger();
                LoadedState(); 
#endif
            }
        }
        // 碰撞体停留时：满足条件则持续触发停留事件与虚方法
        private void OnTriggerStay(Collider other)
        {
            if (CanTrigger(other))
            {
                events.onStay?.Invoke();
                TriggerStay(other);
            }
        }
        // 碰撞体退出时：满足条件则触发退出事件与虚方法，并按配置重置触发状态
        private void OnTriggerExit(Collider other)
        {
            if (CanTrigger(other))
            {
                events.onExit?.Invoke();
                TriggerExit(other);
                if(!rememberTriggerState) triggered = false;
#if SAVE_LOAD_ADD_ON
                StoreData();
#endif
            }
        }

        // 子类可重写：碰撞体进入时调用
        public virtual void TriggerEnter(Collider other)
        {

        }
        // 子类可重写：碰撞体停留时调用
        public virtual void TriggerStay(Collider other)
        {
        }

        // 子类可重写：碰撞体退出时调用
        public virtual void TriggerExit(Collider other)
        {

        }

        // 判断该碰撞体是否满足触发条件（是否限定只对玩家）
        protected bool CanTrigger(Collider other) { return other.CompareTag("Player") && onlyTriggerPlayer || !onlyTriggerPlayer; }
    }
}