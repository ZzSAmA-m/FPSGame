using System;
using System.Collections.Generic;
using GamePlay.Player.Simulation;

namespace Network.Server.Combat
{
    public enum ShotTimeFailure : byte
    {
        None, MissingReceipt, InvalidTime, FutureTime, BackwardsTime, ImplausibleAge, InvalidBracket
    }

    public readonly struct ValidatedShotTime
    {
        public double QueryTick { get; }
        public ShotTimeFailure Failure { get; }
        public bool Clamped { get; }
        public bool IsValid => Failure == ShotTimeFailure.None;
        public ValidatedShotTime(double tick, ShotTimeFailure failure = ShotTimeFailure.None, bool clamped = false)
        { QueryTick = tick; Failure = failure; Clamped = clamped; }
    }

    /// <summary>校验输入首次到达时的视图年龄，再按执行 Tick 限制最大回溯。</summary>
    public sealed class ServerShotTimeValidator
    {
        readonly ServerCombatSettings _settings;
        readonly Dictionary<uint, Cursor> _cursors = new();
        public ServerShotTimeValidator(ServerCombatSettings settings) => _settings = settings;

        public ValidatedShotTime Validate(uint executionTick, in ServerPlayerInput input)
        {
            double view = input.Frame.viewServerTick;
            double receipt = input.ReceiptTick;
            ValidatedShotTime Reject(ShotTimeFailure reason) => new(double.NaN, reason);
            if (!Finite(receipt)) return Reject(ShotTimeFailure.MissingReceipt);
            if (!Finite(view) || view < 0d || !Finite(input.RoundTripSeconds) || input.RoundTripSeconds < 0d)
                return Reject(ShotTimeFailure.InvalidTime);
            double tolerance = _settings.TimeToleranceSeconds / SimTime.Dt;
            if (view > receipt + tolerance || view > executionTick + tolerance)
                return Reject(ShotTimeFailure.FutureTime);
            if (_cursors.TryGetValue(input.PlayerId, out Cursor cursor) && cursor.Epoch == input.PredictionEpoch &&
                input.Frame.tick > cursor.InputTick && view + 1e-6d < cursor.ViewTick)
                return Reject(ShotTimeFailure.BackwardsTime);
            // 显示时钟基于已到达的快照；视图到输入回达经历下行和上行两程。
            double expectedAge = (input.RoundTripSeconds + SimTime.RemoteDelaySeconds) / SimTime.Dt;
            double ageAtReceipt = receipt - view;
            if (ageAtReceipt < -tolerance || Math.Abs(ageAtReceipt - expectedAge) > tolerance)
                return Reject(ShotTimeFailure.ImplausibleAge);
            uint from = input.Frame.viewFromTick;
            uint to = input.Frame.viewToTick;
            if (from > to || view < from || view > to || to - from >= _settings.HistoryCapacity ||
                to > receipt + tolerance)
                return Reject(ShotTimeFailure.InvalidBracket);
            _cursors[input.PlayerId] = new Cursor(input.PredictionEpoch, input.Frame.tick, view);
            double query = Math.Max(0d, Math.Max(executionTick - _settings.MaxRewindSeconds / SimTime.Dt,
                Math.Min(executionTick, view)));
            return new ValidatedShotTime(query, clamped: Math.Abs(query - view) > 1e-6d);
        }

        public void Remove(uint id) => _cursors.Remove(id);
        public void Clear() => _cursors.Clear();
        static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
        readonly struct Cursor
        {
            public readonly uint Epoch, InputTick;
            public readonly double ViewTick;
            public Cursor(uint epoch, uint inputTick, double viewTick)
            { Epoch = epoch; InputTick = inputTick; ViewTick = viewTick; }
        }
    }
}
