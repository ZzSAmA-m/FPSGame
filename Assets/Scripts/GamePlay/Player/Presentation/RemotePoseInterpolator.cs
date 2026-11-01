using System.Collections.Generic;
using GamePlay.Player.Simulation;
using GamePlay.Player.Simulation.Reconciliation;
using UnityEngine;

namespace GamePlay.Player.Presentation
{
    /// <summary>远端玩家只做快照插值，不推进本地物理模拟。</summary>
    public sealed class RemotePoseInterpolator : MonoBehaviour
    {
        readonly List<Sample> _samples = new();
        PlayerBrain _brain;
        double _serverTickEstimate;
        double _estimateLocalTime;
        bool _clockStarted;
        uint _epoch;

        public Vector3 Velocity { get; private set; }

        void Awake()
        {
            _brain = GetComponent<PlayerBrain>();
        }

        public void Push(uint serverTick, in PlayerSimState state, uint epoch = 0)
        {
            if (!PlayerStateValidation.IsFinite(state) ||
                (_epoch != 0 && epoch != _epoch && !TickMath.IsNewer(epoch, _epoch)))
                return;
            if (epoch != _epoch)
            {
                _epoch = epoch;
                _samples.Clear();
                _clockStarted = false;
            }
            Upsert(serverTick, state);
            if (_samples[_samples.Count - 1].tick != serverTick)
                return;

            if (!_clockStarted)
            {
                _serverTickEstimate = serverTick;
                _estimateLocalTime = Time.unscaledTimeAsDouble;
                _clockStarted = true;
                return;
            }

            AdvanceClock();
            double error = serverTick - _serverTickEstimate;
            if (error < -1d)
                _serverTickEstimate = serverTick;
            else if (error > 1d)
                _serverTickEstimate += error * 0.5d;
        }

        void Update()
        {
            if (_brain.Role != PawnRole.Interpolated || _samples.Count == 0 || !_clockStarted)
                return;

            AdvanceClock();

            float delay = SimTime.RemoteDelaySeconds / SimTime.StepSeconds;
            float oldest = _samples[0].tick;
            float latest = _samples[_samples.Count - 1].tick;
            float renderTick = (float)(_serverTickEstimate - delay);
            if (renderTick < oldest)
                renderTick = oldest;
            if (renderTick > latest)
                renderTick = latest;

            int upper = 0;
            while (upper < _samples.Count && _samples[upper].tick < renderTick)
                upper++;

            if (upper <= 0)
                Apply(_samples[0], _samples[0], 0f);
            else if (upper >= _samples.Count)
                Apply(_samples[_samples.Count - 1], _samples[_samples.Count - 1], 0f);
            else
            {
                Sample from = _samples[upper - 1];
                Sample to = _samples[upper];
                float span = to.tick - from.tick;
                float t = span > 0f ? (renderTick - from.tick) / span : 0f;
                Apply(from, to, t);
            }

            while (_samples.Count > 2 && _samples[1].tick < renderTick)
                _samples.RemoveAt(0);
        }

        void Upsert(uint serverTick, in PlayerSimState state)
        {
            int index = _samples.FindIndex(sample => sample.tick == serverTick);
            var sample = new Sample
            {
                tick = serverTick,
                position = state.position,
                velocity = state.velocity,
                yaw = state.yaw,
                pitch = state.pitch
            };
            if (index >= 0)
                _samples[index] = sample;
            else
                _samples.Add(sample);

            _samples.Sort((a, b) => a.tick.CompareTo(b.tick));
            if (_samples.Count > 32)
                _samples.RemoveAt(0);
        }

        void AdvanceClock()
        {
            double now = Time.unscaledTimeAsDouble;
            _serverTickEstimate += (now - _estimateLocalTime) / SimTime.StepSeconds;
            _estimateLocalTime = now;
        }

        void Apply(in Sample from, in Sample to, float t)
        {
            transform.position = Vector3.Lerp(from.position, to.position, t);
            Velocity = Vector3.Lerp(from.velocity, to.velocity, t);
            _brain.Sight.ApplyAngles(
                Mathf.LerpAngle(from.yaw, to.yaw, t),
                Mathf.Lerp(from.pitch, to.pitch, t));
        }

        struct Sample
        {
            public uint tick;
            public Vector3 position;
            public Vector3 velocity;
            public float yaw;
            public float pitch;
        }
    }
}
