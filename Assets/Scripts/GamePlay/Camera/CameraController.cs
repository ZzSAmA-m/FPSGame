using Core.Event;
using Core.Utility;
using Sirenix.OdinInspector;
using UnityEngine;

namespace GamePlay.Camera {
    /// <summary>
    /// 场景主相机的挂载服务。相机属于场景，不属于玩家网络对象。
    /// </summary>
    public class CameraController : Singleton<CameraController>
    {
        private Transform _defaultParent;

        private void Awake()
        {
            _defaultParent = transform.parent;
        }

        private void OnEnable()
        {
            EventCenter.GamePlayEvent.CameraAttachEvent.OnEventRaised += AttachCameraToModel;
            EventCenter.GamePlayEvent.CameraDetachEvent.OnEventRaised += DetachCameraFromPlayer;
        }

        private void OnDisable()
        {
            EventCenter.GamePlayEvent.CameraAttachEvent.OnEventRaised -= AttachCameraToModel;
            EventCenter.GamePlayEvent.CameraDetachEvent.OnEventRaised -= DetachCameraFromPlayer;
        }


        /// <summary>
        /// 把场景相机挂到本地玩家的第一人称相机点。
        /// </summary>
        [Button("相机吸附到模型")]
        public void AttachCameraToModel(Transform tf)
        {
            if (tf == null)
            {
                Debug.LogError($"[{nameof(CameraController)}] 相机挂载点为空。", this);
                return;
            }

            transform.SetParent(tf, false);
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
        }

        /// <summary>
        /// 在本地玩家销毁前将相机移回场景层级，避免随玩家一起销毁。
        /// </summary>
        public void DetachCameraFromPlayer()
        {
            transform.SetParent(_defaultParent, true);
        }
    }
}