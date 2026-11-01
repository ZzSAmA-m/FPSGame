using Sirenix.OdinInspector;
using UnityEngine;

namespace GamePlay.Player
{
    /// <summary>
    /// 把 yaw/pitch 写到角色和第一人称挂点，并管理本机光标。
    /// 角度积分在 <see cref="Simulation.PlayerMotor"/> 里完成。
    /// </summary>
    public class PlayerSightController : MonoBehaviour
    {
        [SerializeField, Min(0f)] float _sensitivity = 0.12f;
        [SerializeField] float _minPitch = -89f;
        [SerializeField] float _maxPitch = 89f;
        [SerializeField] Transform _view;

        bool _lookEnabled;
        float _yaw;
        float _pitch;

        public float Sensitivity
        {
            get => _sensitivity;
            set => _sensitivity = Mathf.Max(0f, value);
        }

        public float MinPitch
        {
            get => _minPitch;
            set
            {
                _minPitch = value;
                ClampPitchRange();
            }
        }

        public float MaxPitch
        {
            get => _maxPitch;
            set
            {
                _maxPitch = value;
                ClampPitchRange();
            }
        }

        public Transform View => _view;

        public void SetPitchLimit(float minPitch, float maxPitch)
        {
            _minPitch = minPitch;
            _maxPitch = maxPitch;
            ClampPitchRange();
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

        [Button(nameof(EnableLook))]
        public void EnableLook()
        {
            _lookEnabled = true;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        [Button(nameof(DisableLook))]
        public void DisableLook()
        {
            if (_lookEnabled)
                ReleaseCursor();
            _lookEnabled = false;
        }

        public void ApplyAngles(float yaw, float pitch)
        {
            _yaw = yaw;
            _pitch = Mathf.Clamp(pitch, _minPitch, _maxPitch);
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
