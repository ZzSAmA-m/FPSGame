using UnityEngine;

namespace GamePlay.Player
{
    [CreateAssetMenu(fileName = "PlayerMovementData", menuName = "Scriptable Objects/PlayerMovementData")]
    public class PlayerMovementDataSO : ScriptableObject
    {
        [SerializeField] float _moveSpeed;
        [SerializeField] float _jumpSpeed;
        [SerializeField, Min(0f)] float _groundAcceleration = 5.5f;
        [SerializeField, Min(0f)] float _groundFriction = 5.2f;
        [SerializeField, Min(0f)] float _stopSpeed = 1.92f;
        [SerializeField, Min(0f)] float _surfaceFriction = 1f;

        public float MoveSpeed => _moveSpeed;
        public float JumpSpeed => _jumpSpeed;
        public float GroundAcceleration => _groundAcceleration;
        public float GroundFriction => _groundFriction;
        public float StopSpeed => _stopSpeed;
        public float SurfaceFriction => _surfaceFriction;
    }
}
