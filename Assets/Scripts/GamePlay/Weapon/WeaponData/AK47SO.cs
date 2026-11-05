using UnityEngine;

namespace GamePlay.Weapon
{
    /// <summary>配置 AK47 参数并创建独立枪械。</summary>
    [CreateAssetMenu(fileName = "AK47SO", menuName = "Scriptable Objects/AK47SO")]
    public class AK47SO : ScriptableObject
    {
        /// <summary>初始总弹量，包含弹匣和备弹。</summary>
        [SerializeField, Min(0)] private int _initialTotalBullets = GunConfig.Default.InitialTotalBullets;

        /// <summary>单个弹匣的容量。</summary>
        [SerializeField, Min(1)] private int _capacity = GunConfig.Default.Capacity;

        /// <summary>准备动作的 Tick 数。</summary>
        [SerializeField, Min(1)] private int _prepareTicks = (int)GunConfig.Default.PrepareTicks;

        /// <summary>两次射击之间的 Tick 数。</summary>
        [SerializeField, Min(1)] private int _shotIntervalTicks = (int)GunConfig.Default.ShotIntervalTicks;

        /// <summary>换弹动作的 Tick 数。</summary>
        [SerializeField, Min(1)] private int _reloadTicks = (int)GunConfig.Default.ReloadTicks;

        /// <summary>从当前参数生成一份固定配置。</summary>
        public GunConfig CreateConfig()
        {
            return new GunConfig(_initialTotalBullets, _capacity, (uint)_prepareTicks, (uint)_shotIntervalTicks, (uint)_reloadTicks);
        }

        /// <summary>创建弹药和状态相互独立的 AK47。</summary>
        public AK47 CreateGun()
        {
            return new AK47(CreateConfig());
        }

        /// <summary>限制 Inspector 中的参数范围。</summary>
        private void OnValidate()
        {
            _initialTotalBullets = Mathf.Max(0, _initialTotalBullets);
            _capacity = Mathf.Max(1, _capacity);
            _prepareTicks = Mathf.Max(1, _prepareTicks);
            _shotIntervalTicks = Mathf.Max(1, _shotIntervalTicks);
            _reloadTicks = Mathf.Max(1, _reloadTicks);
        }
    }
}
