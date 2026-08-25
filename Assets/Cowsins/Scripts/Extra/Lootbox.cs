// Lootbox（战利品箱）：
// 继承自 Interactable，玩家可花费金币开启箱子，延迟一段时间后随机刷出一件战利品，
// 并播放开箱动画与音效。支持自定义战利品表、刷新角度/距离，以及按玩家位置确定刷新方向。
using UnityEngine;
using System.Collections;

namespace cowsins
{
    /// <summary>
    /// 战利品箱：消耗金币后延迟掉落随机战利品，并播放开箱表现。
    /// </summary>
    public class Lootbox : Interactable
    {
        // 开箱所需金币价格（0 表示免费）
        [Title("LOOTBOX", upMargin: 8),SerializeField, Min(0)] private int price;

        // 可掉落的战利品对象数组
        [SerializeField] private GameObject[] loot;

        // 从支付到掉落战利品的延迟时间
        [SerializeField] private float delayToReceiveLoot;

        // 战利品刷新的最小/最大水平角度及刷新的距离
        [SerializeField] private float minSpawnAngle, maxSpawnAngle, spawnDistance;

        // 战利品刷新方向是否取决于玩家相对箱子的位置
        [SerializeField] private bool directionDependsOnPlayerPosition;

        // 开箱动画组件
        private Animation anim;

        // 开箱音效源
        private AudioSource audioSource;

        // 记录基础交互提示文本（不含价格后缀）
        private string baseInteractText;
        // 初始化：获取动画与音源，记录基础提示文本，并在有价格时追加价格显示
        private void Start()
        {
            anim = GetComponent<Animation>();
            audioSource = GetComponent<AudioSource>();
            baseInteractText = interactText;
            if (price != 0) interactText += $" [{price}]";
        }
        // 高亮时根据金币是否足够把价格文本显示为绿色或红色
        public override void Highlight()
        {
            if (price == 0) return;

            if (CoinManager.Instance.CheckIfEnoughCoins(price))
                interactText = baseInteractText + "<color=#" + ColorUtility.ToHtmlStringRGB(Color.green) + ">" + $" [{price}]" + "</color>";
            else interactText = "<color=#" + ColorUtility.ToHtmlStringRGB(Color.red) + ">" + $" [{price}]" + "</color>";
        }
        // 玩家交互：金币足够（或免费）时执行基类交互并开启掉落协程
        public override void Interact(Transform player)
        {
            if (price != 0 && CoinManager.Instance.useCoins && CoinManager.Instance.CheckIfEnoughCoins(price) || price == 0)
            {
                base.Interact(player);
                StartCoroutine(GetLoot(player));
            }
        }

        // 掉落协程：扣除金币、等待延迟后随机刷新战利品并播放开箱表现，最后销毁交互组件
        private IEnumerator GetLoot(Transform player)
        {
            if (price != 0)
            {
                // 扣除金币并通知 UI 刷新金币显示
                CoinManager.Instance.RemoveCoins(price, false);
                UIEvents.onCoinsChange?.Invoke(CoinManager.Instance.coins, false);
            }
            yield return new WaitForSeconds(delayToReceiveLoot);
            GameObject lootObject = null;

            // 从战利品表随机抽取一件
            lootObject = loot[Random.Range(0, loot.Length)];

            SpawnSelectedLoot(lootObject, player);

            anim.Play();
            audioSource.Play();

            Destroy(GetComponent<Lootbox>());
            gameObject.layer = LayerMask.NameToLayer("Default");
            StopAllCoroutines();
        }

        // 在计算出的位置与朝向上实例化选中的战利品
        private void SpawnSelectedLoot(GameObject loot, Transform player)
        {
            float spawnAngle = Random.Range(minSpawnAngle, maxSpawnAngle);
            Quaternion spawnRotation = Quaternion.Euler(0f, spawnAngle, 0f); // Rotate around the y-axis
            player = GameObject.FindGameObjectWithTag("Player").transform;
            // 根据配置决定刷新方向：朝向玩家位置，或按旋转后的箱子局部轴方向
            Vector3 spawnDirection = directionDependsOnPlayerPosition ? (player.position - transform.position).normalized * spawnDistance : spawnRotation * -transform.right;

            Vector3 spawnPosition = transform.position + spawnDirection * spawnDistance;

            var instantiatedLoot = Instantiate(loot, spawnPosition, spawnRotation);
            // 若战利品带 Identifiable 组件，则为其生成唯一 ID 以便存档
            if (instantiatedLoot.TryGetComponent<Identifiable>(out var identifiable))
            {
                identifiable.GenerateUniqueID();
#if SAVE_LOAD_ADD_ON
                identifiable.SaveInstance();
#endif
            }
        }

#if SAVE_LOAD_ADD_ON
        // Only one possible interaction state: Open
        // 存档加载后恢复“已开启”状态：播放动画并销毁交互组件
        public override void LoadedState()
        {
            anim.Play();
            Destroy(GetComponent<Lootbox>());
            // To avoid the InteractManager from detecting it as an Interactable
            gameObject.layer = LayerMask.NameToLayer("Default");
        }
#endif

        // 仅在选中时绘制刷新方向的辅助线，便于在场景中可视化调整角度范围
        private void OnDrawGizmosSelected()
        {
            if (directionDependsOnPlayerPosition) return;

            Gizmos.color = Color.blue;

            Vector3 forward = -transform.right * spawnDistance;

            Quaternion minRotation = Quaternion.Euler(0f, minSpawnAngle, 0f);
            Vector3 minDirection = minRotation * forward;

            Quaternion maxRotation = Quaternion.Euler(0f, maxSpawnAngle, 0f);
            Vector3 maxDirection = maxRotation * forward;

            Gizmos.DrawLine(transform.position, transform.position + minDirection);
            Gizmos.DrawLine(transform.position, transform.position + maxDirection);
        }
    }
}