// ----------------------------------------------------------------------
// BulletTypeIdentifier_SO.cs
// 作用：定义“子弹类型标识”的 ScriptableObject 资源。它继承自 Item_SO，
//       通过 bulletType 枚举区分“自定义子弹”与“通用子弹”，
//       用于拾取物（BulletsPickeable）与武器子弹类型之间的匹配判定。
// ----------------------------------------------------------------------
using UnityEngine;

namespace cowsins
{
    [CreateAssetMenu(fileName = "newBulletsInventoryItem", menuName = "COWSINS/New Bullet Identifier", order = 1)]
    /// <summary>
    /// 子弹类型标识资源。继承自 Item_SO，用于在运行时标识一类子弹，
    /// 通过 BulletType 枚举标记其是自定义类型还是通用类型，
    /// 供武器系统与拾取系统进行子弹兼容性匹配。
    /// </summary>
    public class BulletTypeIdentifier_SO : Item_SO
    {
        // 子弹类型枚举：Custom 表示自定义/特定子弹，Universal 表示通用子弹（可匹配任意武器）。
        public enum BulletType { Custom, Universal }

        // 当前资源所代表的子弹类型。
        public BulletType bulletType;
    }
}