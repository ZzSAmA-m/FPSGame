using System;
using UnityEngine;

namespace GamePlay.Player.Simulation.Reconciliation
{
    /// <summary>先比较同拍状态，仅在误差超限时恢复并重放。</summary>
    public sealed class PlayerReconciler
    {
        readonly IPlayerSimulation _simulation;
        readonly PredictionHistory _history;
        readonly ReconciliationSettings _settings;

        public PlayerReconciler(
            IPlayerSimulation simulation,
            PredictionHistory history,
            ReconciliationSettings settings)
        {
            _simulation = simulation ?? throw new ArgumentNullException(nameof(simulation));
            _history = history ?? throw new ArgumentNullException(nameof(history));
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _settings.Validate();
        }

        public ReconcileResult Reconcile(in PlayerAuthoritativeSnapshot snapshot, uint currentInputTick)
        {
            PlayerSimState before = _simulation.State;
            var result = new ReconcileResult
            {
                Status = ReconcileStatus.Ignored,
                Before = before,
                After = before
            };

            if (!PlayerStateValidation.IsFinite(snapshot.State))
                return result;

            uint ack = snapshot.StateInputTick;
            result.AcknowledgedTick = ack;
            result.Status = ReconcileStatus.NeedsResync;
            if (TickMath.IsNewer(ack, currentInputTick) ||
                !_history.TryGet(ack, out PredictionFrame predicted) ||
                !PlayerStateValidation.IsFinite(predicted.PostState) ||
                !PlayerStateValidation.IsFinite(before))
                return result;

            _settings.Validate();
            result.PositionError = Distance(predicted.PostState.position, snapshot.State.position);
            result.VelocityError = Mathf.Max(
                Distance(predicted.PostState.velocity, snapshot.State.velocity),
                Difference(predicted.PostState.verticalVelocity, snapshot.State.verticalVelocity));
            result.AngleError = Mathf.Max(
                AngleDifference(predicted.PostState.yaw, snapshot.State.yaw),
                AngleDifference(predicted.PostState.pitch, snapshot.State.pitch));

            bool matches = result.PositionError <= _settings.positionTolerance &&
                           result.VelocityError <= _settings.velocityTolerance &&
                           result.AngleError <= _settings.angleTolerance &&
                           predicted.PostState.grounded == snapshot.State.grounded;
            if (matches)
            {
                result.Status = ReconcileStatus.Matched;
                _history.DiscardBefore(ack);
                return result;
            }

            uint replayCount = unchecked(currentInputTick - ack);
            if (replayCount > (uint)_settings.maxReplayTicks || replayCount >= (uint)_history.Capacity ||
                (replayCount > 0 && !_history.HasInputRange(unchecked(ack + 1), currentInputTick)))
                return result;

            // 先确认所有输入可用，再修改角色现场。
            for (uint offset = 1; offset <= replayCount; offset++)
            {
                _history.TryGet(unchecked(ack + offset), out PredictionFrame frame);
                if (!PlayerStateValidation.IsFinite(frame.Input.move) ||
                    !PlayerStateValidation.IsFinite(frame.Input.look))
                    return result;
            }

            _simulation.RestoreState(snapshot.State);
            _history.OverwriteState(ack, snapshot.State);
            for (uint offset = 1; offset <= replayCount; offset++)
            {
                uint tick = unchecked(ack + offset);
                _history.TryGet(tick, out PredictionFrame frame);
                _simulation.Step(frame.Input);
                PlayerSimState state = _simulation.State;
                if (!PlayerStateValidation.IsFinite(state))
                {
                    _simulation.RestoreState(before);
                    _history.Clear();
                    return result;
                }

                _history.OverwriteState(tick, state);
            }

            _history.DiscardBefore(ack);
            result.Status = ReconcileStatus.Replayed;
            result.ReplayedTicks = (int)replayCount;
            result.After = _simulation.State;
            return result;
        }

        static float Distance(Vector3 left, Vector3 right)
        {
            double x = (double)left.x - right.x;
            double y = (double)left.y - right.y;
            double z = (double)left.z - right.z;
            return (float)Math.Min(float.MaxValue, Math.Sqrt(x * x + y * y + z * z));
        }

        static float Difference(float left, float right) =>
            (float)Math.Min(float.MaxValue, Math.Abs((double)left - right));

        static float AngleDifference(float left, float right)
        {
            double difference = ((double)left - right) % 360d;
            if (difference > 180d)
                difference -= 360d;
            else if (difference < -180d)
                difference += 360d;
            return (float)Math.Abs(difference);
        }
    }
}
