using System;
using System.Linq;
using Core.Utility;
using GamePlay.Player;
using GamePlay.Player.Presentation;
using GamePlay.Player.Simulation;
using Mirror;
using Newtonsoft.Json.Linq;
using Unity.Pipeline.Commands;
using UnityEditor;
using UnityEngine;

namespace FPSGame.Editor.Validation
{
    /// <summary>联机验收只读探针，不设置视角、不注入输入或求解姿态。</summary>
    public static class PlayerAimPoseProbe
    {
        [CliCommand("player_aim_pose_probe", "Read live TP aim and FP isolation metrics without changing players", Tags = new[] { "fpsgame/validation" })]
        public static JObject Capture()
        {
            if (!Application.isPlaying) throw new InvalidOperationException("请在 Play Mode 中读取姿态探针。");
            var players = new JArray();
            foreach (PlayerBrain brain in UnityEngine.Object.FindObjectsByType<PlayerBrain>()
                .OrderBy(value => value.GetComponent<NetworkIdentity>()?.netId ?? 0u))
            {
                var motion = brain.GetComponent<PlayerMotionController>();
                var sight = brain.GetComponent<PlayerSightController>();
                var aim = brain.GetComponent<PlayerAimPoseController>();
                var holder = brain.GetComponent<PlayerHolder>();
                var model = brain.GetComponent<PlayerModelController>();
                var presenter = brain.GetComponent<PlayerAnimatorController>();
                var actor = presenter != null ? new SerializedObject(presenter).FindProperty("_mainAnimator")?.objectReferenceValue as Animator : null;
                var bones = new JArray();
                if (actor != null)
                {
                    string[] names = { "spine_0", "spine_1", "spine_2", "spine_3", "neck_0", "head_0", "hand_L", "hand_R" };
                    foreach (Transform bone in actor.GetComponentsInChildren<Transform>(true).Where(value => names.Contains(value.name)))
                        bones.Add(new JObject { ["name"] = bone.name,
                            ["path"] = AnimationUtility.CalculateTransformPath(bone, actor.transform),
                            ["localPosition"] = Vector(bone.localPosition), ["localRotation"] = Rotation(bone.localRotation),
                            ["worldPosition"] = Vector(bone.position), ["worldRotation"] = Rotation(bone.rotation) });
                }
                var mount = holder != null ? holder.WeaponMount : null;
                players.Add(new JObject
                {
                    ["netId"] = brain.GetComponent<NetworkIdentity>()?.netId ?? 0u,
                    ["role"] = brain.Role.ToString(), ["view"] = motion?.StateMachine.View.ToString(),
                    ["yaw"] = brain.transform.eulerAngles.y, ["appliedPitch"] = sight != null ? new JValue(sight.AppliedPitch) : JValue.CreateNull(),
                    ["sensitivity"] = sight != null ? new JValue(sight.Sensitivity) : JValue.CreateNull(),
                    ["simulationPitch"] = brain.Role == PawnRole.Interpolated ? JValue.CreateNull() : new JValue(brain.State.pitch),
                    ["hasAimComponent"] = aim != null, ["hasAppliedPose"] = aim != null && aim.HasAppliedPose,
                    ["hasCompleteBindings"] = HasBindings(aim),
                    ["bodyPitch"] = aim != null ? new JValue(aim.LastBodyPitch) : JValue.CreateNull(),
                    ["posePitch"] = aim != null ? new JValue(aim.LastAppliedPitch) : JValue.CreateNull(),
                    ["aimWeight"] = aim != null ? new JValue(aim.CurrentAimWeight) : JValue.CreateNull(),
                    ["appliedFrameCount"] = aim != null ? aim.AppliedFrameCount : 0,
                    ["ikFrameCount"] = aim != null ? aim.IKFrameCount : 0,
                    ["fallbackCount"] = aim != null ? aim.FallbackCount : 0,
                    ["slot"] = holder?.CurrentSlot?.ToString(), ["gunState"] = (holder?.CurrentWeapon as GamePlay.HoldSystem.Gun)?.GunState.ToString(),
                    ["actorUnderTpRoot"] = actor != null && model != null && actor.transform.IsChildOf(model.ThirdPersonRoot),
                    ["mountLocalToPlayerPosition"] = mount != null ? Vector(brain.transform.InverseTransformPoint(mount.position)) : null,
                    ["mountLocalToPlayerRotation"] = mount != null ? Rotation(Quaternion.Inverse(brain.transform.rotation) * mount.rotation) : null,
                    ["mountWorldPosition"] = mount != null ? Vector(mount.position) : null,
                    ["mountWorldRotation"] = mount != null ? Rotation(mount.rotation) : null,
                    ["bones"] = bones
                });
            }
            return new JObject { ["playing"] = Application.isPlaying, ["frame"] = Time.frameCount,
                ["time"] = Time.unscaledTimeAsDouble, ["serverActive"] = NetworkServer.active,
                ["clientConnected"] = NetworkClient.isConnected, ["players"] = players };
        }

        private static JObject Vector(Vector3 value) => new JObject { ["x"] = value.x, ["y"] = value.y, ["z"] = value.z };
        private static bool HasBindings(PlayerAimPoseController aim)
        {
            if (aim == null) return false;
            var serialized = new SerializedObject(aim);
            string[] fields = { "_thirdPersonRoot", "_animator", "_neck", "_head", "_leftUpperArm", "_leftForearm",
                "_leftHand", "_rightUpperArm", "_rightForearm", "_rightHand", "_weaponMount" };
            foreach (string field in fields)
                if (serialized.FindProperty(field)?.objectReferenceValue == null) return false;
            SerializedProperty spines = serialized.FindProperty("_spines");
            if (spines == null || spines.arraySize != 4) return false;
            for (int i = 0; i < spines.arraySize; i++)
                if (spines.GetArrayElementAtIndex(i).objectReferenceValue == null) return false;
            return true;
        }
        private static JObject Rotation(Quaternion value) => new JObject { ["x"] = value.x, ["y"] = value.y, ["z"] = value.z, ["w"] = value.w };
    }
}
