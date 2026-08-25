// ============================================================
// IDamageable.cs —— 可受击（可承受伤害）接口定义
// 定义统一的伤害接口 Damage(float damage, bool isHeadshot)。
// 玩家（PlayerStats）、敌人（EnemyHealth）、可破坏物（Destructible）
// 等任何需要“被子弹 / 爆炸命中”的对象均可实现该接口，
// 从而使武器、炮台、爆炸等各类伤害来源能够通过同一接口对目标造成伤害。
// ============================================================
/// <summary>
/// This script belongs to cowsins™ as a part of the cowsins´ FPS Engine. All rights reserved. 
/// </summary>
namespace cowsins
{
    /// <summary>
    /// Used for Player and enemies, which can be hit
    /// </summary>
    /// <summary>可受击接口：玩家与敌人都可实现此接口，从而统一接收伤害。</summary>
    // 接口方法：对目标施加伤害；damage 为伤害数值，isHeadshot 表示该次命中是否为爆头。
    public interface IDamageable { void Damage(float damage, bool isHeadshot); }
}
