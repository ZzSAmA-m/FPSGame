using System;
using Core.Event;
using Core.Utility;
using GamePlay.Events;
using Sirenix.OdinInspector;
using UnityEngine;

namespace GamePlay.Camera {
    /// <summary>管理场景主相机的挂载和释放。</summary>
    public class CameraController : Singleton<CameraController>
    {
        /// <summary>相机在场景中的原父节点。</summary>
        private Transform _defaultParent;

        /// <summary>复用相机挂载事件的监听委托。</summary>
        private Action<Transform> _attachListener;

        /// <summary>复用相机释放事件的监听委托。</summary>
        private Action _detachListener;

        /// <summary>缓存原父节点和事件监听委托。</summary>
        private void Awake()
        {
            _defaultParent = transform.parent;
            _attachListener = AttachCameraToModel;
            _detachListener = DetachCameraFromPlayer;
        }

        /// <summary>启用时监听本地相机事件。</summary>
        private void OnEnable()
        {
            EventCenter.AddListener(CameraEventIds.Attach, _attachListener, owner: this);
            EventCenter.AddListener(CameraEventIds.Detach, _detachListener, owner: this);
        }

        /// <summary>停用时移除同一组监听委托。</summary>
        private void OnDisable()
        {
            EventCenter.RemoveListener(CameraEventIds.Attach, _attachListener);
            EventCenter.RemoveListener(CameraEventIds.Detach, _detachListener);
        }


        /// <summary>将相机挂到本地玩家的第一人称挂点。</summary>
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

        /// <summary>在玩家销毁前将相机移回场景层级。</summary>
        public void DetachCameraFromPlayer()
        {
            transform.SetParent(_defaultParent, true);
        }
    }
}
