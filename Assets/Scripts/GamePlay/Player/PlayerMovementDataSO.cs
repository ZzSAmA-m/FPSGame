using UnityEngine;

/// <summary>
/// 玩家移动相关配置数据
/// </summary>
[CreateAssetMenu(fileName = "PlayerMovementData", menuName = "Scriptable Objects/PlayerMovementData")]
public class PlayerMovementDataSO : ScriptableObject
{

    [SerializeField]private float _moveSpeed;
    [SerializeField]private float _jumpSpeed;


    /// <summary>
    /// 移动速度
    /// </summary>
    public float MoveSpeed=>_moveSpeed;
    
    /// <summary>
    /// 跳跃强度
    /// </summary>
    public float JumpSpeed=>_jumpSpeed;
}
