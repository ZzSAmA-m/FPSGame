// CustomShootStyle.cs
// 自定义射击样式(策略模式实现之一)：当武器被设置为 Custom 射击样式时，
// WeaponIdentification 的 shootBehaviour 会被赋值为本类实例。
// 本类不自行处理命中与伤害，而是调用在 WeaponControllerSettings 中配置好的自定义 UnityEvent 回调，
// 射击冷却(射速)仍由本类通过 canShoot 标志与协程进行门控。
using UnityEngine;
using UnityEngine.Events;
using System.Collections;
using System;

namespace cowsins
{
    /// <summary>
    /// When a weapon is set to Custom Shoot Style, its shootBehaviour in WeaponIdentification is assigned to this CustomShootStyle.
    /// This is done when the weapon is unholstered.
    /// </summary>
    public class CustomShootStyle : IShootStyle
    {
        // 玩家依赖容器，用于获取武器引用、启动协程等
        private PlayerDependencies playerDependencies;
        // 当前武器的数据(ScriptableObject)
        private Weapon_SO weapon;
        // 当前武器的身份标识(含射速、枪口点等运行时数据)
        private WeaponIdentification id;
        // 武器控制器设置，内含自定义射击事件数组
        private WeaponControllerSettings settings;

        // 选定的自定义射击 UnityEvent 回调
        private UnityEvent customMethod;

        // 射击门控标志：为 false 时禁止射击，用于控制射速
        private bool canShoot = true;

        // Calls events in Weapon Controller when shooting or hitting an enemy ( CustomShootStyle does not handle hits so onHit is not used here,
        // but it is still required by IShootStyle.
        public event Action onShoot;
#pragma warning disable CS0067
        public event Action<int, float, RaycastHit, bool> onHit;
#pragma warning restore CS0067

        /// <summary>构造自定义射击样式实例：缓存依赖引用，并选定与当前武器匹配的自定义射击回调。</summary>
        /// <param name="playerDependencies">玩家依赖容器。</param>
        /// <param name="settings">武器控制器设置，包含自定义射击事件数组。</param>
        public CustomShootStyle(PlayerDependencies playerDependencies, WeaponControllerSettings settings)
        {
            this.playerDependencies = playerDependencies;
            this.weapon = playerDependencies.WeaponReference.Weapon;
            this.id = playerDependencies.WeaponReference.Id;
            this.settings = settings;

            // 在构造时即选定与当前武器匹配的自定义射击方法
            SelectCustomShotMethod();
        }

        /// <summary>执行一次自定义射击：调用选定的自定义射击方法并触发 onShoot 事件，非连发武器会进入射速冷却。</summary>
        /// <param name="spread">散布角度(本样式未直接使用该参数)。</param>
        /// <param name="damageMultiplier">伤害倍率(本样式未直接使用该参数)。</param>
        /// <param name="shakeMultiplier">相机抖动倍率(本样式未直接使用该参数)。</param>
        public void Shoot(float spread, float damageMultiplier, float shakeMultiplier)
        {
            // 冷却期间禁止射击，直接返回
            if (!canShoot) return;

            // 调用用户配置的自定义射击逻辑(若无则跳过)，随后广播射击事件
            customMethod?.Invoke();
            onShoot?.Invoke();

            // 非连发武器：射击后进入冷却，等待 fireRate 后再允许下一次射击
            if (!weapon.continuousFire)
            {
                canShoot = false;
                playerDependencies.StartCoroutine(AllowShootAfterDelay(id.fireRate));
            }
        }

        // 从 settings.customPrimaryShot 数组中查找与当前武器匹配的条目，
        // 并将其 OnShoot 事件赋给 customMethod，作为每次射击时调用的自定义逻辑。
        private void SelectCustomShotMethod()
        {
            // Iterate through each item in the array
            for (int i = 0; i < settings.customPrimaryShot.Length; i++)
            {
                // Assign the on shoot event to the unity event to call it each time we fire
                if (settings.customPrimaryShot[i].weapon == weapon)
                {
                    customMethod = settings.customPrimaryShot[i].OnShoot;
                    return;
                }
            }

            CowsinsUtilities.LogError("<b><color=yellow>Appropriate weapon ScriptableObject not found!</color></b> " +
                "Please configure the <b><color=cyan>Weapon ScriptableObject</color></b> and assign a suitable method " +
                "in the <b><color=green>Custom Shot Array (Events tab)</color></b> to fix this error.");
        }


        // 射击冷却协程：等待 delay 秒后将 canShoot 置回 true，重新允许射击。
        private IEnumerator AllowShootAfterDelay(float delay)
        {
            yield return new WaitForSeconds(delay);
            canShoot = true;
        }

        /// <summary>设置射击事件回调，供 WeaponController 订阅。</summary>
        /// <param name="event">要订阅的射击事件。</param>
        public void SetOnShootEvent(Action @event) => onShoot = @event;
    }
}