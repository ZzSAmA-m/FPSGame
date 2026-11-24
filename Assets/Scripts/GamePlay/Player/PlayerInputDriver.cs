using Core.Event;
using Core.Input;
using GamePlay.Events;
using GamePlay.Player.Simulation;
using Network.Client;
using UnityEngine;

namespace GamePlay.Player
{
    /// <summary>将设备输入积累到下一拍，不直接移动角色。</summary>
    public sealed class PlayerInputDriver : MonoBehaviour
    {
        [SerializeField] InputCollector _collector;

        PlayerInputFrame _pending;
        bool _inputEnabled = true;
        bool _accepting;
        bool _fireHeld;
        bool _firePending;
        double _sampledViewTick = double.NaN;
        uint _sampledViewFromTick;
        uint _sampledViewToTick;
        bool _hasSampledViewTick;
        double _pendingFireViewTick = double.NaN;
        uint _pendingFireFromTick;
        uint _pendingFireToTick;
        bool _hasPendingFireViewTick;

        public static PlayerInputDriver Instance { get; private set; }

        void Awake()
        {
            Instance = this;
            EventCenter.AddListener<bool>(PlayerInputEventIds.AvailabilityChanged, SetInputEnabled, owner: this);
        }

        void OnDestroy()
        {
            EventCenter.RemoveListener<bool>(PlayerInputEventIds.AvailabilityChanged, SetInputEnabled);
            if (Instance == this)
                Instance = null;
        }

        void OnDisable() => ResetPendingInput();

        void Update()
        {
            if (_inputEnabled)
                Accumulate(_collector.GetSample());
        }

        /// <summary>恢复采样设备输入。</summary>
        public void EnableInput() => SetInputEnabled(true);

        /// <summary>丢弃设备输入，仍按模拟拍生成无操作帧。</summary>
        public void DisableInput() => SetInputEnabled(false);

        void SetInputEnabled(bool enabled)
        {
            if (_inputEnabled == enabled)
                return;

            _inputEnabled = enabled;
            ResetPendingInput();
            _accepting = enabled;
        }

        private void Accumulate(PlayerInput sample)
        {
            if (!_inputEnabled)
                return;

            _sampledViewTick = ClientManager.Instance != null
                ? ClientManager.Instance.LastPresentedServerTick : double.NaN;
            _sampledViewFromTick = ClientManager.Instance != null ? ClientManager.Instance.LastPresentedFromTick : 0;
            _sampledViewToTick = ClientManager.Instance != null ? ClientManager.Instance.LastPresentedToTick : 0;
            _hasSampledViewTick = true;
            _pending.move = sample.move;
            _pending.look += sample.look;
            _pending.jump |= sample.jumpPressed;
            _pending.key1 |= sample.key1;
            _pending.key2 |= sample.key2;
            _pending.key3 |= sample.key3;
            _pending.reload |= sample.r;
            _pending.lightAttackPressed |= sample.lightAttackPressed;
            _pending.heavyAttackPressed |= sample.heavyAttackPressed;
            if (sample.lMouse && !_firePending)
            {
                _pendingFireViewTick = _sampledViewTick;
                _pendingFireFromTick = _sampledViewFromTick;
                _pendingFireToTick = _sampledViewToTick;
                _hasPendingFireViewTick = true;
            }
            // 按住状态供同一渲染帧内的多次 Tick 使用，短点击保留到下一拍。
            _fireHeld = sample.lMouse;
            _firePending |= sample.lMouse;
        }

        public PlayerInputFrame Consume(uint tick)
        {
            if (!_inputEnabled)
                return new PlayerInputFrame { tick = tick, viewServerTick = double.NaN };

            // 丢掉连接前积压的视角和按键，避免进对局的第一拍猛转。
            if (!_accepting)
            {
                _accepting = true;
                _pending = new PlayerInputFrame { move = _pending.move };
                _fireHeld = false;
                _firePending = false;
            }

            PlayerInputFrame frame = _pending;
            frame.tick = tick;
            frame.viewServerTick = _hasSampledViewTick ? _sampledViewTick :
                ClientManager.Instance != null ? ClientManager.Instance.LastPresentedServerTick : double.NaN;
            frame.viewFromTick = _hasSampledViewTick ? _sampledViewFromTick :
                ClientManager.Instance != null ? ClientManager.Instance.LastPresentedFromTick : 0;
            frame.viewToTick = _hasSampledViewTick ? _sampledViewToTick :
                ClientManager.Instance != null ? ClientManager.Instance.LastPresentedToTick : 0;
            if (_firePending && _hasPendingFireViewTick)
            {
                frame.viewServerTick = _pendingFireViewTick;
                frame.viewFromTick = _pendingFireFromTick;
                frame.viewToTick = _pendingFireToTick;
            }
            // 本地预测和服务器使用同一份开火意图；短点击只消费一次，按住可跨拍持续。
            frame.fire = _fireHeld || _firePending;
            _firePending = false;
            _hasPendingFireViewTick = false;
            _pending = new PlayerInputFrame { move = _pending.move };
            return frame;
        }

        /// <summary>新预测开始时丢掉等待期间积压的按键和鼠标增量。</summary>
        public void ResetPendingInput()
        {
            _collector?.ClearPendingActions();
            _pending = default;
            _accepting = false;
            _fireHeld = false;
            _firePending = false;
            _sampledViewTick = double.NaN;
            _sampledViewFromTick = 0;
            _sampledViewToTick = 0;
            _hasSampledViewTick = false;
            _pendingFireViewTick = double.NaN;
            _pendingFireFromTick = 0;
            _pendingFireToTick = 0;
            _hasPendingFireViewTick = false;
        }
    }
}
