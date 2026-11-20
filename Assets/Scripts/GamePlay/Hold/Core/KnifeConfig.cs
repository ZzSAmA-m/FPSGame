using System;

namespace GamePlay.HoldSystem
{
    /// <summary>保存一把刀的伤害和动作时长。</summary>
    public sealed class KnifeConfig
    {
        public static KnifeConfig Default { get; } = new(40, 65, 60, 45, 70);

        public int LightDamage { get; }
        public int HeavyDamage { get; }
        public uint PrepareTicks { get; }
        public uint LightAttackTicks { get; }
        public uint HeavyAttackTicks { get; }

        public KnifeConfig(int lightDamage, int heavyDamage, uint prepareTicks, uint lightAttackTicks, uint heavyAttackTicks)
        {
            if (lightDamage < 0) throw new ArgumentOutOfRangeException(nameof(lightDamage));
            if (heavyDamage < 0) throw new ArgumentOutOfRangeException(nameof(heavyDamage));
            LightDamage = lightDamage;
            HeavyDamage = heavyDamage;
            PrepareTicks = ValidateTicks(prepareTicks, nameof(prepareTicks));
            LightAttackTicks = ValidateTicks(lightAttackTicks, nameof(lightAttackTicks));
            HeavyAttackTicks = ValidateTicks(heavyAttackTicks, nameof(heavyAttackTicks));
        }

        public uint GetDuration(KnifeState state) => state switch
        {
            KnifeState.Preparing => PrepareTicks,
            KnifeState.LightAttack => LightAttackTicks,
            KnifeState.HeavyAttack => HeavyAttackTicks,
            _ => 0
        };

        private static uint ValidateTicks(uint ticks, string name)
        {
            if (ticks == 0 || ticks > int.MaxValue)
                throw new ArgumentOutOfRangeException(name, "动作 Tick 数必须在 1 到 int.MaxValue 之间。");
            return ticks;
        }
    }
}
