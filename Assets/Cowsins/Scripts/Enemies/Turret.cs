// ============================================================
// Turret.cs —— 炮台（自动索敌 / 瞄准 / 射击）
// 可配置目标类型（玩家 / 敌人）、检测范围形状（球形 / 立方体 / 锥形）、
// 视线阻挡判定与射速，配合对象池发射 TurretProjectile 子弹。
// 在编辑器场景视图中还会绘制对应范围形状的辅助线框（Gizmos）。
// ============================================================
using UnityEngine;

namespace cowsins
{
    /// <summary>炮台组件：在指定范围内索敌、瞄准并射击目标，支持多种检测形状与对象池子弹。</summary>
    public class Turret : MonoBehaviour
    {
        /// <summary>炮台目标类型：Player（玩家）或 Enemies（敌人）。</summary>
        public enum TargetType
        {
            Player,
            Enemies
        }

        public enum RangeShape
        {
            Sphere,
            Cube,
            Cone
        }

        [SerializeField, Title("Target Settings"), Tooltip("Choose whether this turret targets the Player or Enemies.")]
        // 目标类型（玩家 / 敌人）。
        private TargetType targetType = TargetType.Player;

        // 是否在场景视图中绘制检测范围线框。
        [SerializeField] private bool displayGizmos = true;
        // 动画控制器：开火时触发 Fire 动画。
        [SerializeField] private Animator animator;
        [SerializeField, Tooltip("The part of the turret that rotates.")] private Transform turretHead;

        [SerializeField, Title("Range Shape"), Tooltip("Select the detection range shape.")]
        private RangeShape rangeShape = RangeShape.Sphere;

        [SerializeField, Tooltip("Distance within the player is detectable (Sphere mode).")]
        // 球形检测半径。
        private float detectionRadius = 10f;

        [SerializeField, Tooltip("Bounds of the cube detection area.")]
        // 立方体检测区域的三轴尺寸。
        private Vector3 cubeBounds = new Vector3(10f, 10f, 10f);

        [SerializeField, Tooltip("Maximum distance of cone detection.")]
        // 锥形检测的最大距离。
        private float coneRange = 15f;
        [SerializeField, Tooltip("Angle of the cone in degrees."), Range(0f, 180f)]
        // 锥形张开角度（度）。
        private float coneAngle = 60f;
        [SerializeField, Tooltip("If true, cone uses a fixed direction. If false, cone rotates with turret head.")]
        // 锥形是否使用固定方向（为 false 时随炮头旋转）。
        private bool coneStaticDirection = false;
        [SerializeField, Tooltip("Horizontal rotation angle (0-360 degrees)."), Range(0f, 360f)]
        // 固定方向锥形的水平旋转角（0~360 度）。
        private float coneHorizontalAngle = 0f;
        [SerializeField, Tooltip("Vertical rotation angle (-90 to 90 degrees)."), Range(-90f, 90f)]
        // 固定方向锥形的垂直旋转角（-90~90 度）。
        private float coneVerticalAngle = 0f;

        [SerializeField, Tooltip("Enable vertical movement."), Title("Basic Settings")]
        // 是否允许垂直方向瞄准（关闭时忽略目标高度差）。
        private bool allowVerticalMovement = false;
        // 瞄准旋转的插值速度。
        [SerializeField, Tooltip("Speed of rotation interpolation.")] private float lerpSpeed = 5f;
        [SerializeField, Tooltip("Check for line of sight to target. If blocked, turret won't track.")]
        // 是否要求目标可见（无遮挡）。
        private bool requireLineOfSight = false;
        [SerializeField, Tooltip("Layers that block line of sight.")]
        // 会遮挡视线的层（用于视线射线检测）。
        private LayerMask obstacleLayers;

        [SerializeField, Title("Projectile Settings")] private GameObject projectilePrefab;
        // 子弹速度、伤害与生存时长。
        [SerializeField, Min(0)] private float projectileSpeed, projectileDamage, projectileDuration;
        // 开火点（子弹生成位置）。
        [SerializeField] private Transform firePoint;
        // 枪口闪光特效预制体（配合对象池使用）。
        [SerializeField] private GameObject muzzleFlash;
        // 射速（每秒射击次数）。
        [SerializeField, Tooltip("Shots per second."), Title("Shooting")] private float fireRate = 2f;

