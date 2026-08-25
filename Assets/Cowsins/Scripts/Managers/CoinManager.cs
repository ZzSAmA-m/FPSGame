// 文件：CoinManager.cs
// 作用：管理玩家的金币系统：持有金币数量、增减金币、判断是否足够并扣款购买、重置金币。
//       作为单例供全局访问，并在金币数量变化时通过 UIEvents.onCoinsChange 事件通知 UI 更新。
using UnityEngine;

namespace cowsins
{
    /// <summary>
    /// 金币管理器：负责金币数量的增减、购买扣款与重置，并广播金币变化事件。
    /// </summary>
    public class CoinManager : MonoBehaviour
    {
        // CoinManager 的单例实例，供全局访问。
        public static CoinManager Instance; // Singleton instance of the CoinManager

        // 是否启用金币系统（可在 Inspector 中配置）。
        [Tooltip("Flag to indicate if the game uses coins.")]
        public bool useCoins;

        // 当前持有金币数量（外部只读，只能通过本类方法修改）。
        public int coins { get; private set; } // Property to access the coin count (read-only from outside)

        // 初始化单例：若已存在实例则销毁自身，否则将自身设为唯一实例。
        private void Awake()
        {
            if (Instance == null) // Check if an instance already exists
            {
                Instance = this; // Set the instance to this object
            }
            else
            {
                Destroy(this.gameObject); // Another instance exists, destroy this one
            }
        }

        // 启动时广播一次当前金币数量，初始化 UI 显示（第二个参数 false 表示不刷新面板动画）。
        private void Start()
        {
            UIEvents.onCoinsChange?.Invoke(coins, false);
        }

        /// <summary>
        /// 增加金币（重载：默认不刷新金币面板 UI）。
        /// </summary>
        /// <param name="amount">要增加的金币数量（内部会取绝对值）。</param>
        public void AddCoins(int amount) => AddCoins(amount, false);

        /// <summary>
        /// 增加金币：将绝对值 amount 累加到总数，并广播金币变化事件。
        /// </summary>
        /// <param name="amount">要增加的金币数量。</param>
        /// <param name="updateCoinsPanel">是否同步刷新金币面板 UI。</param>
        // Add coins to the total count
        public void AddCoins(int amount, bool updateCoinsPanel)
        {
            coins += Mathf.Abs(amount); // Add positive amount of coins
            UIEvents.onCoinsChange?.Invoke(coins, updateCoinsPanel);
        }


        /// <summary>
        /// 扣除金币：减去绝对值 amount，并确保金币数量不会小于 0，然后广播金币变化事件。
        /// </summary>
        /// <param name="amount">要扣除的金币数量。</param>
        /// <param name="updateCoinsPanel">是否同步刷新金币面板 UI。</param>
        // Remove coins from the total count
        public void RemoveCoins(int amount, bool updateCoinsPanel)
        {
            coins -= Mathf.Abs(amount); // Subtract positive amount of coins

            if (coins <= 0) // Ensure coins don't go negative
            {
                coins = 0; // Set coins to zero if they go below
            }
            UIEvents.onCoinsChange?.Invoke(coins, updateCoinsPanel);
        }

        /// <summary>
        /// 判断当前金币是否足以支付指定数量。
        /// </summary>
        /// <param name="amount">要校验的金币数量。</param>
        /// <returns>金币足够时返回 true，否则返回 false。</returns>
        // Check if there are enough coins
        public bool CheckIfEnoughCoins(int amount)
        {
            return coins >= amount; // Return whether there are enough coins
        }

        /// <summary>
        /// 尝试购买：若金币足够则扣除相应数量并返回 true（购买成功），否则返回 false（不扣款）。
        /// </summary>
        /// <param name="amount">购买所需金币数量。</param>
        /// <param name="updateCoinsPanel">是否同步刷新金币面板 UI。</param>
        /// <returns>购买成功返回 true，金币不足返回 false。</returns>
        // Purchase action, checking and deducting coins if sufficient
        public bool CheckIfEnoughCoinsAndPurchase(int amount, bool updateCoinsPanel)
        {
            if (coins >= amount) // Check if enough coins are available
            {
                RemoveCoins(amount, updateCoinsPanel); // Deduct coins from the total
                return true; // Purchase successful
            }
            return false; // Not enough coins for purchase
        }

        /// <summary>
        /// 将金币数量重置为 0。
        /// </summary>
        // Reset the coin count to zero
        public void ResetCoins()
        {
            coins = 0; // Set coins to zero
        }
    }
}
