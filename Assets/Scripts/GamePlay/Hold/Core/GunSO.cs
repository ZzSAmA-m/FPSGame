using GamePlay.HoldSystem;
using UnityEngine;
namespace GamePlay.HoldSystem
{
    public class GunSO:WeaponSO
    {
        [SerializeField, Min(0)] protected int _initialTotalBullets = GunConfig.Default.InitialTotalBullets;

        /// <summary>单个弹匣的容量。</summary>
        [SerializeField, Min(1)] protected int _capacity = GunConfig.Default.Capacity;

        /// <summary>准备动作的 Tick 数。</summary>
        [SerializeField, Min(1)] protected int _prepareTicks = (int)GunConfig.Default.PrepareTicks;

        /// <summary>两次射击之间的 Tick 数。</summary>
        [SerializeField, Min(1)] protected int _shotIntervalTicks = (int)GunConfig.Default.ShotIntervalTicks;

        /// <summary>换弹动作的 Tick 数。</summary>
        [SerializeField, Min(1)] protected int _reloadTicks = (int)GunConfig.Default.ReloadTicks;

        /// <summary>限制 Inspector 中的参数范围。</summary>
        protected void OnValidate()
        {
            _initialTotalBullets = Mathf.Max(0, _initialTotalBullets);
            _capacity = Mathf.Max(1, _capacity);
            _prepareTicks = Mathf.Max(1, _prepareTicks);
            _shotIntervalTicks = Mathf.Max(1, _shotIntervalTicks);
            _reloadTicks = Mathf.Max(1, _reloadTicks);
        }
        /// <summary>从当前参数生成一份固定配置。</summary>
        public GunConfig CreateConfig()
        {
            return new GunConfig(_initialTotalBullets, _capacity, (uint)_prepareTicks, (uint)_shotIntervalTicks, (uint)_reloadTicks);
        }
    }

}
