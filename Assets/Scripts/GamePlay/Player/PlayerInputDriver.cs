using Core.Input;
using GamePlay.Player.Simulation;
using UnityEngine;

namespace GamePlay.Player
{
    /// <summary>将设备输入积累到下一拍，不直接移动角色。</summary>
    public sealed class PlayerInputDriver : MonoBehaviour
    {
        [SerializeField] InputCollector _collector;

        PlayerInputFrame _pending;
        bool _accepting;
        bool _fireHeld;
        bool _firePending;

        public static PlayerInputDriver Instance { get; private set; }

        void Awake()
        {
            Instance = this;
        }

        void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        void OnDisable() => ResetPendingInput();

        void Update() => Accumulate(_collector.GetSample());

        private void Accumulate(PlayerInput sample)
        {
            _pending.move = sample.move;
            _pending.look += sample.look;
            _pending.jump |= sample.space;
            _pending.key1 |= sample.key1;
            _pending.key2 |= sample.key2;
            _pending.key3 |= sample.key3;
            // 按住状态供同一渲染帧内的多次 Tick 使用，短点击保留到下一拍。
            _fireHeld = sample.lMouse;
            _firePending |= sample.lMouse;
        }

        public PlayerInputFrame Consume(uint tick)
        {
            // 丢掉连接前积压的视角和按键，避免进对局的第一拍猛转。
            if (!_accepting)
            {
                _accepting = true;
                _pending.look = Vector2.zero;
                _pending.jump = false;
                _pending.fire = false;
                _pending.key1 = false;
                _pending.key2 = false;
                _pending.key3 = false;
                _fireHeld = false;
                _firePending = false;
            }

            PlayerInputFrame frame = _pending;
            frame.tick = tick;
            // 本地预测和服务器使用同一份开火意图；短点击只消费一次，按住可跨拍持续。
            frame.fire = _fireHeld || _firePending;
            _firePending = false;
            _pending.look = Vector2.zero;
            _pending.jump = false;
            _pending.fire = false;
            _pending.key1 = false;
            _pending.key2 = false;
            _pending.key3 = false;
            return frame;
        }

        /// <summary>新预测开始时丢掉等待期间积压的按键和鼠标增量。</summary>
        public void ResetPendingInput()
        {
            _pending = default;
            _accepting = false;
            _fireHeld = false;
            _firePending = false;
        }
    }
}
