using System.Collections.Generic;
using GamePlay.Player.Combat;
using GamePlay.Player.Simulation;
using GamePlay.Player.Simulation.Reconciliation;
using UnityEngine;

namespace GamePlay.Player.Presentation
{
    /// <summary>缓存远端状态，由公共显示时间轴统一指定呈现 Tick。</summary>
    public sealed class RemotePoseInterpolator : MonoBehaviour
    {
        readonly List<Sample> _samples = new();
        PlayerBrain _brain;
        PlayerCombatPoseController _combatPose;
        uint _epoch;
        uint _continuity;

        public Vector3 Velocity { get; private set; }
        public double PresentedTick { get; private set; } = double.NaN;

        void Awake()
        {
            _brain = GetComponent<PlayerBrain>();
            _combatPose = GetComponent<PlayerCombatPoseController>();
        }

        public void Push(uint serverTick, in PlayerSimState state, uint epoch = 0)
            => Push(serverTick, state, epoch, default);

        public void Push(uint serverTick, in PlayerSimState state, uint epoch, in PlayerCombatPose combatPose,
            uint continuity = 0)
        {
            if (!PlayerStateValidation.IsFinite(state) ||
                (_epoch != 0 && epoch != _epoch && !TickMath.IsNewer(epoch, _epoch)) ||
                (_continuity != 0 && continuity != 0 && continuity != _continuity &&
                 !TickMath.IsNewer(continuity, _continuity)))
                return;
            if (epoch != _epoch || (continuity != 0 && continuity != _continuity))
            {
                _epoch = epoch;
                _continuity = continuity;
                _samples.Clear();
                _combatPose?.ClearRemotePose();
                PresentedTick = double.NaN;
            }
            Upsert(serverTick, state, combatPose);
        }

        public bool TryGetRange(out double oldest, out double latest)
        {
            oldest = latest = double.NaN;
            if (!isActiveAndEnabled || _samples.Count == 0)
                return false;
            oldest = _samples[0].tick;
            latest = _samples[_samples.Count - 1].tick;
            return true;
        }

        /// <summary>所有角色使用同一 Tick，样本不足由调用方统一等待或夹取。</summary>
        public bool Present(double renderTick) => Present(renderTick, out _, out _);

        public bool TryGetBracket(double renderTick, out uint fromTick, out uint toTick)
        {
            fromTick = toTick = 0;
            if (!TryGetSamples(renderTick, out Sample from, out Sample to, out _))
                return false;
            fromTick = from.tick;
            toTick = to.tick;
            return true;
        }

        public bool Present(double renderTick, out uint fromTick, out uint toTick)
        {
            fromTick = toTick = 0;
            if (!TryGetSamples(renderTick, out Sample from, out Sample to, out float fraction))
                return false;

            Apply(from, to, fraction);
            fromTick = from.tick;
            toTick = to.tick;
            PresentedTick = renderTick;
            while (_samples.Count > 2 && _samples[1].tick < renderTick)
                _samples.RemoveAt(0);
            return true;
        }

        private bool TryGetSamples(double renderTick, out Sample from, out Sample to, out float fraction)
        {
            from = to = default;
            fraction = 0f;
            if (_brain == null || _brain.Role != PawnRole.Interpolated ||
                double.IsNaN(renderTick) || double.IsInfinity(renderTick) ||
                !TryGetRange(out double oldest, out double latest) || renderTick < oldest || renderTick > latest)
                return false;

            int upper = 0;
            while (upper < _samples.Count && _samples[upper].tick < renderTick)
                upper++;

            if (upper < _samples.Count && _samples[upper].tick == renderTick)
                from = to = _samples[upper];
            else if (upper <= 0)
                from = to = _samples[0];
            else if (upper >= _samples.Count)
                from = to = _samples[_samples.Count - 1];
            else
            {
                from = _samples[upper - 1];
                to = _samples[upper];
                double span = to.tick - from.tick;
                fraction = span > 0d ? (float)((renderTick - from.tick) / span) : 0f;
            }
            return true;
        }

        void Upsert(uint serverTick, in PlayerSimState state, in PlayerCombatPose combatPose)
        {
            int index = _samples.FindIndex(sample => sample.tick == serverTick);
            var sample = new Sample
            {
                tick = serverTick,
                position = state.position,
                velocity = state.velocity,
                yaw = state.yaw,
                pitch = state.pitch,
                combatPose = combatPose
            };
            if (index >= 0)
                _samples[index] = sample;
            else
                _samples.Add(sample);

            _samples.Sort((a, b) => a.tick.CompareTo(b.tick));
            if (_samples.Count > 32)
                _samples.RemoveAt(0);
        }

        void OnDisable()
        {
            _samples.Clear();
            _epoch = 0;
            _continuity = 0;
            Velocity = Vector3.zero;
            PresentedTick = double.NaN;
            _combatPose?.ClearRemotePose();
        }

        void Apply(in Sample from, in Sample to, float t)
        {
            transform.position = Vector3.Lerp(from.position, to.position, t);
            Velocity = Vector3.Lerp(from.velocity, to.velocity, t);
            _brain.Sight.ApplyAngles(
                Mathf.LerpAngle(from.yaw, to.yaw, t),
                Mathf.Lerp(from.pitch, to.pitch, t));
            if (_combatPose == null)
                _combatPose = GetComponent<PlayerCombatPoseController>();
            _combatPose?.SetRemotePose(PlayerCombatPose.Lerp(from.combatPose, to.combatPose, t));
        }

        struct Sample
        {
            public uint tick;
            public Vector3 position;
            public Vector3 velocity;
            public float yaw;
            public float pitch;
            public PlayerCombatPose combatPose;
        }
    }
}