        [SerializeField, Title("Pool Settings")] private int projectilePoolSize;

        // 当前是否允许开火。
        private bool canShoot = false;
        private float fireCooldown = 0f;
        // 当前锁定的目标。
        private Transform target;

        Vector3 targetDirection;
        // 记录的目标旋转（运行期使用）。
        Quaternion targetRotation;

        private void Start()
        {
            InitializeTarget();
            PoolManager.Instance.RegisterPool(projectilePrefab, projectilePoolSize);
            PoolManager.Instance.RegisterPool(muzzleFlash, projectilePoolSize);
        }

        private void Update()
        {
            UpdateTarget();
            if (target == null) return;

            Vector3 targetDir = target.position - transform.position;
            if (!allowVerticalMovement) targetDir.y = 0f;

            if (IsTargetInRange(target.position) && HasLineOfSight(target.position))
            {
                AimAtTarget(targetDir);
                Shoot(targetDir);
            }
            else
            {
                canShoot = false;
                fireCooldown = fireRate;
            }
        }

        private void InitializeTarget()
        {
            if (targetType == TargetType.Player)
            {
                GameObject player = GameObject.FindWithTag("Player");
                if (player != null) target = player.transform;
            }
        }

        private void UpdateTarget()
        {
            if (targetType == TargetType.Enemies)
            {
                FindClosestEnemy();
            }
        }

        private bool IsTargetInRange(Vector3 targetPosition)
        {
            switch (rangeShape)
            {
                case RangeShape.Sphere:
                    return IsInSphereRange(targetPosition);

                case RangeShape.Cube:
                    return IsInCubeRange(targetPosition);

                case RangeShape.Cone:
                    return IsInConeRange(targetPosition);

                default:
                    return false;
            }
        }

        private bool IsInSphereRange(Vector3 targetPosition)
        {
            float distance = Vector3.Distance(transform.position, targetPosition);
            return distance <= detectionRadius;
        }

        private bool IsInCubeRange(Vector3 targetPosition)
        {
            Vector3 localPos = transform.InverseTransformPoint(targetPosition);
            return Mathf.Abs(localPos.x) <= cubeBounds.x / 2f &&
                   Mathf.Abs(localPos.y) <= cubeBounds.y / 2f &&
                   Mathf.Abs(localPos.z) <= cubeBounds.z / 2f;
        }

        private bool IsInConeRange(Vector3 targetPosition)
        {
            Vector3 directionToTarget = targetPosition - transform.position;
            float distance = directionToTarget.magnitude;

            if (distance > coneRange) return false;

            Vector3 forward = GetConeForwardDirection();
            float angle = Vector3.Angle(forward, directionToTarget);

            return angle <= coneAngle / 2f;
        }

        private Vector3 GetConeForwardDirection()
        {
            if (coneStaticDirection)
            {
                Quaternion rotation = Quaternion.Euler(coneVerticalAngle, coneHorizontalAngle, 0f);
                return transform.TransformDirection(rotation * Vector3.forward);
            }
            else
            {
                return turretHead != null ? turretHead.forward : transform.forward;
            }
        }

        private bool HasLineOfSight(Vector3 targetPosition)
        {
            if (!requireLineOfSight) return true;

            Vector3 direction = targetPosition - transform.position;
            float distance = direction.magnitude;

            if (Physics.Raycast(transform.position, direction.normalized, out RaycastHit hit, distance, obstacleLayers))
            {
                // line of sight blocked
                return false;
            }

            return true;
        }

        private void AimAtTarget(Vector3 direction)
        {
            canShoot = true;
            Quaternion targetRotation = Quaternion.LookRotation(direction);
            turretHead.rotation = Quaternion.RotateTowards(turretHead.rotation, targetRotation, lerpSpeed * 100f * Time.deltaTime);
        }

