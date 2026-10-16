using Core.Event;
using Mirror;
using UnityEngine;

namespace GamePlay.Player
{
    /// <summary>
    /// 只为本地玩家挂载场景相机；远端玩家不会影响本机相机。
    /// </summary>
    public class PlayerCameraController : MonoBehaviour
    {
        [SerializeField] private Transform TfCameraPoint;

        private void Awake()
        {
            // Prefab 丢失引用时尽早报错，比连接后出现空引用更容易定位。
            if (TfCameraPoint == null)
            {
                Debug.LogError($"[{nameof(PlayerCameraController)}] CameraPoint 未配置。", this);
            }
        }

        /// <summary>本地玩家开始控制时，把场景相机挂到第一人称相机点。</summary>
        public void AttachCamera2Player()
        {
            EventCenter.GamePlayEvent.CameraAttachEvent.RaiseEvent(TfCameraPoint);
        }

        /// <summary>
        /// 玩家销毁前先释放相机，防止相机作为玩家子物体被 Unity 一并销毁。
        /// </summary>
        public void DisAttachCamera2Player()
        {
            EventCenter.GamePlayEvent.CameraDetachEvent.RaiseEvent();
        }


    }
}

