// ============================================================
// TurretProjectile.cs —— 炮台发射的子弹（配合对象池使用）
// 由 Turret 生成、从对象池取出并初始化：沿指定方向匀速飞行，
// 命中障碍物或有效目标（玩家 / 敌人）时造成伤害，随后触发
// destroyEvent 通知炮台将自身归还对象池。
// ============================================================
using System;
using UnityEngine;
using UnityEngine.Events;

namespace cowsins
{
    /// <summary>炮台子弹：沿指定方向飞行，命中障碍物或有效目标后造成伤害并归还对象池。</summary>
    public class TurretProjectile : MonoBehaviour
    {
        // 目标类型（玩家 / 敌人），决定对哪类对象造成伤害。
        private Turret.TargetType targetType;
        // 飞行方向（世界空间单位向量）。
        private Vector3 direction;
        // 伤害值、飞行速度与生存时长（秒）。
        private float damage, speed, lifetime;
        // 用于判定障碍物的 LayerMask：命中该层则销毁子弹。
        private LayerMask obstacleLayers;
        // 销毁事件：子弹销毁时触发，炮台通过它把子弹归还对象池。
        public UnityEvent<TurretProjectile> destroyEvent;

        /// <summary>初始化子弹参数：记录目标类型、方向、伤害、速度、生存时长与障碍层，绑定归还对象池回调，并启动生命周期倒计时。</summary>
        /// <param name="targetType">目标类型（玩家 / 敌人）。</param>
        /// <param name="direction">飞行方向。</param>
        /// <param name="damage">伤害值。</param>
        /// <param name="speed">飞行速度。</param>
        /// <param name="duration">生存时长（秒）。</param>
        /// <param name="obstacleLayers">障碍物层。</param>
        /// <param name="returnToPool">归还对象池的回调。</param>
        public void Initialize(Turret.TargetType targetType, Vector3 direction, float damage, float speed, float duration, LayerMask obstacleLayers, Action<TurretProjectile> returnToPool)
        {
            this.targetType = targetType;
            this.direction = direction;
            this.damage = damage;
            this.speed = speed;
            this.lifetime = duration;
            this.obstacleLayers = obstacleLayers;

            destroyEvent.RemoveAllListeners();
            destroyEvent.AddListener(proj => returnToPool(proj));
            CancelInvoke();
            Invoke(nameof(DestroyProjectile), lifetime);
        }

        // 每帧更新：沿 direction 方向以 speed 速度在世界空间平移。
        private void Update()
        {
            transform.Translate(direction * speed * Time.deltaTime, Space.World);
        }

        // 触发碰撞回调：命中障碍物则销毁；命中有效目标则造成伤害后销毁。
        private void OnTriggerEnter(Collider other)
        {
            // Check if projectile hit an obstacle
            if (((1 << other.gameObject.layer) & obstacleLayers) != 0)
            {
                DestroyProjectile();
                return;
            }

            if (!IsValidTarget(other)) return;

            if (targetType == Turret.TargetType.Player && other.TryGetComponent<PlayerStats>(out var player))
                player.Damage(damage, false);

            if (targetType == Turret.TargetType.Enemies && other.TryGetComponent<IDamageable>(out var dmg))
                dmg.Damage(damage, false);

            DestroyProjectile();
        }

        // 判断碰撞体是否为有效目标：按目标类型检查 Player / Enemy 标签。
        private bool IsValidTarget(Collider other)
        {
            if (targetType == Turret.TargetType.Player) return other.CompareTag("Player");
            if (targetType == Turret.TargetType.Enemies) return other.CompareTag("Enemy");
            return false;
        }

        // 销毁子弹：触发 destroyEvent，通知炮台将自身归还对象池。
        private void DestroyProjectile() => destroyEvent?.Invoke(this);
    }
}