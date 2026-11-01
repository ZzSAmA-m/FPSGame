using UnityEngine;

namespace GamePlay.Player.Presentation
{
    /// <summary>只平滑模型和相机的纠偏位置，不改变物理根节点。</summary>
    [DefaultExecutionOrder(100)]
    public sealed class PlayerCorrectionSmoother : MonoBehaviour
    {
        [SerializeField, Min(0f)] float _decaySpeed = 18f;
        [SerializeField, Min(0f)] float _maxSmoothDistance = 1f;

        Transform _visualRoot;
        Vector3 _worldOffset;

        public Vector3 Offset => _worldOffset;
        public Vector3 VisualPosition => _visualRoot != null ? _visualRoot.position : transform.position;

        public void Initialize(Transform firstPersonRoot, Transform thirdPersonRoot)
        {
            if (_visualRoot != null)
                return;

            _visualRoot = new GameObject("PredictionVisualOffset").transform;
            _visualRoot.SetParent(transform, false);
            if (firstPersonRoot != null)
                firstPersonRoot.SetParent(_visualRoot, true);
            if (thirdPersonRoot != null && thirdPersonRoot != firstPersonRoot)
                thirdPersonRoot.SetParent(_visualRoot, true);
        }

        public void ApplyCorrection(Vector3 previousVisualPosition)
        {
            _worldOffset = previousVisualPosition - transform.position;
            if (_worldOffset.sqrMagnitude > _maxSmoothDistance * _maxSmoothDistance)
                _worldOffset = Vector3.zero;
            ApplyOffset();
        }

        public void ResetOffset()
        {
            _worldOffset = Vector3.zero;
            ApplyOffset();
        }

        void LateUpdate()
        {
            _worldOffset = _decaySpeed <= 0f
                ? Vector3.zero
                : _worldOffset * Mathf.Exp(-_decaySpeed * Time.unscaledDeltaTime);
            if (_worldOffset.sqrMagnitude < 0.000001f)
                _worldOffset = Vector3.zero;
            ApplyOffset();
        }

        void OnDisable()
        {
            ResetOffset();
        }

        void ApplyOffset()
        {
            if (_visualRoot != null)
                _visualRoot.position = transform.position + _worldOffset;
        }
    }
}
