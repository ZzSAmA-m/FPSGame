using Core.Event;
using Core.Utility;
using GamePlay.Events;
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
            _brain.Stats.InitializeServer();
            _brain.Motion.SetView(EPlayerModelType.TP);
            _brain.CombatPose?.InitializeServer();
            _brain.Hitboxes?.InitializeServer();
        }

        public override void OnStopServer()
        {
            _brain.AbortServerStep();
            _brain.Hitboxes?.Shutdown();
            _brain.CombatPose?.Shutdown();
            _brain.Stats.Shutdown();
            base.OnStopServer();
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            _brain.SetRole(isLocalPlayer ? PawnRole.Predicted : PawnRole.Interpolated);
            _brain.Stats.InitializeClient(isLocalPlayer);
            ClientManager.RegisterPlayer(netId, gameObject);
            if (isLocalPlayer)
                return;

            _brain.Motion.SetView(EPlayerModelType.TP);
        }

        public override void OnStartLocalPlayer()
        {
            base.OnStartLocalPlayer();
            _brain.Stats.OnLifeStateChanged += PublishLocalInputAvailability;
            PublishLocalInputAvailability(_brain.Stats.LifeState);
            _brain.Motion.SetView(EPlayerModelType.FP);
            _brain.Camera.AttachCamera2Player();
            _brain.Sight.EnableLook();
        }

        public override void OnStopLocalPlayer()
        {
            _brain.Stats.OnLifeStateChanged -= PublishLocalInputAvailability;
            PublishLocalInputAvailability(PlayerLifeState.Uninitialized);
            _brain.Sight.DisableLook();
            _brain.Camera.DisAttachCamera2Player();
            base.OnStopLocalPlayer();
        }

        public override void OnStopClient()
        {
            ClientManager.UnregisterPlayer(netId);
            _brain.CombatPose?.ClearRemotePose();
            _brain.Stats.Shutdown();
            base.OnStopClient();
        }

        static void PublishLocalInputAvailability(PlayerLifeState state) =>
            EventCenter.Publish(PlayerInputEventIds.AvailabilityChanged, state == PlayerLifeState.Alive);
    }
}
