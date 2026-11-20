using System;
using GamePlay.Player;
using GamePlay.Player.Combat;
using UnityEngine;

namespace Network.Server.Combat
{
    public enum ServerShotStatus : byte
    {
        Miss = 0,
        Hit = 1,
        MapBlocked = 2,
        InvalidRay = 3,
        HistoryUnavailable = 4
    }

    /// <summary>一次历史查询的几何结果，数值伤害由服务器玩法层提交。</summary>
    public readonly struct ServerShotResult
    {
        public ServerShotStatus Status { get; }
        public uint TargetId { get; }
        public uint LifeGeneration { get; }
        public HitRegion Region { get; }
        public Vector3 Point { get; }
        public float Distance { get; }
        public HitboxHistoryFailure HistoryFailure { get; }

        public ServerShotResult(ServerShotStatus status, uint targetId = 0, uint lifeGeneration = 0,
            HitRegion region = HitRegion.None, Vector3 point = default, float distance = 0f,
            HitboxHistoryFailure historyFailure = HitboxHistoryFailure.None)
        {
            Status = status;
            TargetId = targetId;
            LifeGeneration = lifeGeneration;
            Region = region;
            Point = point;
            Distance = distance;
            HistoryFailure = historyFailure;
        }
    }

    public sealed class ServerShotResolver
    {
        private readonly ServerCombatSettings _settings;

        public ServerShotResolver(ServerCombatSettings settings = null)
        {
            _settings = settings ?? new ServerCombatSettings();
            if (!HitboxRayMath.IsFinite(_settings.MaxRange) || _settings.MaxRange <= 0f ||
                !HitboxRayMath.IsFinite(_settings.HeadTieTolerance) || _settings.HeadTieTolerance < 0f)
                throw new ArgumentException("命中查询需要有限正射程和非负头身容差。", nameof(settings));
        }

        public ServerShotResult Resolve(ServerHitboxHistory history, uint shooterId, Ray viewRay, Ray shootRay,
            double queryTick, uint? viewFromTick = null, uint? viewToTick = null)
        {
            if (history == null)
                throw new ArgumentNullException(nameof(history));
            if (!HitboxRayMath.TryNormalize(viewRay, out Ray view) ||
                !HitboxRayMath.TryNormalize(shootRay, out Ray shot))
                return new ServerShotResult(ServerShotStatus.InvalidRay);
            if (!HitboxRayMath.IsFinite(queryTick))
                return new ServerShotResult(ServerShotStatus.HistoryUnavailable,
                    historyFailure: HitboxHistoryFailure.InvalidTime);

            // 从视线原点检查到前移原点，避免近墙位于被跳过的短线段中。
            Vector3 nearSegment = shot.origin - view.origin;
            float nearDistance = nearSegment.magnitude;
            if (nearDistance > 1e-6f && TryMapDistance(new Ray(view.origin, nearSegment), nearDistance,
                    out float nearBlock))
                return new ServerShotResult(ServerShotStatus.MapBlocked, point: view.origin +
                    nearSegment.normalized * nearBlock, distance: nearBlock);

            bool mapBlocked = TryMapDistance(shot, _settings.MaxRange, out float mapDistance);
            float visibleDistance = mapBlocked ? mapDistance : _settings.MaxRange;
            ServerShotResult nearest = new(ServerShotStatus.Miss, distance: visibleDistance);
            bool sampledAny = false;
            HitboxHistoryFailure historyFailure = HitboxHistoryFailure.None;

            foreach (uint playerId in history.PlayerIds)
            {
                if (playerId == shooterId)
                    continue;
                ServerHitboxFrame frame;
                HitboxHistoryFailure failure;
                bool sampled = viewFromTick.HasValue && viewToTick.HasValue
                    ? history.TrySamplePair(playerId, queryTick, viewFromTick.Value, viewToTick.Value, out frame, out failure)
                    : history.TrySample(playerId, queryTick, out frame, out failure);
                if (!sampled)
                {
                    if (historyFailure == HitboxHistoryFailure.None)
                        historyFailure = failure;
                    continue;
                }
                sampledAny = true;
                if (!TryPlayerHit(shot, frame.Geometry, visibleDistance, out HitRegion region, out float distance))
                    continue;
                if (mapBlocked && distance >= mapDistance)
                    continue;
                if (nearest.Status == ServerShotStatus.Hit &&
                    (distance > nearest.Distance || (distance == nearest.Distance && playerId > nearest.TargetId)))
                    continue;
                nearest = new ServerShotResult(ServerShotStatus.Hit, playerId, frame.LifeGeneration,
                    region, shot.GetPoint(distance), distance);
            }

            if (nearest.Status == ServerShotStatus.Hit)
                return nearest;
            if (mapBlocked)
                return new ServerShotResult(ServerShotStatus.MapBlocked, point: shot.GetPoint(mapDistance),
                    distance: mapDistance, historyFailure: historyFailure);
            return new ServerShotResult(sampledAny || historyFailure == HitboxHistoryFailure.None
                ? ServerShotStatus.Miss : ServerShotStatus.HistoryUnavailable,
                distance: _settings.MaxRange, historyFailure: historyFailure);
        }

        private bool TryPlayerHit(Ray ray, PlayerHitboxSnapshot geometry, float maxDistance,
            out HitRegion region, out float distance)
        {
            bool head = HitboxRayMath.RaycastSphere(ray, geometry.Head, maxDistance, out float headDistance);
            bool body = HitboxRayMath.RaycastCapsule(ray, geometry.Body, maxDistance, out float bodyDistance);
            region = HitRegion.None;
            distance = 0f;
            if (head && (!body || headDistance <= bodyDistance + _settings.HeadTieTolerance))
            {
                region = HitRegion.Head;
                distance = body ? Mathf.Min(headDistance, bodyDistance) : headDistance;
                return true;
            }
            if (!body)
                return false;
            region = HitRegion.Body;
            distance = bodyDistance;
            return true;
        }

        private bool TryMapDistance(Ray ray, float maxDistance, out float distance)
        {
            distance = maxDistance;
            bool found = false;
            foreach (RaycastHit hit in Physics.RaycastAll(ray, maxDistance, _settings.StaticCollisionMask,
                         QueryTriggerInteraction.Ignore))
            {
                if (hit.collider.GetComponentInParent<PlayerController>() != null)
                    continue;
                if (hit.distance < distance || !found)
                {
                    distance = hit.distance;
                    found = true;
                }
            }
            return found;
        }
    }
}
