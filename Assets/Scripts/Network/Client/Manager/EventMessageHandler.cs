using System;
using Core.ZLog;
using GamePlay.Player;
using GamePlay.Player.Simulation;
using Network.Common.Messages;
#if UNITY_EDITOR
using UnityEngine;
#endif

namespace Network.Client
{
    /// <summary>对服务器事件去重，并将远端玩家事实交给本客户端的表现组件。</summary>
    public sealed class EventMessageHandler
    {
        private readonly GamePlayerManager _players;
        private ulong _lastReceivedEventId;

        public EventMessageHandler(GamePlayerManager players)
        {
            _players = players ?? throw new ArgumentNullException(nameof(players));
        }

        /// <summary>有效事件接受一次；角色表现失败后仍允许观察者接收该事实。</summary>
        public bool Handle(NetworkEventMessage message)
        {
            if (message == null || message.eventId == 0 || message.eventId <= _lastReceivedEventId)
                return false;

            _lastReceivedEventId = message.eventId;
            try
            {
                switch (message)
                {
                    case FireEventMessage fire:
#if UNITY_EDITOR
                        bool localShot = _players.Players.TryGetValue(fire.netId, out var shooter) &&
                            shooter != null && shooter.TryGetComponent(out PlayerBrain brain) &&
                            brain.Role == PawnRole.Predicted;
                        Vector3 rayOrigin = fire.origin;
                        Vector3 rayDirection = fire.direction;
                        if (localShot && shooter.TryGetComponent(out PlayerCameraController cameraController) &&
                            cameraController.TryGetViewRay(out Ray cameraRay))
                        {
                            Ray shootRay = PlayerShootConfig.CreateShootRay(cameraRay);
                            rayOrigin = shootRay.origin;
                            rayDirection = shootRay.direction;
                        }
                        Color rayColor = localShot ? new Color(0.5f, 0f, 1f) : Color.red;
                        Debug.DrawRay(rayOrigin, rayDirection * 100f, rayColor, 3f, false);
#endif
                        FindWeapon(fire.netId, PawnRole.Interpolated)?.PresentRemoteFire(fire.weaponSlot);
                        break;
                    case WeaponSwitchEventMessage weaponSwitch:
                        if (weaponSwitch.serverInitiated)
                            FindWeapon(weaponSwitch.netId, PawnRole.Predicted)?.ApplyAuthoritativeAction(
                                PlayerWeaponActionKind.Switched, weaponSwitch.weaponSlot, default);
                        FindWeapon(weaponSwitch.netId, PawnRole.Interpolated)?.PresentRemoteWeaponSwitch(weaponSwitch.weaponSlot);
                        break;
                    case KnifeAttackEventMessage attack:
                        FindWeapon(attack.netId, PawnRole.Interpolated)?.PresentRemoteKnifeAttack(attack.weaponSlot, attack.attackKind);
                        break;
                    case PrepareEventMessage prepare:
                        if (prepare.serverInitiated)
                            FindWeapon(prepare.netId, PawnRole.Predicted)?.ApplyAuthoritativeAction(
                                PlayerWeaponActionKind.PrepareStarted, prepare.weaponSlot, default);
                        FindWeapon(prepare.netId, PawnRole.Interpolated)?.PresentRemotePrepare(prepare.weaponSlot);
                        break;
                    case ReloadEventMessage reload:
                        if (reload.serverInitiated)
                            FindWeapon(reload.netId, PawnRole.Predicted)?.ApplyAuthoritativeAction(
                                PlayerWeaponActionKind.ReloadStarted, reload.weaponSlot, default);
                        FindWeapon(reload.netId, PawnRole.Interpolated)?.PresentRemoteReload(reload.weaponSlot);
                        break;
                    case WeaponActionCanceledEventMessage canceled:
                        if (canceled.serverInitiated)
                            FindWeapon(canceled.netId, PawnRole.Predicted)?.ApplyAuthoritativeAction(
                                PlayerWeaponActionKind.Canceled, canceled.weaponSlot, canceled.canceledState);
                        FindWeapon(canceled.netId, PawnRole.Interpolated)?.PresentRemoteActionCanceled(
                            canceled.weaponSlot, canceled.canceledState);
                        break;
                }
            }
            catch (Exception exception)
            {
                ZLog.LogError<EventMessageHandler>(
                    $"服务器事件 {message.eventId} 的玩家 {message.netId} 表现失败。\n{exception}");
            }
            return true;
        }

        private PlayerWeaponController FindWeapon(uint netId, PawnRole role)
        {
            if (!_players.Players.TryGetValue(netId, out var player) || player == null)
                return null;
            if (!player.TryGetComponent(out PlayerBrain brain) || brain.Role != role)
                return null;
            return player.GetComponent<PlayerWeaponController>();
        }
    }
}
