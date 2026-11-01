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

        void Update()
        {
            PlayerInput sample = _collector.GetSample();
            _pending.move = sample.move;
            _pending.look += sample.look;
            _pending.jump |= sample.space;
            _pending.fire |= sample.lMouse;
            _pending.key1 |= sample.key1;
            _pending.key2 |= sample.key2;
            _pending.key3 |= sample.key3;
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
            }

            PlayerInputFrame frame = _pending;
            frame.tick = tick;
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
        }
    }
}
