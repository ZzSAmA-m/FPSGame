// IBullet.cs
// 定义子弹(弹丸)对象必须实现的基础属性接口。
// 所有投射物子弹(如 Bullet.cs 及自定义弹丸脚本)通过实现 IBullet 接口，
// 向 ProjectileShootStyle 等射击系统暴露统一的飞行、伤害、爆炸、暴击等配置属性。
using UnityEngine;

namespace cowsins
{
    /// <summary>子弹对象统一接口：声明弹丸飞行、伤害与爆炸所需的基础属性，供射击系统统一读写。</summary>
    public interface IBullet
    {
        // 弹丸飞行速度
        float Speed { get; set; }
        // 弹丸基础伤害
        float Damage { get; set; }
        // 弹丸目标点(命中点或落点)
        Vector3 Destination { get; set; }
        // 是否受重力影响
        bool Gravity { get; set; }
        // 发射该弹丸的玩家 Transform
        Transform Player { get; set; }
        // 是否会对玩家造成伤害
        bool HurtsPlayer { get; set; }
        // 命中时是否触发范围爆炸
        bool ExplosionOnHit { get; set; }
        // 爆炸特效预制体
        GameObject ExplosionVFX { get; set; }
        // 爆炸作用半径
        float ExplosionRadius { get; set; }
        // 爆炸施加给刚体的冲击力
        float ExplosionForce { get; set; }
        // 暴击伤害倍率
        float CriticalMultiplier { get; set; }
        // 弹丸存活时长(秒)，超时后自动销毁
        float Duration { get; set; }
    }
}