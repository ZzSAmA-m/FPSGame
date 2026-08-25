// ============================================================
// CircularTargetEnemy.cs —— 沿直线往返移动的圆形靶子敌人示例
// 继承自 EnemyHealth，演示如何在基础敌人脚本上扩展自定义行为：
// 该敌人沿设定方向匀速移动，到达方向持续时间后反向折返，
// 被击杀后经过 timeToRevive 秒自动复活并恢复满血 / 满盾。
// 文件末尾附带仅在编辑器下生效的自定义 Inspector 绘制代码。
// ============================================================
using UnityEngine;
using cowsins;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace cowsins
{
    /// <summary>
    /// Another example of Enemy.cs inheritance.
    /// </summary>
    /// <summary>圆形靶子敌人：沿设定方向往返移动，被击杀后可自动复活的敌人示例。</summary>
    public class CircularTargetEnemy : EnemyHealth
    {
        // 移动方向：到达折返点后会自动反向。
        [Tooltip("Direction of the movement. Whenever it reaches the end, it will go the opposite direction"), SerializeField] private Vector3 movementDirection;

        // 保持同一方向移动的持续时间（秒），结束后折返。
        [Tooltip("Amount of time that the target will keep moving on the same direction"), SerializeField] private float directionDuration;

        // 移动速度（目标速度大小）。
        [Tooltip("Target velocity magnitude"), SerializeField] private float speed;

        // 被击杀后到自动复活之间的等待时间（秒）。
        [SerializeField] private float timeToRevive;

        // 当前实际移动方向（运行期会随折返而反向）。
        private Vector3 movementDir;

        // 方向倒计时，归零后切换移动方向。
        private float movementTimer;

        // 缓存的 Collider 引用，用于击杀后禁用碰撞。
        private Collider col;


        // 重写 Start：先调用基类初始化，再设置默认移动方向、计时器并缓存 Collider。
        public override void Start()
        {
            // Since we override the start method, make sure to call the base function
            base.Start();

            // Set the default movement variables
            movementDir = movementDirection;
            movementTimer = directionDuration;

            col = GetComponent<Collider>(); 
        }

        // 每帧更新：死亡则跳过移动；否则按当前方向匀速移动并处理折返计时。
        public void Update()
        {
            // If the enemy is dead, stop calling the movement 
            if (isDead) return;

            // Apply the position
            transform.position = Vector3.MoveTowards(transform.position, transform.position + transform.TransformDirection(movementDir), speed * Time.deltaTime);

            // Timer related code.
            // Once the timer is equal or less than 0, the movement should be reset and change directions
            movementTimer -= Time.deltaTime;
            if (movementTimer <= 0) ResetMovement();
        }

        // Change directions and reset timer
        // 折返：反转移动方向并重置方向计时器。
        private void ResetMovement()
        {
            movementDir = -movementDir;
            movementTimer = directionDuration;
        }
        // Simple damage function override
        // 重写 Damage：死亡则忽略；否则调用基类伤害处理。
        public override void Damage(float damage, bool isHeadshot)
        {
            if (isDead) return;
            base.Damage(damage, isHeadshot);
        }
        // Simple Die function override
        // 重写 Die：标记死亡、触发死亡事件、禁用子物体与碰撞、播放音效并定时复活。
        public override void Die()
        {
            if (isDead) return; // If the target is already dead, dont call this again
                                // Set to dead
            isDead = true;
            // Call custom event
            events.OnDeath.Invoke();

            // Disable any possible hit effects
            for (int i = 0; i < transform.childCount; i++)
            {
                var child = transform.GetChild(i).gameObject;
                if (child != null)
                    child.SetActive(false);
            }

            col.enabled = false;
            SoundManager.Instance.PlaySound(dieSFX, 0, 0, false);
            // Invoke revive method
            Invoke("Revive", timeToRevive);
        }
        // 复活：解除死亡状态、恢复生命与护盾、重新启用子物体与碰撞。
        private void Revive()
        {
            // Revive
            isDead = false;
            // Reset variables related to health
            health = maxHealth;
            shield = maxShield;

            for (int i = 0; i < transform.childCount; i++)
            {
                var child = transform.GetChild(i).gameObject;
                if (child != null)
                    child.SetActive(true);
            }

            col.enabled = true;
        }
    }

#if UNITY_EDITOR
    [CustomEditor(typeof(CircularTargetEnemy))]
    /// <summary>CircularTargetEnemy 的自定义 Inspector：展示该靶子敌人的相关可配置参数。</summary>
    public class CircularTargetEnemyEditor : Editor
    {
        /// <summary>绘制自定义 Inspector：分组显示移动方向、持续时间、速度与复活时间等参数。</summary>
        override public void OnInspectorGUI()
        {
            serializedObject.Update();
            var myScript = target as CircularTargetEnemy;

            EditorGUILayout.LabelField("CIRCULAR TARGET", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("This Component uses a custom inspector to display relevant variables regarding the Circular Target Enemy. It is worth to mention " +
                "that this script inherits from Enemy.cs, so it is a damageable object.", EditorStyles.helpBox);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("movementDirection"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("directionDuration"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("speed"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("timeToRevive"));
            serializedObject.ApplyModifiedProperties();

        }
    }
#endif
}