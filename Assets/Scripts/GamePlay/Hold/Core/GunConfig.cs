using System;

namespace GamePlay.HoldSystem
{
    /// <summary>保存枪械的固定配置。</summary>
    public sealed class GunConfig
    {
        /// <summary>兼容无参创建的默认配置。</summary>
        public static GunConfig Default { get; } = new(100, 30, 30, 6, 120);

        /// <summary>初始总弹量，包含弹匣和备弹。</summary>
        public int InitialTotalBullets { get; }

        /// <summary>弹匣容量。</summary>
        public int Capacity { get; }

        /// <summary>准备动作持续的 Tick 数。</summary>
        public uint PrepareTicks { get; }

        /// <summary>两次射击之间的 Tick 数。</summary>
        public uint ShotIntervalTicks { get; }

        /// <summary>换弹动作持续的 Tick 数。</summary>
        public uint ReloadTicks { get; }

        /// <summary>校验并保存枪械配置。</summary>
        public GunConfig(int initialTotalBullets, int capacity, uint prepareTicks, uint shotIntervalTicks, uint reloadTicks)
        {
            if (initialTotalBullets < 0)
                throw new ArgumentOutOfRangeException(nameof(initialTotalBullets), "初始总弹量不能为负数。");
            if (capacity <= 0)
                throw new ArgumentOutOfRangeException(nameof(capacity), "弹匣容量必须大于零。");
            if (prepareTicks == 0 || prepareTicks > int.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(prepareTicks), "准备 Tick 数必须在 1 到 int.MaxValue 之间。");
            if (shotIntervalTicks == 0 || shotIntervalTicks > int.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(shotIntervalTicks), "射击间隔 Tick 数必须在 1 到 int.MaxValue 之间。");
            if (reloadTicks == 0 || reloadTicks > int.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(reloadTicks), "换弹 Tick 数必须在 1 到 int.MaxValue 之间。");

            InitialTotalBullets = initialTotalBullets;
            Capacity = capacity;
            PrepareTicks = prepareTicks;
            ShotIntervalTicks = shotIntervalTicks;
            ReloadTicks = reloadTicks;
        }
    }
}
