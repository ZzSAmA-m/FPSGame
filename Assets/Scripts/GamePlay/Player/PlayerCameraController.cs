using Core.Event;
using GamePlay.Events;
using UnityEngine;

namespace GamePlay.Player
{
    /// <summary>为本地玩家挂载和释放场景相机。</summary>
    public class PlayerCameraController : MonoBehaviour
    {
        /// <summary>本地玩家的第一人称相机挂点。</summary>
        [SerializeField] private Transform TfCameraPoint;

        /// <summary>检查相机挂点是否配置。</summary>
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
            EventCenter.Publish(CameraEventIds.Attach, TfCameraPoint);
        }

        /// <summary>在玩家销毁前同步释放场景相机。</summary>
        public void DisAttachCamera2Player()
        {
            EventCenter.Publish(CameraEventIds.Detach);
        }


    }
}

