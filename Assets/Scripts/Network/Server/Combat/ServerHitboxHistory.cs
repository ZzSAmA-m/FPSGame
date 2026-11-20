using System;
using System.Collections.Generic;
using GamePlay.Player.Combat;

namespace Network.Server.Combat
{
    public enum HitboxHistoryFailure : byte
    {
        None = 0,
        UnknownPlayer = 1,
        NotYetBorn = 2,
        TooOld = 3,
        NewerThanHistory = 4,
        Discontinuous = 5,
        LifeChanged = 6,
        Dead = 7,
        InvalidTime = 8
    }

    public readonly struct ServerHitboxFrame
    {
        public uint PlayerId { get; }
        public double ServerTick { get; }
        public uint LifeGeneration { get; }
        public uint ContinuityGeneration { get; }
        public bool IsAlive { get; }
        public PlayerHitboxSnapshot Geometry { get; }
        public HitboxReconstruction Reconstruction { get; }

        public ServerHitboxFrame(uint playerId, double serverTick, uint lifeGeneration, uint continuityGeneration,
            bool isAlive, PlayerHitboxSnapshot geometry, HitboxReconstruction reconstruction = default)
        {
            PlayerId = playerId;
            ServerTick = serverTick;
            LifeGeneration = lifeGeneration;
            ContinuityGeneration = continuityGeneration;
            IsAlive = isAlive;
            Geometry = geometry;
            Reconstruction = reconstruction;
        }
    }

    /// <summary>每名玩家保留固定容量的已完成姿态，按服务器小数 Tick 查询。</summary>
    public sealed class ServerHitboxHistory
    {
        private const double TickEpsilon = 1e-6;
        private readonly Dictionary<uint, PlayerHistory> _players = new();
        public int Capacity { get; }
        public IEnumerable<uint> PlayerIds => _players.Keys;

        public ServerHitboxHistory(int capacity = 64)
        {
            if (capacity < 2)
                throw new ArgumentOutOfRangeException(nameof(capacity), "历史容量至少需要两帧。");
            Capacity = capacity;
        }

        public void Record(ServerHitboxFrame frame)
        {
            if (frame.PlayerId == 0 || !HitboxRayMath.IsFinite(frame.ServerTick) || frame.ServerTick < 0d ||
                frame.LifeGeneration == 0 || frame.ContinuityGeneration == 0 || !frame.Geometry.IsValid)
                throw new ArgumentException("历史帧需要有效身份、时间、生命轮次、连续段及命中几何。", nameof(frame));
            if (!_players.TryGetValue(frame.PlayerId, out PlayerHistory history))
            {
                history = new PlayerHistory(Capacity, frame.ServerTick);
                _players.Add(frame.PlayerId, history);
            }
            history.Add(frame);
        }

        public void Record(uint playerId, double serverTick, PlayerHitboxController controller)
        {
            if (controller == null)
                throw new ArgumentNullException(nameof(controller));
            var geometry = controller.Capture();
            var reconstruction = new HitboxReconstruction(controller.transform,
                controller.GetComponent<PlayerCombatPoseController>(), geometry);
            Record(new ServerHitboxFrame(playerId, serverTick, controller.LifeGeneration,
                controller.ContinuityGeneration, controller.IsAlive, geometry, reconstruction));
        }

        public bool TrySample(uint playerId, double serverTick, out ServerHitboxFrame frame,
            out HitboxHistoryFailure failure)
        {
            frame = default;
            if (!HitboxRayMath.IsFinite(serverTick))
            {
                failure = HitboxHistoryFailure.InvalidTime;
                return false;
            }
            if (!_players.TryGetValue(playerId, out PlayerHistory history))
            {
                failure = HitboxHistoryFailure.UnknownPlayer;
                return false;
            }

            ServerHitboxFrame first = history[0];
            ServerHitboxFrame latest = history[history.Count - 1];
            if (serverTick < first.ServerTick - TickEpsilon)
            {
                failure = serverTick < history.BirthTick - TickEpsilon
                    ? HitboxHistoryFailure.NotYetBorn : HitboxHistoryFailure.TooOld;
                return false;
            }
            if (serverTick > latest.ServerTick + TickEpsilon)
            {
                failure = HitboxHistoryFailure.NewerThanHistory;
                return false;
            }
            if (!latest.IsAlive)
            {
                failure = HitboxHistoryFailure.Dead;
                return false;
            }

            ServerHitboxFrame before = first;
            for (int index = 0; index < history.Count; index++)
            {
                ServerHitboxFrame after = history[index];
                if (Math.Abs(serverTick - after.ServerTick) <= TickEpsilon)
                {
                    if (!CanUse(after, latest.LifeGeneration, out failure))
                        return false;
                    frame = after;
                    return true;
                }
                if (after.ServerTick > serverTick)
                {
                    if (!CanUse(before, latest.LifeGeneration, out failure) ||
                        !CanUse(after, latest.LifeGeneration, out failure))
                        return false;
                    if (before.ContinuityGeneration != after.ContinuityGeneration)
                    {
                        failure = HitboxHistoryFailure.Discontinuous;
                        return false;
                    }
                    float fraction = (float)((serverTick - before.ServerTick) / (after.ServerTick - before.ServerTick));
                    frame = new ServerHitboxFrame(playerId, serverTick, before.LifeGeneration,
                        before.ContinuityGeneration, true,
                        Interpolate(before, after, fraction));
                    failure = HitboxHistoryFailure.None;
                    return true;
                }
                before = after;
            }

            failure = HitboxHistoryFailure.NewerThanHistory;
            return false;
        }

