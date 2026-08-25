// IShootStyle.cs
// 射击样式接口(策略模式)：定义所有射击样式必须实现的统一射击入口。
// 默认由 HitscanShootStyle、ProjectileShootStyle、MeleeShootStyle 与 CustomShootStyle 实现，
// 并在 WeaponController 的 Shoot() 方法中被调用，以切换不同的射击策略。
using System;
using UnityEngine;

namespace cowsins
{
    /// <summary>
    /// Defines actions & Callbacks of different Shoot Styles.
    /// By Default, HitscanShootStyle, ProjectileShootStyle & MeleeShootStyle implement IShootStyle. These are called in WeaponController´s Shoot() method.
    /// </summary>
    public interface IShootStyle
    {
        /// <summary>执行一次射击：由各射击样式按自己的策略实现(即时命中/投射物/近战/自定义)。</summary>
        /// <param name="spread">散布角度，用于随机偏移射击方向。</param>
        /// <param name="damageMultiplier">伤害倍率。</param>
        /// <param name="shakeMultiplier">相机抖动倍率。</param>
        void Shoot(float spread, float damageMultiplier, float shakeMultiplier);
        // 射击事件：实际射击发生时广播，供 WeaponController 订阅
        event Action onShoot;
    }
}