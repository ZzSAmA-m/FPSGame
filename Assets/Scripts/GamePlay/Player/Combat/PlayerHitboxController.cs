using System;
using GamePlay.Player.Simulation;
using UnityEngine;

namespace GamePlay.Player.Combat
{
    /// <summary>绑定骨骼判定体，并标识可连续采样的服务器生命与姿态片段。</summary>
    [DisallowMultipleComponent]
    public sealed class PlayerHitboxController : MonoBehaviour
    {
        [SerializeField] private SphereCollider _head;
        [SerializeField] private CapsuleCollider _body;
        private PlayerStatsController _stats;
        private PlayerLifeState _lastLifeState;
        private bool _initialized;

        public SphereCollider HeadCollider => _head;
        public CapsuleCollider BodyCollider => _body;
        private uint _lifeGeneration;
        private uint _continuityGeneration;
        public uint LifeGeneration => checked(_lifeGeneration + (_stats != null ? _stats.SpawnSequence : 0));
        public uint ContinuityGeneration => checked(_continuityGeneration + (_stats != null ? _stats.SpawnSequence : 0));
        public bool IsInitialized => _initialized;
        public bool IsAlive => _initialized && _stats != null && _stats.LifeState == PlayerLifeState.Alive;

        public void InitializeServer()
        {
            if (_initialized)
                return;
            PlayerBrain brain = GetComponent<PlayerBrain>();
            _stats = GetComponent<PlayerStatsController>();
            if (brain == null || brain.Role != PawnRole.Authoritative || _stats == null ||
                _stats.LifeState == PlayerLifeState.Uninitialized)
                throw new InvalidOperationException("命中判定体需要已经初始化的服务端权威数值。");

            Capture();
            _lifeGeneration = checked(_lifeGeneration + 1);
            _continuityGeneration = checked(_continuityGeneration + 1);
            _lastLifeState = _stats.LifeState;
            _stats.OnLifeStateChanged += HandleLifeStateChanged;
            _stats.Stopped += Shutdown;
            _initialized = true;
        }

        /// <summary>传送完成后开启新连续段，保留同一生命轮次。</summary>
        public void BreakContinuity()
        {
            if (!_initialized)
                throw new InvalidOperationException("判定体尚未初始化服务端生命周期。");
            _continuityGeneration = checked(_continuityGeneration + 1);
        }

        public PlayerHitboxSnapshot Capture()
        {
            if (_head == null || _body == null)
                throw new InvalidOperationException("PlayerHitboxController 必须显式绑定头部球体和身体胶囊。");

            Transform headTransform = _head.transform;
            Vector3 headScale = Abs(headTransform.lossyScale);
            float headRadius = _head.radius * Mathf.Max(headScale.x, Mathf.Max(headScale.y, headScale.z));
            var head = new HitSphere(headTransform.TransformPoint(_head.center), headRadius);

            Transform bodyTransform = _body.transform;
            Vector3 bodyScale = Abs(bodyTransform.lossyScale);
            Vector3 axis;
            float axisScale;
            float radiusScale;
            switch (_body.direction)
            {
                case 0:
                    axis = Vector3.right;
                    axisScale = bodyScale.x;
                    radiusScale = Mathf.Max(bodyScale.y, bodyScale.z);
                    break;
                case 2:
                    axis = Vector3.forward;
                    axisScale = bodyScale.z;
                    radiusScale = Mathf.Max(bodyScale.x, bodyScale.y);
                    break;
                default:
                    axis = Vector3.up;
                    axisScale = bodyScale.y;
                    radiusScale = Mathf.Max(bodyScale.x, bodyScale.z);
                    break;
            }

            float bodyRadius = _body.radius * radiusScale;
            float halfSegment = Mathf.Max(_body.height * axisScale * 0.5f - bodyRadius, 0f);
            Vector3 bodyCenter = bodyTransform.TransformPoint(_body.center);
            Vector3 axisOffset = bodyTransform.TransformDirection(axis).normalized * halfSegment;
            var body = new HitCapsule(bodyCenter - axisOffset, bodyCenter + axisOffset, bodyRadius);
            var snapshot = new PlayerHitboxSnapshot(head, body);
            if (!snapshot.IsValid)
                throw new InvalidOperationException("命中判定体产生了非有限坐标或无效半径。");
            return snapshot;
        }

        public void Shutdown()
        {
            if (!_initialized)
                return;
            _stats.OnLifeStateChanged -= HandleLifeStateChanged;
            _stats.Stopped -= Shutdown;
            _lifeGeneration = LifeGeneration;
            _continuityGeneration = ContinuityGeneration;
            _stats = null;
            _lastLifeState = PlayerLifeState.Uninitialized;
            _initialized = false;
        }

        private void HandleLifeStateChanged(PlayerLifeState state)
        {
            if (state == _lastLifeState)
                return;
            _continuityGeneration = checked(_continuityGeneration + 1);
            _lastLifeState = state;
        }

        private void OnDestroy() => Shutdown();
        private static Vector3 Abs(Vector3 value) => new(Mathf.Abs(value.x), Mathf.Abs(value.y), Mathf.Abs(value.z));
    }
}
