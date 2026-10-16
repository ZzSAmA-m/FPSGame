using Core.Input;
using Mirror;
using Sirenix.OdinInspector;
using UnityEngine;

namespace GamePlay.Player
{
    /// <summary>
    /// FPS 鼠标视角。左右转玩家 transform（模型跟着转），上下转相机挂点。
    /// </summary>
    public class PlayerSightController : MonoBehaviour
    {
        [SerializeField, Min(0f)] float _sensitivity = 0.12f;
        [SerializeField] float _minPitch = -89f;
        [SerializeField] float _maxPitch = 89f;

        [SerializeField]Transform _view;
        bool _lookEnabled;
        float _yaw;
        float _pitch;

        /// <summary>整体灵敏度（度 / 鼠标增量）。</summary>
        public float Sensitivity
        {
            get => _sensitivity;
            set => _sensitivity = Mathf.Max(0f, value);
        }

        /// <summary>仰角下限（负值朝上看）。</summary>
        public float MinPitch
        {
            get => _minPitch;
            set
            {
                _minPitch = value;
                ClampPitchRange();
                _pitch = Mathf.Clamp(_pitch, _minPitch, _maxPitch);
            }
        }

        /// <summary>仰角上限（正值朝下看）。</summary>
        public float MaxPitch
        {
            get => _maxPitch;
            set
            {
                _maxPitch = value;
                ClampPitchRange();
                _pitch = Mathf.Clamp(_pitch, _minPitch, _maxPitch);
            }
        }

        
        /// <summary>同时设置上下仰角限制。</summary>
        public void SetPitchLimit(float minPitch, float maxPitch)
        {
            _minPitch = minPitch;
            _maxPitch = maxPitch;
            ClampPitchRange();
            _pitch = Mathf.Clamp(_pitch, _minPitch, _maxPitch);
        }

        void Awake()
        {
            ClampPitchRange();
        }


        void OnDisable()
        {
            if (_lookEnabled)
                ReleaseCursor();
            _lookEnabled = false;
        }

        void Start()
        {
            CaptureCurrentAngles();
        }

        /// <summary>允许本机转动视角并锁定光标。</summary>
        [Button(nameof(EnableLook))]
        public void EnableLook()
        {
            _lookEnabled = true;
            CaptureCurrentAngles();
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        /// <summary>禁止转动视角并释放光标。</summary>
        [Button(nameof(DisableLook))]
        public void DisableLook()
        {
            if (_lookEnabled)
                ReleaseCursor();
            _lookEnabled = false;
        }

        /// <summary>按鼠标/摇杆增量转动视角。X 为水平，Y 为俯仰。</summary>
        public void Simulate(ref PlayerInput input)
        {
            var lookDelta=input.look;
            if (lookDelta.sqrMagnitude < 1e-8f)
                return;

            _yaw += lookDelta.x * _sensitivity;
            _pitch -= lookDelta.y * _sensitivity;
            _pitch = Mathf.Clamp(_pitch, _minPitch, _maxPitch);
            ApplyRotation();
        }

        void CaptureCurrentAngles()
        {
            _yaw = transform.eulerAngles.y;
            if (_view != null)
            {
                var x = _view.localEulerAngles.x;
                if (x > 180f)
                    x -= 360f;
                _pitch = x;
            }
            _pitch = Mathf.Clamp(_pitch, _minPitch, _maxPitch);
        }

        void ApplyRotation()
        {
            transform.rotation = Quaternion.Euler(0f, _yaw, 0f);
            if (_view != null)
                _view.localRotation = Quaternion.Euler(_pitch, 0f, 0f);
        }

        void ClampPitchRange()
        {
            if (_minPitch > _maxPitch)
                (_minPitch, _maxPitch) = (_maxPitch, _minPitch);
        }


        static void ReleaseCursor()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

    }
}
