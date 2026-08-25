// 文件：ExperienceManager.cs
// 作用：管理玩家的经验值与等级系统：累计经验、扣除经验、根据经验阈值自动升级/降级，
//       并提供查询当前等级与当前等级内经验进度的方法。
using UnityEngine;

namespace cowsins
{
    /// <summary>
    /// 经验管理器：负责经验累计、等级升降与经验进度查询的单例。
    /// </summary>
    public class ExperienceManager : MonoBehaviour
    {
        // Singleton pattern to ensure that there is only one ExperienceManager instance in the game.
        // 单例实例，供全局访问。
        public static ExperienceManager Instance;
        // 是否启用经验系统。
        public bool useExperience;
        // 玩家当前等级（内部索引，从 0 开始）。
        public int playerLevel;
        // 每个等级所需累计经验阈值数组。
        public int[] experienceRequirements;

        // 玩家累计获得的总经验值。
        private float totalExperience;

        // 对外暴露总经验值（只读）。
        public float TotalExperience => totalExperience;

        // 初始化单例：仅在不存在实例时把自身设为 Instance。
        private void OnEnable()
        {
            // If there is no existing ExperienceManager instance, then set this instance as the singleton.
            if (Instance == null) Instance = this;
        }

        /// <summary>
        /// 增加经验：累加 totalExperience 并检查是否触发升级。
        /// </summary>
        /// <param name="amount">要增加的经验值。</param>
        // add experience to the player.
        public void AddExperience(float amount)
        {
            // Increase the player's total experience.
            totalExperience += amount;

            // Check if the player has leveled up.
            CheckForLevelUp();
        }

        /// <summary>
        /// 扣除经验：减少 totalExperience（下限为 0）并检查是否触发降级。
        /// </summary>
        /// <param name="amount">要扣除的经验值。</param>
        // remove experience from the player.
        public void RemoveExperience(float amount)
        {
            // Reduce the player's total experience.
            totalExperience = Mathf.Max(totalExperience - amount, 0);

            // Check if the player has leveled down.
            CheckForLevelDown();
        }

        /// <summary>
        /// 将总经验值重置为 0。
        /// </summary>
        public void ResetExperience() => totalExperience = 0;

        // 检查是否升级：只要当前等级未达上限且总经验超过下一级阈值，就逐级提升等级。
        // check if the player has leveled up.
        private void CheckForLevelUp()
        {
            // While the player's level is less than the maximum level and their total experience is greater than the experience required for the next level, increase the player's level.
            while (playerLevel < experienceRequirements.Length - 1 && totalExperience >= experienceRequirements[playerLevel])
            {
                playerLevel++;
            }
        }

        // 检查是否降级：只要等级大于 0 且总经验低于当前等级阈值，就逐级降低等级。
        // check if the player has leveled down.
        private void CheckForLevelDown()
        {
            // While the player's level is greater than the minimum level and their total experience is less than the experience required for the current level, decrease the player's level.
            while (playerLevel > 0 && totalExperience < experienceRequirements[playerLevel])
            {
                playerLevel--;
            }
        }

        /// <summary>
        /// 获取玩家当前等级（索引从 0 开始，故 +1 返回人类可读的等级）。
        /// </summary>
        /// <returns>玩家当前等级。</returns>
        // get the player's level.
        public int GetPlayerLevel()
        {
            // Return the player's level plus one, since the level array starts at zero.
            return playerLevel + 1;
        }

        /// <summary>
        /// 获取玩家在当前等级内已积累的经验值（总经验减去上一级所需经验）。
        /// </summary>
        /// <returns>当前等级内的经验进度。</returns>
        // get the player's current experience.
        public float GetCurrentExperience()
        {
            // Calculate the player's current experience by subtracting the experience required for the previous level from their total experience.
            float previousLevelExperience = playerLevel > 0 ? experienceRequirements[playerLevel - 1] : 0;
            return totalExperience - previousLevelExperience;
        }
    }
}