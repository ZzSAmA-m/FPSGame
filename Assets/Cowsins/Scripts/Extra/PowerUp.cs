// PowerUp（增益道具基类）：
// 继承自 Trigger，是所有可拾取增益道具（医疗包、伤害/治疗倍率等）的基类。
// 玩家进入触发器时触发 Interact，并支持“一次性使用”或“使用后延迟重新出现”两种模式（带计时 UI）。
/// <summary>
/// This script belongs to cowsins™ as a part of the cowsins´ FPS Engine. All rights reserved. 
/// </summary>
using UnityEngine;
using UnityEngine.UI;
using System.Collections;

namespace cowsins
{
    /// <summary>
    /// 增益道具基类：处理拾取触发、是否重新出现以及重现实计时。
    /// </summary>
    public class PowerUp : Trigger
    {
        // 是否在被拾取后重新出现
        [SerializeField] private bool reappears;

        // 重新出现所需时间（秒）
        [SerializeField] protected float reappearTime;

        // 用于显示重现实进度的 Image（环形进度）
        [SerializeField] private Image Timer;

        // 是否已被使用（隐藏字段，但可被外部读写）
        [HideInInspector] public bool used;

        // 重现实计时器
        protected float timer = 0;

        // 当前运行的计时协程
        private Coroutine timerCoroutine;

        // 初始化：先隐藏计时 UI
        private void Start()
        {
            Timer?.gameObject.SetActive(false);
        }

        // 玩家停留在触发器内时：若未使用则触发交互，并按需销毁或开始重现实计时
        public override void TriggerStay(Collider other)
        {
            if (used) return;
            
            Interact(other.GetComponent<PlayerDependencies>());

#if SAVE_LOAD_ADD_ON
            SaveTrigger();
#endif

            if (!reappears)
            {
                // 一次性道具：直接销毁
                Destroy(this.gameObject);
            }
            else
            {
                used = true;
                Timer?.gameObject.SetActive(true);
                if (timerCoroutine != null) StopCoroutine(timerCoroutine); // Stop any existing coroutine
                timerCoroutine = StartCoroutine(StartTimerCoroutine());
            } 
        }

        // 重现实计时协程：倒计时期间更新进度 UI，结束后恢复为可用状态
        private IEnumerator StartTimerCoroutine()
        {
            float timer = reappearTime;

            while (timer > 0)
            {
                timer -= Time.deltaTime;
                if (Timer != null)
                    Timer.fillAmount = (reappearTime - timer) / reappearTime;

                yield return null;
            }

            used = false;
            Timer?.gameObject.SetActive(false);
        }

        // 交互逻辑（子类重写实现具体效果）
        public virtual void Interact(PlayerDependencies player)
        {
            // Override this
        }

#if SAVE_LOAD_ADD_ON
        // If this power up was triggered and is not supposed to reappear, destroy it.
        // 存档加载后：若已被触发且不会重新出现，则销毁该道具
        public override void LoadedState()
        {
            if (triggered && !reappears) Destroy(this.gameObject);
        }
#endif
    }
}