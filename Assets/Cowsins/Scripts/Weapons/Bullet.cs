// Bullet.cs
// 投射物弹丸组件：实现 IBullet 接口，负责弹丸的直线飞行、碰撞伤害与命中爆炸。
// 由 ProjectileShootStyle 在枪口点实例化并配置属性，飞行方向朝向 Destination，
// 命中目标时结算伤害(区分暴击/身体部位/普通伤害)，并在开启爆炸时对范围内目标造成随距离衰减的范围伤害与冲击力。
/// <summary>
/// This script belongs to cowsins™ as a part of the cowsins´ FPS Engine. All rights reserved. 
/// </summary>using UnityEngine;
using UnityEngine;

namespace cowsins
{
    /// <summary>投射物弹丸组件：沿目标点飞行，命中后结算伤害与可选的范围爆炸。</summary>
    public class Bullet : MonoBehaviour, IBullet
    {
        // 弹丸飞行速度
        [HideInInspector] public float Speed { get; set; }
        // 弹丸基础伤害
        [HideInInspector] public float Damage { get; set; }
        // 弹丸目标点，飞行朝向并最终命中/到达的位置
        [HideInInspector] public Vector3 Destination { get; set; }
        // 是否受重力影响
        [HideInInspector] public bool Gravity { get; set; }
        // 发射该弹丸的玩家 Transform
        [HideInInspector] public Transform Player { get; set; }
        // 爆炸时是否会对玩家造成伤害
        [HideInInspector] public bool HurtsPlayer { get; set; }
        // 命中/销毁时是否触发范围爆炸
        [HideInInspector] public bool ExplosionOnHit { get; set; }
        // 爆炸特效预制体
        [HideInInspector] public GameObject ExplosionVFX { get; set; }
        // 爆炸作用半径
        [HideInInspector] public float ExplosionRadius { get; set; }
        // 爆炸施加给刚体的冲击力
        [HideInInspector] public float ExplosionForce { get; set; }
        // 暴击伤害倍率
        [HideInInspector] public float CriticalMultiplier { get; set; }
        // 弹丸存活时长(秒)，超时后自动销毁
        [HideInInspector] public float Duration { get; set; }

        // 弹丸可与之碰撞(视为地面/障碍)的层掩码
        [SerializeField] private LayerMask projectileHitLayer;


        // 弹丸是否已命中过目标，用于防止重复命中/双重伤害
        private bool projectileHasAlreadyHit = false; // Prevent from double hitting issues

        // Start：在首帧让弹丸朝向目标点，并在 Duration 秒后自动销毁弹丸。
        private void Start()
        {
            transform.LookAt(Destination);
            Invoke(nameof(DestroyProjectile), Duration);
        }

        // Update：每帧沿自身前方以 Speed 速度直线移动。
        private void Update()
        {
            transform.Translate(0.0f, 0.0f, Speed * Time.deltaTime);
        }

        // 碰撞回调：弹丸进入其他碰撞体时结算命中。
        // 根据目标标签(暴击/身体部位)、是否可伤害、是否为地面/障碍分别处理伤害或销毁。
        private void OnTriggerEnter(Collider other)
        {
            if (projectileHasAlreadyHit || other.gameObject.layer == LayerMask.NameToLayer("Effects")) return;

            IDamageable damageable = other.GetComponent<IDamageable>();

            // 命中暴击部位：对父级可伤害对象按 基础伤害×暴击倍率 结算伤害
            if (other.CompareTag("Critical"))
            {
                DamageTarget(CowsinsUtilities.GatherDamageableParent(other.transform), Damage * CriticalMultiplier, false);
            }
            // 命中身体部位：对父级可伤害对象按基础伤害结算
            else if (other.CompareTag("BodyShot"))
            {
                DamageTarget(CowsinsUtilities.GatherDamageableParent(other.transform), Damage, false);
            }
            // 命中普通可伤害对象(非玩家)：按基础伤害结算
            else if (damageable != null && !other.CompareTag("Player"))
            {
                DamageTarget(damageable, Damage, false);
            }
            // 命中地面/障碍层：直接销毁弹丸
            else if (IsGroundOrObstacleLayer(other.gameObject.layer))
            {
                DestroyProjectile();
            }
        }

        // 对目标结算伤害：若目标有效则调用其 Damage，标记已命中并销毁弹丸。
        private void DamageTarget(IDamageable target, float dmg, bool isCritical)
        {
            if (target != null)
            {
                target.Damage(dmg, isCritical);
                projectileHasAlreadyHit = true;
                DestroyProjectile();
            }
        }

        // 判断给定层是否属于弹丸应视为地面/障碍的层(通过位掩码判断)。
        private bool IsGroundOrObstacleLayer(int layer)
        {
            return (projectileHitLayer.value & (1 << layer)) != 0;
        }

        // 销毁弹丸：若开启命中爆炸，则生成爆炸特效并对半径内目标造成随距离衰减的范围伤害与冲击力。
        private void DestroyProjectile()
        {
            if (ExplosionOnHit)
            {
                if (ExplosionVFX != null)
                {
                    var contact = GetComponent<Collider>().ClosestPoint(transform.position);
                    Instantiate(ExplosionVFX, contact, Quaternion.identity);
                }

                // 获取爆炸半径内的所有碰撞体
                Collider[] colliders = Physics.OverlapSphere(transform.position, ExplosionRadius);

                foreach (var collider in colliders)
                {
                    var damageable = collider.GetComponent<IDamageable>();
                    var playerMovement = collider.GetComponent<PlayerMovement>();
                    var rigidbody = collider.GetComponent<Rigidbody>();

                    // 对范围内可伤害目标结算随距离衰减的伤害
                    if (damageable != null)
                    {
                        // Calculate the distance ratio and damage based on the explosion radius
                        float distanceRatio = 1 - Mathf.Clamp01(Vector3.Distance(collider.transform.position, transform.position) / ExplosionRadius);
                        float dmg = Damage * distanceRatio;

                        // Apply damage if the collider is a player and the explosion should hurt the player
                        if (collider.CompareTag("Player") && HurtsPlayer)
                        {
                            damageable.Damage(dmg, false);
                        }
                        // Apply damage if the collider is not a player
                        else if (!collider.CompareTag("Player"))
                        {
                            damageable.Damage(dmg, false);
                        }
                    }

                    // 对玩家触发爆炸相机震动
                    if (playerMovement != null)
                    {
                        CameraEffects cameraEffects = playerMovement.GetComponent<CameraEffects>();
                        cameraEffects.ExplosionShake(Vector3.Distance(cameraEffects.transform.position, transform.position));
                    }

                    // 对刚体施加爆炸冲击力
                    if (rigidbody != null && collider != this)
                    {
                        rigidbody.AddExplosionForce(ExplosionForce, transform.position, ExplosionRadius, 5, ForceMode.Force);
                    }
                }
            }

            Destroy(gameObject);
        }
    }
}
