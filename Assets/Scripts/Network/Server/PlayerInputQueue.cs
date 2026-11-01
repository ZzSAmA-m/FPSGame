using System;
using System.Collections.Generic;
using GamePlay.Player.Simulation;

namespace Network.Server
{
    /// <summary>每拍解析一份输入，模拟完成后才推进确认。</summary>
    public sealed class PlayerInputQueue
    {
        public const int MaxFutureTicks = 256;
        public const int MissingMoveHoldTicks = 3;

        readonly Dictionary<uint, PlayerInputFrame> _pending = new(MaxFutureTicks);
        PlayerInputFrame _last;
        PlayerInputFrame _resolved;
        bool _hasResolved;
        bool _resolvedSynthetic;
        int _missingTicks;

        public uint LastProcessedTick { get; private set; }
        public bool HasPending => _pending.Count > 0;
        public int PendingCount => _pending.Count;
        public uint SyntheticInputCount { get; private set; }

        public void Reset(uint baseTick = 0)
        {
            _pending.Clear();
            _last = default;
            _resolved = default;
            _hasResolved = false;
            _resolvedSynthetic = false;
            _missingTicks = 0;
            LastProcessedTick = baseTick;
            SyntheticInputCount = 0;
        }

        public void Push(in PlayerInputFrame frame)
        {
            uint distance = unchecked(frame.tick - LastProcessedTick);
            if (distance == 0 || distance > MaxFutureTicks || !IsFinite(frame))
                return;

            // 同一拍只接受第一份，避免重复包改写已选中的输入。
            if (!_pending.ContainsKey(frame.tick) && !(_hasResolved && frame.tick == _resolved.tick))
                _pending.Add(frame.tick, frame);
        }

        public PlayerInputFrame ResolveNext()
        {
            if (_hasResolved)
                return _resolved;

            uint next = unchecked(LastProcessedTick + 1);
            _resolvedSynthetic = !_pending.TryGetValue(next, out _resolved);
            if (_resolvedSynthetic)
            {
                _resolved = _last;
                if (_missingTicks >= MissingMoveHoldTicks)
                    _resolved.move = default;
                _resolved.look = default;
                _resolved.jump = false;
                _resolved.fire = false;
                _resolved.key1 = false;
                _resolved.key2 = false;
                _resolved.key3 = false;
            }

            _resolved.tick = next;
            _hasResolved = true;
            return _resolved;
        }

        public void Commit(uint tick)
        {
            if (!_hasResolved || tick != _resolved.tick || tick != unchecked(LastProcessedTick + 1))
                throw new InvalidOperationException("只能确认刚刚完成模拟的下一拍。");

            _pending.Remove(tick);
            LastProcessedTick = tick;
            _last = _resolved;
            if (_resolvedSynthetic)
            {
                _missingTicks = Math.Min(_missingTicks + 1, MissingMoveHoldTicks + 1);
                SyntheticInputCount++;
            }
            else
                _missingTicks = 0;
            _hasResolved = false;
        }

        static bool IsFinite(in PlayerInputFrame frame)
        {
            return IsFinite(frame.move.x) && IsFinite(frame.move.y)
                && IsFinite(frame.look.x) && IsFinite(frame.look.y);
        }

        static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
