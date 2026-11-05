using System;

namespace GamePlay.HoldSystem
{
    /// <summary>管理弹匣和备弹。</summary>
    public sealed class Magazine
    {
        /// <summary>剩余总弹量，包含弹匣和备弹。</summary>
        public int TotalBullets => Rounds + ReserveRounds;

        /// <summary>弹匣容量。</summary>
        public int Capacity { get; }

        /// <summary>弹匣当前弹量。</summary>
        public int Rounds { get; private set; }

        /// <summary>当前备弹数量。</summary>
        public int ReserveRounds { get; private set; }

        /// <summary>弹匣是否为空。</summary>
        public bool Empty => Rounds == 0;

        /// <summary>弹匣是否已满。</summary>
        public bool Full => Rounds == Capacity;

        /// <summary>是否有空间和备弹可供装填。</summary>
        public bool CanReload => !Full && ReserveRounds > 0;

        /// <summary>装填完成时通知本次装填量。</summary>
        public event Action<int> OnReload;

        /// <summary>装填成功时通知。</summary>
        public event Action OnReloadSuccessed;

        /// <summary>扣弹后通知弹匣剩余弹量。</summary>
        public event Action<int> OnConsumeRound;

        /// <summary>按总弹量初始化弹匣和备弹。</summary>
        public Magazine(int totalBullets, int capacity)
        {
            if (totalBullets < 0)
                throw new ArgumentOutOfRangeException(nameof(totalBullets), "总弹量不能为负数。");
            if (capacity <= 0)
                throw new ArgumentOutOfRangeException(nameof(capacity), "弹匣容量必须大于零。");

            Capacity = capacity;
            Rounds = Math.Min(totalBullets, capacity);
            ReserveRounds = totalBullets - Rounds;
        }

        /// <summary>尝试扣除一发弹匣弹药。</summary>
        internal bool TryConsumeRound()
        {
            if (Empty)
                return false;

            Rounds--;
            return true;
        }

        /// <summary>使用备弹填补弹匣缺口。</summary>
        internal bool Reload()
        {
            if (!CanReload)
                return false;

            int loaded = Math.Min(Capacity - Rounds, ReserveRounds);
            Rounds += loaded;
            ReserveRounds -= loaded;
            return true;
        }

        /// <summary>在状态提交后发送扣弹通知。</summary>
        internal void NotifyConsumed()
        {
            OnConsumeRound?.Invoke(Rounds);
        }

        /// <summary>在状态提交后发送装填通知。</summary>
        internal void NotifyReloaded(int loaded)
        {
            OnReload?.Invoke(loaded);
            OnReloadSuccessed?.Invoke();
        }
    }
}
