using System;
using System.Linq;
using System.Reflection;
using GamePlay.HoldSystem;
using GamePlay.Player;
using GamePlay.Player.Simulation;
using Mirror;
using Newtonsoft.Json.Linq;
using Unity.Pipeline.Commands;
using UnityEditor;
using UnityEngine;

namespace FPSGame.Editor.Validation
{
    /// <summary>观察真实角色和武器模型动作；换弹及取消仅通过现有玩法入口执行。</summary>
    public static class GunPresentationProbe
    {
        private static readonly FieldInfo MainAnimator = typeof(PlayerAnimatorController)
            .GetField("_mainAnimator", BindingFlags.Instance | BindingFlags.NonPublic);

        [CliCommand("gun_presentation_probe", "Inspect live actor and gun animations or request reload/cancel without saving assets", Tags = new[] { "fpsgame/validation" })]
        public static JObject Probe(string action = "capture", uint net_id = 0)
        {
            if (!Application.isPlaying)
                throw new InvalidOperationException("枪械表现探针只能在 Play Mode 中运行。");
            var operation = new JObject { ["action"] = action };
            if (action != "capture")
            {
                PlayerBrain target = FindTarget(net_id);
                PlayerHolder holder = target.GetComponent<PlayerHolder>();
                if (holder == null) throw new InvalidOperationException("目标玩家没有装备栏。");
                operation["netId"] = Identity(target);
                if (action == "reload")
                {
                    if (holder.CurrentWeapon is not Gun gun)
                        throw new InvalidOperationException("目标玩家当前没有装备枪械。");
                    operation["beforeState"] = gun.GunState.ToString();
                    operation["accepted"] = gun.TryReload();
                    operation["afterState"] = gun.GunState.ToString();
                }
                else if (action == "cancel")
                    operation["accepted"] = holder.SelectSlot(WeaponSlot.Knife);
                else
                    throw new ArgumentException("仅支持 capture、reload 和 cancel。", nameof(action));
            }

            var players = new JArray();
            foreach (PlayerBrain brain in UnityEngine.Object.FindObjectsByType<PlayerBrain>(FindObjectsSortMode.None).OrderBy(Identity))
            {
                PlayerHolder holder = brain.GetComponent<PlayerHolder>();
                WeaponController model = holder != null ? holder.CurrentController : null;
                Gun gun = holder != null ? holder.CurrentWeapon as Gun : null;
                IWeaponPresentationResources resources = holder?.CurrentEntry?.PresentationResources;
                PlayerAnimatorController actor = brain.GetComponent<PlayerAnimatorController>();
                Animator animator = actor != null && MainAnimator != null ? MainAnimator.GetValue(actor) as Animator : null;
                var models = new JArray();
                if (model != null)
                    foreach (Animator value in model.GetComponentsInChildren<Animator>(true))
                        models.Add(AnimatorData(value));

                players.Add(new JObject
                {
                    ["netId"] = Identity(brain),
                    ["role"] = brain.Role.ToString(),
                    ["slot"] = holder?.CurrentSlot?.ToString(),
                    ["definition"] = holder != null && holder.CurrentWeaponSO != null ? AssetDatabase.GetAssetPath(holder.CurrentWeaponSO) : null,
                    ["resourceInterface"] = resources?.GetType().FullName,
                    ["resourcePack"] = holder?.CurrentWeaponSO is GunSO definition && definition.ResourcePack != null
                        ? AssetDatabase.GetAssetPath(definition.ResourcePack) : null,
                    ["modelPrefab"] = resources?.ModelPrefab != null ? AssetDatabase.GetAssetPath(resources.ModelPrefab) : null,
                    ["modelControllerType"] = model != null ? model.GetType().FullName : null,
                    ["gunState"] = gun?.GunState.ToString(),
                    ["rounds"] = gun != null ? new JValue(gun.Magazine.Rounds) : JValue.CreateNull(),
                    ["reserveRounds"] = gun != null ? new JValue(gun.Magazine.ReserveRounds) : JValue.CreateNull(),
                    ["configuredAnimations"] = AnimationResources(resources),
                    ["actorAnimator"] = AnimatorData(animator),
                    ["modelAnimators"] = models
                });
            }
            return new JObject
            {
                ["ok"] = true,
                ["time"] = Time.unscaledTimeAsDouble,
                ["frame"] = Time.frameCount,
                ["serverActive"] = NetworkServer.active,
                ["clientConnected"] = NetworkClient.isConnected,
                ["localNetId"] = NetworkClient.localPlayer != null ? NetworkClient.localPlayer.netId : 0u,
                ["operation"] = operation,
                ["players"] = players
            };
        }

