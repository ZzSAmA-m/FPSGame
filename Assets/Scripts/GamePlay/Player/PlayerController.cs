using Core.Utility;
using GamePlay.Player.MotionFSM;
using GamePlay.Player.Simulation;
using Mirror;
using Network.Client;

namespace GamePlay.Player
{
    /// <summary>将 Mirror 生命周期转换成玩家身份和组件启停。</summary>
    public sealed class PlayerController : NetworkBehaviour
    {
        PlayerBrain _brain;

        void Awake()
        {
            _brain = GetComponent<PlayerBrain>();
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            _brain.SetRole(PawnRole.Authoritative);
            _brain.Model.SwitchModel(EPlayerModelType.TP);
            _brain.Animator.SwitchState<PlayerMotionState>();
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            _brain.SetRole(isLocalPlayer ? PawnRole.Predicted : PawnRole.Interpolated);
            ClientManager.RegisterPlayer(netId, gameObject);
            if (isLocalPlayer)
                return;

            _brain.Model.SwitchModel(EPlayerModelType.TP);
            _brain.Animator.SwitchState<PlayerMotionState>();
        }

        public override void OnStartLocalPlayer()
        {
            base.OnStartLocalPlayer();
            _brain.Model.SwitchModel(EPlayerModelType.FP);
            _brain.Animator.SwitchState<PlayerFPMotionState>();
            _brain.Camera.AttachCamera2Player();
            _brain.Sight.EnableLook();
        }

        public override void OnStopLocalPlayer()
        {
            _brain.Sight.DisableLook();
            _brain.Camera.DisAttachCamera2Player();
            base.OnStopLocalPlayer();
        }

        public override void OnStopClient()
        {
            ClientManager.UnregisterPlayer(netId);
            base.OnStopClient();
        }
    }
}