        private void Shoot(Vector3 direction)
        {
            fireCooldown -= Time.deltaTime;
            if (!canShoot || fireCooldown > 0) return;

            fireCooldown = fireRate;
            animator?.SetTrigger("Fire");
            PoolManager.Instance.GetFromPool(muzzleFlash, firePoint.position, firePoint.rotation);

            var projGO = PoolManager.Instance.GetFromPool(projectilePrefab, firePoint.position, Quaternion.identity);
            var projectile = projGO.GetComponent<TurretProjectile>();
            projectile.Initialize(targetType, direction.normalized, projectileDamage, projectileSpeed, projectileDuration, obstacleLayers, ReturnProjectileToPool);
        }

        private void ReturnProjectileToPool(TurretProjectile proj)
        {
            proj.destroyEvent.RemoveAllListeners();
            PoolManager.Instance?.ReturnToPool(proj.gameObject, projectilePrefab);
        }

        private void FindClosestEnemy()
        {
            GameObject[] enemies = GameObject.FindGameObjectsWithTag("Enemy");
            float minDistance = Mathf.Infinity;
            target = null;

            foreach (var e in enemies)
            {
                if (e == this.gameObject) continue;

                EnemyHealth enemyHealth = e.GetComponent<EnemyHealth>();
                if (enemyHealth == null) continue;

                // Skip dead enemies
                if (enemyHealth.IsDead) continue;

                // Check if enemy is in range before calculating distance
                if (!IsTargetInRange(e.transform.position)) continue;

                // Check line of sight
                if (!HasLineOfSight(e.transform.position)) continue;

                float dist = Vector3.Distance(transform.position, e.transform.position);
                if (dist < minDistance)
                {
                    minDistance = dist;
                    target = e.transform;
                }
            }
        }

        // Draw Gizmos
        private void OnDrawGizmosSelected()
        {
            if (!displayGizmos) return;

            Gizmos.color = Color.yellow;

            switch (rangeShape)
            {
                case RangeShape.Sphere:
                    DrawSphereGizmo();
                    break;

                case RangeShape.Cube:
                    DrawCubeGizmo();
                    break;

                case RangeShape.Cone:
                    DrawConeGizmo();
                    break;
            }
        }

        private void DrawSphereGizmo()
        {
            Gizmos.matrix = Matrix4x4.TRS(transform.position, Quaternion.identity, Vector3.one);
            Gizmos.DrawWireSphere(Vector3.zero, detectionRadius);
        }

        private void DrawCubeGizmo()
        {
            Gizmos.matrix = Matrix4x4.TRS(transform.position, transform.rotation, Vector3.one);
            Gizmos.DrawWireCube(Vector3.zero, cubeBounds);
        }

        private void DrawConeGizmo()
        {
            Vector3 forward;

            if (coneStaticDirection)
            {
                Quaternion rotation = Quaternion.Euler(coneVerticalAngle, coneHorizontalAngle, 0f);
                forward = transform.TransformDirection(rotation * Vector3.forward);
            }
            else
            {
                forward = turretHead != null ? turretHead.forward : transform.forward;
            }

            Vector3 origin = transform.position;

            // Calculate cone lines
            int segments = 16;
            float halfAngle = coneAngle / 2f;

            Vector3 perpendicular = Vector3.Cross(forward, Vector3.up);
            if (perpendicular.sqrMagnitude < 0.001f)
            {
                perpendicular = Vector3.Cross(forward, Vector3.right);
            }
            perpendicular.Normalize();

            Vector3[] directions = new Vector3[segments + 1];
            for (int i = 0; i <= segments; i++)
            {
                float angle = (i / (float)segments) * 360f;

                Vector3 rotated = Quaternion.AngleAxis(angle, forward) * perpendicular;
                Vector3 direction = Quaternion.AngleAxis(halfAngle, rotated) * forward;
                directions[i] = direction;
            }

            // Draw cone circle
            for (int i = 0; i < segments; i++)
            {
                Gizmos.DrawLine(origin + directions[i] * coneRange,
                              origin + directions[i + 1] * coneRange);
            }

            // Draw lines from origin to cone edge
            for (int i = 0; i <= segments; i += segments / 4)
            {
                Gizmos.DrawLine(origin, origin + directions[i] * coneRange);
            }

            Gizmos.DrawLine(origin, origin + forward * coneRange);
        }
    }
}