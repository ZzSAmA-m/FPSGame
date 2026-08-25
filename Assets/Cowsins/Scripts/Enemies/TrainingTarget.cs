// ============================================================
// TrainingTarget.cs —— 训练场靶子（可被击倒并自动复原）
// 继承自 EnemyHealth，用于训练场景中的射击靶：
// 被击中时播放受击动画，血量为零时播放倒地动画、隐藏状态 UI 并
// 从指南针中移除标记，经过 timeToRevive 秒后自动复活恢复满状态。
// ============================================================
using UnityEngine;
namespace cowsins
{
    /// <summary>训练靶子：继承敌人生命逻辑，被击倒后播放动画并自动复原。</summary>
    public class TrainingTarget : EnemyHealth
    {
        // 被击倒后到自动复活之间的等待时间（秒）。
        [SerializeField] private float timeToRevive;

        // 动画控制器：播放受击 / 倒地 / 复活动画。
        private Animator animator;
        // 父物体上的指南针元素：死亡时从指南针移除标记，复活时重新添加。
        private CompassElement compassElement;

        // 重写 Start：先缓存动画与指南针元素引用，再调用基类初始化。
        public override void Start()
        {
            animator = GetComponent<Animator>();
            compassElement = transform.parent.GetComponent<CompassElement>();
            base.Start();
        }

        // 重写 Damage：死亡则忽略；否则先播放受击动画再调用基类伤害处理。
        public override void Damage(float damage, bool isHeadshot)
        {
            if (isDead) return;
            animator.Play("Target_Hit");
            base.Damage(damage, isHeadshot);
        }
        // 重写 Die：标记死亡、触发事件、隐藏状态 UI、移除指南针标记、播放倒地动画与音效并定时复活。
        public override void Die()
        {
            if (isDead) return;
            isDead = true;
            events.OnDeath?.Invoke();
            Invoke("Revive", timeToRevive);

            if (enemyStatusContainer != null) enemyStatusContainer.gameObject.SetActive(false);

            if (showKillFeed) UIEvents.onEnemyKilled.Invoke(_name);

            if (compassElement != null) compassElement.Remove();

            animator.Play("Target_Die");

            SoundManager.Instance.PlaySound(dieSFX, 0, 0, false);
        }
        // 复活：解除死亡状态、播放复活动画、恢复生命 / 护盾并恢复 UI 与指南针标记。
        private void Revive()
        {
            isDead = false;
            animator.Play("Target_Revive");
            health = maxHealth;
            shield = maxShield;

            if (enemyStatusContainer != null) enemyStatusContainer.gameObject.SetActive(true);

            if (compassElement != null) compassElement.Add();

            if (healthBar != null) healthBar.fillAmount = 1;
            if (shieldBar != null) shieldBar.fillAmount = 1;

#if SAVE_LOAD_ADD_ON
            StoreData();
#endif
        }

#if SAVE_LOAD_ADD_ON
        /// <summary>读取存档后调用：若血量为零则复活，否则按当前血量刷新血条显示。</summary>
        public override void LoadedState()
        {
            if (health <= 0)
                Revive();
                
            healthBar.fillAmount = health / maxHealth;
            shieldBar.fillAmount = shield / maxShield;
        }
#endif
    }
}