using System;
using GamePlay.Player;
using UnityEngine;

namespace GamePlay.HoldSystem
{
    /// <summary>武器预制体的入口，将模型表现绑定到所属角色的运行时武器。</summary>
    [DisallowMultipleComponent]
    public abstract class WeaponController : MonoBehaviour
    {
        public PlayerHolder Holder { get; private set; }
        public WeaponBase Weapon { get; private set; }
        public WeaponEntry Entry { get; private set; }
        public IWeaponPresentationResources PresentationResources => Entry?.PresentationResources;

        /// <summary>装备前检查逻辑对象是否与这个模型控制器匹配。</summary>
        public abstract bool CanBind(WeaponBase weapon);

        /// <summary>在实例化之后调用；Awake/OnEnable 不应依赖尚未绑定的武器。</summary>
        public void Bind(PlayerHolder holder, WeaponEntry entry)
        {
            if (holder == null)
                throw new ArgumentNullException(nameof(holder));
            if (entry == null)
                throw new ArgumentNullException(nameof(entry));
            if (Weapon != null)
                throw new InvalidOperationException("武器控制器已经绑定，请先解绑。");
            if (!CanBind(entry.Weapon))
                throw new ArgumentException("武器逻辑与模型控制器不匹配。", nameof(entry));

            Holder = holder;
            Weapon = entry.Weapon;
            Entry = entry;
            OnBound();
        }

        /// <summary>释放表现层订阅；可以重复调用。</summary>
        public void Unbind()
        {
            if (Weapon == null)
                return;
            try
            {
                OnUnbinding();
            }
            finally
            {
                Weapon = null;
                Entry = null;
                Holder = null;
            }
        }

        protected virtual void OnBound() { }
        protected virtual void OnUnbinding() { }
        protected virtual void OnDestroy() => Unbind();
    }
}
