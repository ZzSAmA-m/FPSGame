using System;
using Core.Event;
using Core.Input;
using Core.Utility;
using UnityEngine;
namespace GamePlay.Player
{
    public sealed class PlayerInputDriver : Singleton<PlayerInputDriver>
    {
        [SerializeField] private InputCollector _collector;
        [SerializeField] private PlayerMovementController _playerMovement;
        [SerializeField] private PlayerSightController _playerSight;
        [SerializeField] private PlayerWeaponController _playerWeapon;
        

        private GameObject _boundPlayer;
        public event InputDataSender OnDriveInputData;
        

        private void Update()
        {
            PlayerInput input = _collector.GetSample();
            PredictInputData(ref input);
            OnDriveInputData?.Invoke(ref input,Time.deltaTime);
        }


        private void OnEnable()
        {
            EventCenter.InputEvent.BindPlayer2InputDriverEvent.OnEventRaised += BindPlayer;
            EventCenter.InputEvent.UnbindPlayerFromInputDriverEvent.OnEventRaised += UnbindPlayer;
        }

        private void OnDisable()
        {
            EventCenter.InputEvent.BindPlayer2InputDriverEvent.OnEventRaised -= BindPlayer;
            EventCenter.InputEvent.UnbindPlayerFromInputDriverEvent.OnEventRaised -= UnbindPlayer;
        }
        void PredictInputData(ref PlayerInput input)
        {
            _playerSight?.Simulate(ref input);
        }
        public void DriveInputData(ref PlayerInput input)
        {

            _playerMovement?.Simulate(ref input);  
            _playerWeapon?.Simulate(ref input);
        }
        /// <summary>
        /// 将场景级输入采集器绑定到当前本地玩家。
        /// </summary>
        public void BindPlayer(GameObject obj)
        {
            if (obj == null)
            {
                Debug.LogError($"[{nameof(PlayerInputDriver)}] 不能绑定空玩家对象。", this);
                return;
            }

            _boundPlayer = obj;
            _playerMovement = obj.GetComponent<PlayerMovementController>();
            _playerSight = obj.GetComponent<PlayerSightController>();
            _playerWeapon = obj.GetComponent<PlayerWeaponController>();

            if (_playerMovement == null || _playerSight == null || _playerWeapon == null)
            {
                Debug.LogError(
                    $"[{nameof(PlayerInputDriver)}] 玩家预制体缺少输入驱动所需组件。" +
                    $"movement={_playerMovement != null}, sight={_playerSight != null}, weapon={_playerWeapon != null}",
                    obj);
            }
        }

        /// <summary>
        /// 仅解除当前绑定的玩家；旧对象迟到的退出事件不会误清理新玩家。
        /// </summary>
        public void UnbindPlayer(GameObject obj)
        {
            if (_boundPlayer != obj)
            {
                return;
            }

            _boundPlayer = null;
            _playerMovement = null;
            _playerSight = null;
            _playerWeapon = null;
        }
    }
}