        public bool Remove(uint playerId) => _players.Remove(playerId);
        public void Clear() => _players.Clear();

        public bool TrySamplePair(uint playerId, double tick, uint fromTick, uint toTick,
            out ServerHitboxFrame frame, out HitboxHistoryFailure failure)
        {
            frame = default;
            failure = HitboxHistoryFailure.InvalidTime;
            if (!HitboxRayMath.IsFinite(tick) || fromTick > toTick || tick < fromTick || tick > toTick)
                return false;
            if (!TrySample(playerId, fromTick, out var a, out failure) ||
                !TrySample(playerId, toTick, out var b, out failure))
                return false;
            if (a.ContinuityGeneration != b.ContinuityGeneration)
            {
                failure = HitboxHistoryFailure.Discontinuous;
                return false;
            }
            float t = toTick > fromTick ? (float)((tick - fromTick) / (toTick - fromTick)) : 0f;
            frame = new ServerHitboxFrame(playerId, tick, a.LifeGeneration, a.ContinuityGeneration, true,
                Interpolate(a, b, t));
            failure = HitboxHistoryFailure.None;
            return true;
        }

        private static PlayerHitboxSnapshot Interpolate(in ServerHitboxFrame a, in ServerHitboxFrame b, float t)
            => a.Reconstruction.IsValid && b.Reconstruction.IsValid
                ? HitboxReconstruction.Interpolate(a.Reconstruction, b.Reconstruction, a.Geometry, b.Geometry, t)
                : PlayerHitboxSnapshot.Interpolate(a.Geometry, b.Geometry, t);

        private static bool CanUse(ServerHitboxFrame frame, uint currentLife, out HitboxHistoryFailure failure)
        {
            failure = HitboxHistoryFailure.None;
            if (frame.LifeGeneration != currentLife)
                failure = HitboxHistoryFailure.LifeChanged;
            else if (!frame.IsAlive)
                failure = HitboxHistoryFailure.Dead;
            return failure == HitboxHistoryFailure.None;
        }

        private sealed class PlayerHistory
        {
            private readonly ServerHitboxFrame[] _frames;
            private int _start;
            public int Count { get; private set; }
            public double BirthTick { get; }
            public ServerHitboxFrame this[int index] => _frames[(_start + index) % _frames.Length];

            public PlayerHistory(int capacity, double birthTick)
            {
                _frames = new ServerHitboxFrame[capacity];
                BirthTick = birthTick;
            }

            public void Add(ServerHitboxFrame frame)
            {
                if (Count > 0)
                {
                    ServerHitboxFrame latest = this[Count - 1];
                    if (frame.ServerTick < latest.ServerTick)
                        throw new ArgumentException("历史帧必须按服务器 Tick 顺序插入。", nameof(frame));
                    if (frame.LifeGeneration < latest.LifeGeneration ||
                        frame.ContinuityGeneration < latest.ContinuityGeneration)
                        throw new ArgumentException("历史帧的生命轮次与连续段不能回退。", nameof(frame));
                    if (frame.ServerTick == latest.ServerTick)
                    {
                        _frames[(_start + Count - 1) % _frames.Length] = frame;
                        return;
                    }
                }

                if (Count == _frames.Length)
                {
                    _frames[_start] = frame;
                    _start = (_start + 1) % _frames.Length;
                    return;
                }
                _frames[(_start + Count) % _frames.Length] = frame;
                Count++;
            }
        }
    }
}
