// Healthpack（医疗包）：
// 继承自 PowerUp，玩家交互时若未满血则恢复指定生命值，并标记为已使用、开始重新出现计时，
// 用于场景中可拾取的治疗道具。
/// <summary>
/// This script belongs to cowsins™ as a part of the cowsins´ FPS Engine. All rights reserved. 
/// </summary>
using UnityEngine;
namespace cowsins
{
    /// <summary>
    /// 医疗包：玩家交互时恢复生命值，并在已满血时忽略拾取。
    /// </summary>
    public class Healthpack : PowerUp
    {
        // 恢复的生命值数量
        [Tooltip("Amount of health to be restored")] [Range(.1f, 1000), SerializeField] private float healAmount;
        // 玩家交互：若未满血则恢复生命并标记已使用、启动重现实计时
        public override void Interact(PlayerDependencies player)
        {
            IPlayerStatsProvider playerStatsProvider = player.GetComponent<IPlayerStatsProvider>();
            // 已满血时直接返回，不消耗医疗包
            if (playerStatsProvider.IsFullyHealed()) return;
            used = true;
            timer = reappearTime;
            playerStatsProvider.Heal(healAmount);
        }
    }
}