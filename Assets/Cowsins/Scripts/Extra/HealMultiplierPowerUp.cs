/// <summary>
/// This script belongs to cowsinsT as a part of the cowsins FPS Engine. All rights reserved. 
/// </summary>
// HealMultiplierPowerUp（治疗倍率增益道具）：
// 继承自 PowerUp，被拾取后给玩家的“治疗量倍率”追加一个可叠加的加成修正，
// 并在生效后立即销毁自身。用于实现“拾取后一段时间内治疗效果增强”的玩法。
using UnityEngine;
namespace cowsins
{
    /// <summary>
    /// 治疗倍率增益道具：拾取后为玩家的治疗倍率增加一个加法修正，然后销毁自身。
    /// </summary>
    public class HealMultiplierPowerUp : PowerUp
    {
        // 自定义区段标题，并序列化治疗倍率追加的数值
        [Header("CUSTOM"), SerializeField]
        private float healMultiplierAddition;

        // 玩家交互（拾取）时：先执行基类逻辑，再追加治疗倍率修正并销毁道具
        public override void Interact(PlayerDependencies player)
        {
            base.Interact(player);
            player.PlayerMultipliers.HealMultiplier.AddModifier(new StatModifier(healMultiplierAddition, StatModifierType.Additive, this));
            Destroy(this.gameObject);
        }
    }
}
