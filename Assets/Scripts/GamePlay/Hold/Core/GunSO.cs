using GamePlay.HoldSystem;
using UnityEngine;
namespace GamePlay.HoldSystem
{
    public abstract class GunSO:WeaponSO
    {
        [SerializeField, Tooltip("统一提供本枪的模型、FP/TP/模型动作名称和音频引用。")]
        private GunResourcePackSO _resourcePack;

        public GunResourcePackSO ResourcePack => _resourcePack;
        public override IWeaponPresentationResources PresentationResources => _resourcePack;
        public override WeaponController ControllerPrefab => _resourcePack != null ? _resourcePack.ModelPrefab : null;

        [SerializeField, Min(0)] protected int _initialTotalBullets = GunConfig.Default.InitialTotalBullets;

        /// <summary>单个弹匣的容量。</summary>
        [SerializeField, Min(1)] protected int _capacity = GunConfig.Default.Capacity;

        /// <summary>命中身体时的基础伤害。</summary>
        [SerializeField, Min(0)] protected int _bodyDamage = 0;

        /// <summary>命中腿部时的基础伤害。</summary>
        [SerializeField, Min(0)] protected int _legDamage = 0;

        /// <summary>命中头部时的基础伤害。</summary>
        [SerializeField, Min(0)] protected int _headDamage = 0;

        /// <summary>穿射损失的伤害比例，0 不减少伤害，1 减少全部伤害。</summary>
        [SerializeField, Range(0f, 1f), Tooltip("穿射伤害减少比例：0 不减少伤害，1 减少全部伤害。")]
        protected float _penetrationDamageReductionRatio = 0f;

        /// <summary>准备动作的 Tick 数。</summary>
        [SerializeField, Min(1)] protected int _prepareTicks = (int)GunConfig.Default.PrepareTicks;

        /// <summary>两次射击之间的 Tick 数。</summary>
        [SerializeField, Min(1)] protected int _shotIntervalTicks = (int)GunConfig.Default.ShotIntervalTicks;

        /// <summary>换弹动作的 Tick 数。</summary>
        [SerializeField, Min(1)] protected int _reloadTicks = (int)GunConfig.Default.ReloadTicks;

        public int BodyDamage => _bodyDamage;
        public int LegDamage => _legDamage;
        public int HeadDamage => _headDamage;
        public float PenetrationDamageReductionRatio => _penetrationDamageReductionRatio;

        /// <summary>限制 Inspector 中的参数范围。</summary>
        protected void OnValidate()
        {
            _initialTotalBullets = Mathf.Max(0, _initialTotalBullets);
            _capacity = Mathf.Max(1, _capacity);
            _bodyDamage = Mathf.Max(0, _bodyDamage);
            _legDamage = Mathf.Max(0, _legDamage);
            _headDamage = Mathf.Max(0, _headDamage);
            _penetrationDamageReductionRatio = Mathf.Clamp01(_penetrationDamageReductionRatio);
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