        private static PlayerBrain FindTarget(uint netId)
        {
            PlayerBrain[] brains = UnityEngine.Object.FindObjectsByType<PlayerBrain>(FindObjectsSortMode.None);
            PlayerBrain target = netId != 0
                ? brains.FirstOrDefault(brain => Identity(brain) == netId)
                : NetworkClient.localPlayer != null ? NetworkClient.localPlayer.GetComponent<PlayerBrain>() :
                    brains.SingleOrDefault(brain => brain.Role == PawnRole.Authoritative);
            if (target == null) throw new InvalidOperationException("没有找到可操作玩家；服务器有多个玩家时请指定 net_id。");
            if (target.Role != PawnRole.Predicted && target.Role != PawnRole.Authoritative)
                throw new InvalidOperationException("不能对插值角色或尚未绑定身份的角色注入武器操作。");
            return target;
        }

        private static uint Identity(PlayerBrain brain)
        {
            NetworkIdentity identity = brain.GetComponent<NetworkIdentity>();
            return identity != null ? identity.netId : 0u;
        }

        private static JObject AnimationResources(IWeaponPresentationResources resources)
        {
            var values = new JObject();
            if (resources == null) return values;
            foreach (WeaponAnimationTarget target in Enum.GetValues(typeof(WeaponAnimationTarget)))
            {
                var actions = new JObject();
                foreach (WeaponPresentationAction action in Enum.GetValues(typeof(WeaponPresentationAction)))
                    actions[action.ToString()] = resources.TryGetAnimation(action, target, out string name) ? name : null;
                values[target.ToString()] = actions;
            }
            return values;
        }

        private static JObject AnimatorData(Animator animator)
        {
            if (animator == null) return null;
            var result = new JObject
            {
                ["name"] = animator.name,
                ["path"] = AnimationUtility.CalculateTransformPath(animator.transform, null),
                ["enabled"] = animator.enabled,
                ["active"] = animator.gameObject.activeInHierarchy,
                ["initialized"] = animator.isInitialized,
                ["speed"] = animator.speed,
                ["descendantPoseHash"] = PoseHash(animator),
                ["controller"] = animator.runtimeAnimatorController != null ? AssetDatabase.GetAssetPath(animator.runtimeAnimatorController) : null
            };
            var layers = new JArray();
            if (animator.isInitialized)
            {
                for (int layer = 0; layer < animator.layerCount; layer++)
                {
                    AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(layer);
                    var clips = new JArray();
                    foreach (AnimatorClipInfo clip in animator.GetCurrentAnimatorClipInfo(layer))
                        clips.Add(new JObject
                        {
                            ["name"] = clip.clip != null ? clip.clip.name : null,
                            ["asset"] = clip.clip != null ? AssetDatabase.GetAssetPath(clip.clip) : null,
                            ["length"] = clip.clip != null ? clip.clip.length : 0f,
                            ["weight"] = clip.weight
                        });
                    layers.Add(new JObject
                    {
                        ["index"] = layer,
                        ["name"] = animator.GetLayerName(layer),
                        ["weight"] = animator.GetLayerWeight(layer),
                        ["fullPathHash"] = state.fullPathHash,
                        ["shortNameHash"] = state.shortNameHash,
                        ["normalizedTime"] = state.normalizedTime,
                        ["length"] = state.length,
                        ["speed"] = state.speed,
                        ["inTransition"] = animator.IsInTransition(layer),
                        ["clips"] = clips
                    });
                }
            }
            result["layers"] = layers;
            return result;
        }

        private static int PoseHash(Animator animator)
        {
            int hash = 17;
            foreach (Transform child in animator.GetComponentsInChildren<Transform>(true))
            {
                if (child == animator.transform) continue;
                unchecked
                {
                    hash = hash * 31 + child.localPosition.GetHashCode();
                    hash = hash * 31 + child.localRotation.GetHashCode();
                    hash = hash * 31 + child.localScale.GetHashCode();
                }
            }
            return hash;
        }
    }
}
