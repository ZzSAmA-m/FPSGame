// 文件：PoolManager.cs
// 作用：对象池管理器：按预制体预先创建并缓存一组游戏对象（如子弹、命中特效等），
//       通过启用/禁用而非频繁实例化/销毁来复用对象，从而降低 GC 压力、提升性能。
//       同时内置了对特效（ParticleSystem）播完自动归还池子的支持。
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace cowsins
{
    /// <summary>
    /// 对象池管理器：负责对象池的注册、取用、归还、扩容与清理的单例。
    /// </summary>
    public class PoolManager : MonoBehaviour
    {
        // 单例实例。
        public static PoolManager Instance;

        // 未预先注册时使用的默认池容量。
        [SerializeField, Title("Pool Sizes")] private int defaultSize = 5;
        // 各类对象的池容量（武器特效 / 伤害数字 / 子弹拖尾等）。
        [SerializeField] private int weaponEffectsSize;
        [SerializeField] private int damagePopUpsSize;
        [SerializeField] private int bulletGraphicsSize;

        // 默认归还延迟时间（秒）。
        [SerializeField, Title("Return Times", upMargin = 10)] private float defaultReturnTime = 3f;
        // 伤害数字的归还延迟时间（秒）。
        [SerializeField] private float damagePopUpsReturnTime = .4f;    

        // GETTERS
        // 以下为各池容量与归还时间的只读访问属性。
        public int WeaponEffectsSize => weaponEffectsSize;
        public int DamagePopUpsSize => damagePopUpsSize;
        public int BulletGraphicsSize => bulletGraphicsSize;
        public float DefaultReturnTime => defaultReturnTime;
        public float DamagePopUpsReturnTime => damagePopUpsReturnTime;

        // INTERNAL USE
        // 以预制体为 key 的空闲对象队列（池本体）。
        private Dictionary<GameObject, Queue<GameObject>> poolDictionary = new Dictionary<GameObject, Queue<GameObject>>();
        // 以预制体为 key 的当前活跃对象列表，用于统一清理。
        private Dictionary<GameObject, List<GameObject>> activeObjects = new Dictionary<GameObject, List<GameObject>>();

        // 初始化单例：若不存在实例则设为 Instance，否则销毁自身。
        private void Awake()
        {
            // Initialize singleton
            if (Instance == null) Instance = this;
            else Destroy(this.gameObject);
        }

        /// <summary>
        /// Registers a new pool given a prefab and the size of the collection.
        /// </summary>
        /// <param name="prefab">作为池模板的预制体。</param>
        /// <param name="size">初始池容量（预创建的实例数量）。</param>
        public void RegisterPool(GameObject prefab, int size)
        {
            if (prefab == null)
            {
                return;
            }
            // Avoid registering duplicates
            if (poolDictionary.ContainsKey(prefab))
            {
                return;
            }

            Queue<GameObject> objectQueue = new Queue<GameObject>();
            List<GameObject> activeList = new List<GameObject>();

            for (int i = 0; i < size; i++)
            {
                GameObject obj = Instantiate(prefab, transform);
                obj?.SetActive(false);
                objectQueue.Enqueue(obj);
            }

            poolDictionary[prefab] = objectQueue;
            activeObjects[prefab] = activeList;
        }
        /// <summary>
        /// 从池中取出一个对象（重载：使用默认归还延迟时间）。
        /// </summary>
        /// <param name="prefab">对象预制体。</param>
        /// <param name="position">放置位置。</param>
        /// <param name="rotation">放置旋转。</param>
        /// <returns>取出的对象；失败时返回 null。</returns>
        public GameObject GetFromPool(GameObject prefab, Vector3 position, Quaternion rotation)
        {
            return GetFromPool(prefab, position, rotation, defaultReturnTime);
        }

        /// <summary>
        /// 从池中取出一个对象并激活；若池为空则扩容后重试，并对特效自动安排延迟归还。
        /// </summary>
        /// <param name="prefab">对象预制体。</param>
        /// <param name="position">放置位置。</param>
        /// <param name="rotation">放置旋转。</param>
        /// <param name="returnToPool">自动归还延迟（秒），&lt;=0 表示不自动归还。</param>
        /// <returns>取出的对象；失败时返回 null。</returns>
        public GameObject GetFromPool(GameObject prefab, Vector3 position, Quaternion rotation, float returnToPool)
        {
            if (prefab == null)
            {
                return null;
            }
            // 若该预制体尚未注册池，则用默认容量补注册。
            if (!poolDictionary.ContainsKey(prefab))
            {
                // Default size if not preregistered
                RegisterPool(prefab, defaultSize);
            }

            // 从队列中取出可用对象（跳过已被销毁的空引用）。
            while (poolDictionary[prefab].Count > 0)
            {
                GameObject obj = poolDictionary[prefab].Dequeue();
                if (obj != null) 
                {
                    obj.transform.position = position;
                    obj.transform.rotation = rotation;
                    obj.SetActive(true);

                    if (activeObjects.ContainsKey(prefab))
                        activeObjects[prefab].Add(obj);

                    HandleVFXAutoReturn(obj, prefab, returnToPool);
                    return obj;
                }
            }

            // 队列耗尽：扩容后再递归取一次。
            ExpandPool(prefab);
            return GetFromPool(prefab, position, rotation, returnToPool);
        }

        /// <summary>
        /// 将对象归还到对应池中：禁用对象、重新挂回管理器节点并重新入队。
        /// </summary>
        /// <param name="obj">要归还的对象。</param>
        /// <param name="prefab">该对象所属池的预制体。</param>
        public void ReturnToPool(GameObject obj, GameObject prefab)
        {
            if (obj == null || prefab == null) return;

            // 若该预制体的池已不存在，则直接销毁对象。
            if (!poolDictionary.ContainsKey(prefab))
            { 
                // Pool for this prefab no longer exists, destroy the object.
                Destroy(obj);
                return;
            }

            obj.SetActive(false);
            obj.transform.SetParent(this.transform);
            poolDictionary[prefab].Enqueue(obj);

            if (activeObjects.ContainsKey(prefab))
                activeObjects[prefab].Remove(obj);
        }

        // 池容量不足时扩容：再实例化一个对象（初始为禁用）加入队列。
        private void ExpandPool(GameObject prefab)
        {
            GameObject obj = Instantiate(prefab, transform);
            obj.SetActive(false);
            poolDictionary[prefab].Enqueue(obj);
        }

        // 处理取用对象的自动归还：若带 ParticleSystem 则播放并按特效时长归还，否则按给定延迟归还。
        private void HandleVFXAutoReturn(GameObject obj, GameObject prefab, float returnToPool)
        {
            ParticleSystem ps = obj.GetComponent<ParticleSystem>();
            if (ps != null)
            {
                ps.Play();
                // Automatically return to pool after VFX duration
                if (returnToPool > 0) StartCoroutine(ReturnAfterDelay(obj, prefab, ps.main.duration));
            }
            else
            {
                // If it's not a VFX, use a default delay
                if (returnToPool > 0) StartCoroutine(ReturnAfterDelay(obj, prefab, returnToPool));
            }
        }

        // 协程：等待指定延迟后把对象归还到池中。
        private IEnumerator ReturnAfterDelay(GameObject obj, GameObject prefab, float delay)
        {
            yield return new WaitForSeconds(delay);

            ReturnToPool(obj, prefab);
        }

        /// <summary>
        /// Removes all objects from a specific pool.
        /// </summary>
        /// <param name="prefab">要移除池的预制体。</param>
        public void RemovePool(GameObject prefab)
        {
            if (!poolDictionary.ContainsKey(prefab))
            {
                return;
            }

            // Disable and destroy active objects
            foreach (var obj in activeObjects[prefab])
            {
                Destroy(obj);
            }
            activeObjects.Remove(prefab);

            // Destroy all pooled objects
            foreach (var obj in poolDictionary[prefab])
            {
                Destroy(obj);
            }
            poolDictionary.Remove(prefab);
        }
    }
}

#if UNITY_EDITOR
namespace cowsins
{

    /// <summary>
    /// PoolManager 的自定义 Inspector 编辑器：绘制默认字段并在底部显示说明文字。
    /// </summary>
    [CustomEditor(typeof(PoolManager))]
    public class PoolManagerEditor : Editor
    {
        // 自定义 Inspector 绘制：先绘制默认检视面板，再追加对象池说明信息。
        override public void OnInspectorGUI()
        {
            serializedObject.Update();
            var myScript = target as PoolManager;

            DrawDefaultInspector();

            EditorGUILayout.Space(15);
            EditorGUILayout.LabelField("Pool Manager Information", EditorStyles.boldLabel);
            
            EditorGUILayout.LabelField("Pool Manager is a system that enhances the performance of your game. Instead of constantly instantiating and destroying objects such as bullets, " +
            "impacts, or other visual effects, the Pool Manager stores an array of them and enables and disables them based on the Player needs, reducing Garbage Collection overhead and enhances overall efficiency.", EditorStyles.helpBox);

            serializedObject.ApplyModifiedProperties();
        }
    }
}
#endif