using System;

namespace GamePlay.Player.Simulation.Reconciliation
{
    /// <summary>固定容量的预测历史，旧槽位必须通过 Tick 校验才能读取。</summary>
    public sealed class PredictionHistory
    {
        readonly PredictionFrame[] _frames;
        readonly bool[] _occupied;
        uint _latestTick;
        int _latestSlot;
        bool _hasLatest;

        public int Capacity => _frames.Length;
        public int Count { get; private set; }

        public PredictionHistory(int capacity)
        {
            if (capacity < 1)
                throw new ArgumentOutOfRangeException(nameof(capacity));

            _frames = new PredictionFrame[capacity];
            _occupied = new bool[capacity];
        }

        public void Seed(uint tick, in PlayerSimState state)
        {
            Clear();
            Store(new PredictionFrame
            {
                Input = new PlayerInputFrame { tick = tick },
                PostState = state,
                HasInput = false
            });
        }

        public void Record(in PlayerInputFrame input, in PlayerSimState postState)
        {
            Store(new PredictionFrame { Input = input, PostState = postState, HasInput = true });
        }

        public bool TryGet(uint tick, out PredictionFrame frame)
        {
            int slot = Slot(tick);
            if (_occupied[slot] && _frames[slot].Input.tick == tick)
            {
                frame = _frames[slot];
                return true;
            }

            frame = default;
            return false;
        }

        public bool HasInputRange(uint first, uint last)
        {
            if (first == unchecked(last + 1))
                return true;

            uint length = unchecked(last - first) + 1;
            if (TickMath.IsOlder(last, first) || length == 0 || length > (uint)Capacity)
                return false;

            for (uint offset = 0; offset < length; offset++)
            {
                if (!TryGet(unchecked(first + offset), out PredictionFrame frame) || !frame.HasInput)
                    return false;
            }

            return true;
        }

        public void OverwriteState(uint tick, in PlayerSimState state)
        {
            int slot = Slot(tick);
            if (!_occupied[slot] || _frames[slot].Input.tick != tick)
                throw new InvalidOperationException("要改写的预测状态已不在历史中。");

            _frames[slot].PostState = state;
        }

        public void DiscardBefore(uint tick)
        {
            for (int slot = 0; slot < Capacity; slot++)
            {
                if (_occupied[slot] && TickMath.IsOlder(_frames[slot].Input.tick, tick))
                    ClearSlot(slot);
            }
        }

        public PlayerInputFrame[] CopyRecentInputs(int maxCount)
        {
            if (!_hasLatest || maxCount < 1)
                return Array.Empty<PlayerInputFrame>();

            int limit = Math.Min(maxCount, Capacity);
            int count = 0;
            for (uint offset = 0; offset < (uint)Capacity && count < limit; offset++)
            {
                if (TryGet(unchecked(_latestTick - offset), out PredictionFrame frame) && frame.HasInput)
                    count++;
            }

            if (count == 0)
                return Array.Empty<PlayerInputFrame>();

            var inputs = new PlayerInputFrame[count];
            int target = count - 1;
            for (uint offset = 0; offset < (uint)Capacity && target >= 0; offset++)
            {
                if (TryGet(unchecked(_latestTick - offset), out PredictionFrame frame) && frame.HasInput)
                    inputs[target--] = frame.Input;
            }

            return inputs;
        }

        public void Clear()
        {
            Array.Clear(_frames, 0, Capacity);
            Array.Clear(_occupied, 0, Capacity);
            Count = 0;
            _hasLatest = false;
            _latestTick = 0;
            _latestSlot = 0;
        }

        void Store(in PredictionFrame frame)
        {
            uint tick = frame.Input.tick;
            if (_hasLatest && TickMath.IsOlder(tick, _latestTick) &&
                unchecked(_latestTick - tick) >= (uint)Capacity)
                return;

            if (!_hasLatest)
            {
                _latestTick = tick;
                _latestSlot = 0;
                _hasLatest = true;
            }
            else if (TickMath.IsNewer(tick, _latestTick))
            {
                uint advance = unchecked(tick - _latestTick);
                if (advance >= (uint)Capacity)
                {
                    Array.Clear(_frames, 0, Capacity);
                    Array.Clear(_occupied, 0, Capacity);
                    Count = 0;
                }
                else
                {
                    for (uint offset = 1; offset <= advance; offset++)
                        ClearSlot(Slot(unchecked(_latestTick + offset)));
                }
                _latestSlot = (_latestSlot + (int)(advance % (uint)Capacity)) % Capacity;
                _latestTick = tick;
            }

            int slot = Slot(tick);
            if (!_occupied[slot])
                Count++;
            _occupied[slot] = true;
            _frames[slot] = frame;
        }

        int Slot(uint tick)
        {
            int offset = unchecked((int)(tick - _latestTick)) % Capacity;
            return (_latestSlot + offset + Capacity) % Capacity;
        }

        void ClearSlot(int slot)
        {
            if (!_occupied[slot])
                return;

            _occupied[slot] = false;
            _frames[slot] = default;
            Count--;
        }
    }
}
