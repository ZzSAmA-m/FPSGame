using GamePlay.Physics;
using UnityEngine;
/// <summary>
/// 挂载这个脚本,有CharactorController组件的GO会附加重力效果
/// </summary>
public class CharactorGForce : MonoBehaviour
{
    CharacterController _cc;
    private float _verticalVelocity;             //当前垂直速度
    private float _groundedForce;                //在地面上的速度
    [Header("重力Scale")]
    [SerializeField]private float _gravityScale=1;

    void Awake()
    {
        _cc=GetComponent<CharacterController>();
        if (_cc == null)
        {
            Debug.LogError("[CharactorGForce] 挂载重力组件的GameObject没有CharacterController");
        }
    }
    void Update()
    {
        if(_cc==null)return;
        ApplyGravity();
    }
    private void ApplyGravity()
    {
        // 在地面上时，不要让速度无限向下累积
        if (_cc.isGrounded && _verticalVelocity < 0f)
        {
            _verticalVelocity = _groundedForce;
        }

        // 重力影响速度
        _verticalVelocity += GForce.globalGForce * Time.deltaTime*_gravityScale;

        // 移动
        Vector3 velocity = Vector3.up * _verticalVelocity;

        _cc.Move(velocity * Time.deltaTime);
    }
}
