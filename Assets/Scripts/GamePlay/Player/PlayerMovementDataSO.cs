using UnityEngine;

namespace GamePlay.Player
{
    [CreateAssetMenu(fileName = "PlayerMovementData", menuName = "Scriptable Objects/PlayerMovementData")]
    public class PlayerMovementDataSO : ScriptableObject
    {
        [SerializeField] float _moveSpeed;
        [SerializeField] float _jumpSpeed;

        public float MoveSpeed => _moveSpeed;
        public float JumpSpeed => _jumpSpeed;
    }
}
