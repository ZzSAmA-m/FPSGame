using Core.Event;
using Core.Utility;
using GamePlay.Player.MotionFSM;
using GamePlay.Player.Network;
using Mirror;
using UnityEngine;

namespace GamePlay.Player
{
    /// <summary>
    /// 玩家网络生命周期与本地玩法组件之间的桥接器。
    /// </summary>
    /// <remarks>
    /// 这里只决定“本机看到 FP 还是 TP、是否绑定本地输入”，不负责网络传输。
    /// 使用 Mirror 的生命周期回调比在 Start 中读取 isLocalPlayer 更可靠，也能对称清理。
    /// </remarks>
    public class PlayerController : NetworkBehaviour
    {
        [SerializeField, Sirenix.OdinInspector.ReadOnly]
        private PlayerMovementController _playerMovementController;

        [SerializeField, Sirenix.OdinInspector.ReadOnly]
        private PlayerModelController _playerModelController;

        [SerializeField, Sirenix.OdinInspector.ReadOnly]
        private PlayerAnimatorController _playerAnimatorController;

        [SerializeField, Sirenix.OdinInspector.ReadOnly]
        private PlayerCameraController _playerCameraController;
        [SerializeField]
        private PlayerNetworkManager _playerNetworkManager;
        public PlayerNetworkManager PlayerNetworkManager=>_playerNetworkManager;

        private void Awake()
        {
            _playerMovementController = GetComponent<PlayerMovementController>();
            _playerModelController = GetComponent<PlayerModelController>();
            _playerAnimatorController = GetComponent<PlayerAnimatorController>();
            _playerCameraController = GetComponent<PlayerCameraController>();
        }
        void Start()
        {
            if (isLocalPlayer)
            {
                EventCenter.GamePlayEvent.LocalPlayerSpawnEvent.RaiseEvent(gameObject);
                EventCenter.InputEvent.BindPlayer2InputDriverEvent.RaiseEvent(gameObject);
                _playerCameraController.AttachCamera2Player();
                _playerModelController.SwitchModel(EPlayerModelType.FP);
                _playerAnimatorController.SwitchState<PlayerFPMotionState>();
            }
            else
            {
                _playerModelController.SwitchModel(EPlayerModelType.TP);     
                _playerAnimatorController.SwitchState<PlayerMotionState>();
            }
        }
        /// <summary>
        /// 该玩家对象在客户端完成 Spawn 时执行，本地玩家和远端玩家都会走这里。
        /// </summary>
        public override void OnStartClient()
        {
            base.OnStartClient();

            EventCenter.GamePlayEvent.PlayerSpawnEvent.RaiseEvent();

            // 默认先使用第三人称表现。随后本地玩家的 OnStartLocalPlayer
            // 会切到第一人称；远端玩家则一直保持第三人称。
            _playerModelController.SwitchModel(EPlayerModelType.TP);
        }

        /// <summary>
        /// 仅本机拥有的玩家执行：绑定输入、广播本地生成并切换第一人称模型。
        /// </summary>
        public override void OnStartLocalPlayer()
        {
            base.OnStartLocalPlayer();

        }

        /// <summary>
        /// 本地玩家停止时解除输入引用，避免场景服务继续驱动已销毁对象。
        /// </summary>
        public override void OnStopLocalPlayer()
        {
            EventCenter.InputEvent.UnbindPlayerFromInputDriverEvent.RaiseEvent(gameObject);
            _playerCameraController.DisAttachCamera2Player();
            base.OnStopLocalPlayer();
        }
    }
}

