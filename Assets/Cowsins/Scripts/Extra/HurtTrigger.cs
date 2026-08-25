// HurtTrigger（伤害区域触发器）：
// 继承自 Trigger，当玩家停留在触发器内时按固定冷却周期持续造成伤害，
// 用于实现火焰、毒气、激光等持续伤害区域。
using UnityEngine;
namespace cowsins
{
    /// <summary>
    /// 持续伤害触发器：对停留在区域内的可伤害对象按冷却时间周期性造成伤害。
    /// </summary>
    public class HurtTrigger : Trigger
    {
        // 每次造成的伤害数值
        [SerializeField] private float damage;
        // 两次伤害之间的冷却时间
        [SerializeField] private float cooldown = 1f;

        // 冷却计时器（倒计时到 0 才能再次造成伤害）
        private float timer;

        // 初始化时将计时器归零，使进入区域可立即造成伤害
        private void Awake() => timer = 0;

        // 玩家停留在触发器内时：冷却结束后尝试造成伤害并重置冷却
        public override void TriggerStay(Collider other)
        {
            if (timer <= 0)
            {
                // 若碰撞体实现了 IDamageable 接口则对其造成伤害
                if (other.TryGetComponent(out IDamageable damageable))
                    damageable.Damage(damage, false);

                timer = cooldown;
            }

            timer -= Time.deltaTime;
        }
    }
}