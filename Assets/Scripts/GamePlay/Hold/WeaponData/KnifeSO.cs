using UnityEngine;

namespace GamePlay.HoldSystem
{
    /// <summary>刀具的配置和模型入口，每名角色获得独立逻辑实例。</summary>
    [CreateAssetMenu(fileName = "KnifeSO", menuName = "Scriptable Objects/Weapon/KnifeSO")]
    public sealed class KnifeSO : WeaponSO
    {
        [SerializeField, Min(0)] private int _lightDamage = KnifeConfig.Default.LightDamage;
        [SerializeField, Min(0)] private int _heavyDamage = KnifeConfig.Default.HeavyDamage;
        [SerializeField, Min(1)] private int _prepareTicks = (int)KnifeConfig.Default.PrepareTicks;
        [SerializeField, Min(1)] private int _lightAttackTicks = (int)KnifeConfig.Default.LightAttackTicks;
        [SerializeField, Min(1)] private int _heavyAttackTicks = (int)KnifeConfig.Default.HeavyAttackTicks;

        public override WeaponSlot Slot => WeaponSlot.Knife;
        public override WeaponBase CreateWeapon() => new Knife(CreateConfig());

        public KnifeConfig CreateConfig() => new(_lightDamage, _heavyDamage,
            (uint)_prepareTicks, (uint)_lightAttackTicks, (uint)_heavyAttackTicks);

        private void OnValidate()
        {
            _lightDamage = Mathf.Max(0, _lightDamage);
            _heavyDamage = Mathf.Max(0, _heavyDamage);
            _prepareTicks = Mathf.Max(1, _prepareTicks);
            _lightAttackTicks = Mathf.Max(1, _lightAttackTicks);
            _heavyAttackTicks = Mathf.Max(1, _heavyAttackTicks);
        }
    }
}
