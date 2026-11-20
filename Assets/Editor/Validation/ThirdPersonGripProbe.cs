using System;
using System.Collections.Generic;
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
using Object = UnityEngine.Object;

namespace FPSGame.Editor.Validation
{
    /// <summary>联机时只观察权威 TP 的握持关系，不修改动画、挂点或网络输入。</summary>
    public static class ThirdPersonGripProbe
    {
        private static ThirdPersonGripRecorder _recorder;
        private static JObject _lastReport;

        static ThirdPersonGripProbe()
        {
            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.ExitingPlayMode)
                    Stop();
            };
            AssemblyReloadEvents.beforeAssemblyReload += Stop;
        }

        [CliCommand("gun_tp_grip_probe", "Observe live authoritative TP weapon grip without changing presentation", Tags = new[] { "fpsgame/validation" })]
        public static JObject Run(string action = "report", uint net_id = 0)
        {
            if (action == "stop")
            {
                Stop();
                return _lastReport ?? new JObject { ["running"] = false };
            }
            if (action == "report")
                return _recorder != null ? _recorder.Report() :
                    _lastReport ?? new JObject { ["running"] = false };
            if (action != "start")
                throw new ArgumentException("仅支持 start、report 和 stop。", nameof(action));
            if (!Application.isPlaying)
                throw new InvalidOperationException("请在 Play Mode 中启动握持探针。");

            PlayerBrain[] candidates = Object.FindObjectsByType<PlayerBrain>()
                .Where(brain => brain.Role == PawnRole.Authoritative &&
                    (net_id == 0 || brain.GetComponent<NetworkIdentity>()?.netId == net_id)).ToArray();
            if (candidates.Length != 1)
                throw new InvalidOperationException("必须唯一定位权威玩家；服务器有多个玩家时请指定 net_id。");
            Stop();
            var root = new GameObject("TPGripValidationProbe") { hideFlags = HideFlags.HideAndDontSave };
            Object.DontDestroyOnLoad(root);
            try
            {
                _recorder = root.AddComponent<ThirdPersonGripRecorder>();
                _recorder.Begin(candidates[0]);
                return _recorder.Report();
            }
            catch
            {
                Object.DestroyImmediate(root);
                _recorder = null;
                throw;
            }
        }

        private static void Stop()
        {
            if (_recorder == null) return;
            _lastReport = _recorder.Report();
            _lastReport["running"] = false;
            _recorder.Detach();
            Object.DestroyImmediate(_recorder.gameObject);
            _recorder = null;
        }
    }

    /// <summary>在本拍所有实时输入与 Animator 求值后读数；只存在于编辑器验证程序集。</summary>
    [DefaultExecutionOrder(32000)]
    public sealed class ThirdPersonGripRecorder : MonoBehaviour
    {
        private static readonly FieldInfo MainAnimator = typeof(PlayerAnimatorController)
            .GetField("_mainAnimator", BindingFlags.Instance | BindingFlags.NonPublic);
        private readonly HashSet<string> _errors = new();
        private readonly List<WeaponController> _retiredModels = new();
        private PlayerBrain _brain;
        private PlayerHolder _holder;
        private PlayerWeaponController _forwarder;
        private Gun _gun;
        private WeaponEntry _entry;
        private WeaponController _model;
        private Animator _actor;
        private Animator _modelAnimator;
        private Transform _right;
        private Transform _left;
        private Transform _mount;
        private Transform _body;
        private Pose _rightMount;
        private Pose _rightModel;
        private Pose _rightBody;
        private Pose _mountLeft;
        private int _actorFireHash;
        private int _actorHoldHash;
        private int _shots;
        private int _forwardedShots;
        private int _samples;
        private int _holdReturns;
        private int _switches;
        private int _switchCancellations;
        private int _actionCancellations;
        private int _destroyedOldModels;
        private int _lastShotFrame;
        private bool _tracking;
        private bool _sawFire;
        private bool _attached;
        private float _rightPosition;
        private float _rightRotation;
        private float _leftPosition;
        private float _leftRotation;
        private float _modelPosition;
        private float _modelRotation;
        private float _bodyPosition;
        private float _bodyRotation;
        private float _minimumShotTime = float.PositiveInfinity;
        private float _maximumShotTime;
        private uint _netId;
        private string _modelBodyPath;

        public void Begin(PlayerBrain brain)
        {
            _brain = brain;
            _holder = brain.GetComponent<PlayerHolder>();
            _forwarder = brain.GetComponent<PlayerWeaponController>();
            _entry = _holder != null ? _holder.CurrentEntry : null;
            _gun = _entry?.Weapon as Gun;
            _model = _holder != null ? _holder.CurrentController : null;
            var presenter = brain.GetComponent<PlayerAnimatorController>();
            _actor = presenter != null ? MainAnimator?.GetValue(presenter) as Animator : null;
            _modelAnimator = (_model as GunModelController)?.ModelAnimator;
            Require(_gun != null && _gun.GunState == GunState.Normal && _model != null &&
                    _actor != null && _actor.isInitialized && _modelAnimator != null && _modelAnimator.isInitialized,
                "目标必须持有已准备完毕的真实枪械和角色/模型 Animator。");
            IWeaponPresentationResources resources = _entry.PresentationResources;
            Require(resources != null, "缺少当前装备资源。");
            Require(resources.TryGetAnimation(WeaponPresentationAction.Hold,
                    WeaponAnimationTarget.ThirdPerson, out string actorHold), "缺少 TP 持握动作。");
            Require(resources.TryGetAnimation(WeaponPresentationAction.Fire,
                    WeaponAnimationTarget.ThirdPerson, out string actorFire), "缺少 TP 开火动作。");
            Require(resources.TryGetAnimation(WeaponPresentationAction.Hold,
                    WeaponAnimationTarget.Model, out string modelHold), "缺少模型持握动作。");
            _actorHoldHash = Animator.StringToHash(actorHold);
            _actorFireHash = Animator.StringToHash(actorFire);
            AnimatorStateInfo actorState = _actor.GetCurrentAnimatorStateInfo(PlayerAnimatorController.UpLayer);
            AnimatorStateInfo modelState = _modelAnimator.GetCurrentAnimatorStateInfo(0);
            Require(Matches(actorState, _actorHoldHash) && actorState.normalizedTime >= 1f &&
                    Matches(modelState, Animator.StringToHash(modelHold)) && modelState.normalizedTime >= 1f &&
                    _actor.GetLayerWeight(PlayerAnimatorController.FPLayer) == 0f,
                "请等待 TP 角色和模型都回到持握末帧后再 start，不能在开火中途重新取基线。");

            _right = FindUnique(_actor.transform, "hand_R");
            _left = FindUnique(_actor.transform, "hand_L");
            _mount = _holder.WeaponMount;
            Require(_mount != null && _model.transform.parent == _mount, "真实装备必须直接挂在 wpn。");
            _body = FindUnique(_model.transform, "weapon_offset");
            _modelBodyPath = AnimationUtility.CalculateTransformPath(_body, _model.transform);
            _rightMount = Relative(_right, _mount);
            _rightModel = Relative(_right, _model.transform);
            _rightBody = Relative(_right, _body);
            _mountLeft = Relative(_mount, _left);
            _netId = brain.GetComponent<NetworkIdentity>()?.netId ?? 0;
            _gun.OnShot += OnShot;
            _gun.OnActionCanceled += OnActionCanceled;
            if (_forwarder != null) _forwarder.OnShot += OnForwardedShot;
            _holder.OnWeaponChanged += OnWeaponChanged;
            _attached = true;
        }

        private void OnShot()
        {
            _shots++;
            _tracking = true;
            _sawFire = false;
            _lastShotFrame = Time.frameCount;
        }

        private void OnForwardedShot(Gun gun)
        {
            if (ReferenceEquals(gun, _gun)) _forwardedShots++;
        }

        private void OnActionCanceled(GunState state)
        {
            if (_tracking) _actionCancellations++;
        }

        private void OnWeaponChanged()
        {
            if (_holder == null || ReferenceEquals(_holder.CurrentEntry, _entry)) return;
            _switches++;
            if (_tracking) _switchCancellations++;
            if (_model != null) _retiredModels.Add(_model);
            _tracking = false;
            DetachGun();
        }

        private void LateUpdate()
        {
            for (int i = _retiredModels.Count - 1; i >= 0; i--)
            {
                if (_retiredModels[i] != null) continue;
                _destroyedOldModels++;
                _retiredModels.RemoveAt(i);
            }
            if (!_attached || !_tracking || _actor == null || Time.frameCount == _lastShotFrame) return;
            if (_brain == null || _holder == null || !ReferenceEquals(_holder.CurrentEntry, _entry) || _model == null)
            {
                _errors.Add("采样时权威角色或原装备已丢失，且未收到成对切换通知。");
                _tracking = false;
                return;
            }
            AnimatorStateInfo state = _actor.GetCurrentAnimatorStateInfo(PlayerAnimatorController.UpLayer);
            if (Matches(state, _actorFireHash))
            {
                _sawFire = true;
                _samples++;
                _minimumShotTime = Mathf.Min(_minimumShotTime, state.normalizedTime);
                _maximumShotTime = Mathf.Max(_maximumShotTime, state.normalizedTime);
                Measure(Relative(_right, _mount), _rightMount, ref _rightPosition, ref _rightRotation);
                Measure(Relative(_mount, _left), _mountLeft, ref _leftPosition, ref _leftRotation);
                Measure(Relative(_right, _model.transform), _rightModel, ref _modelPosition, ref _modelRotation);
                Measure(Relative(_right, _body), _rightBody, ref _bodyPosition, ref _bodyRotation);
                if (_model.transform.parent != _mount) _errors.Add("真实装备不再位于原 wpn 挂点。");
                if (_rightPosition >= 0.001f || _rightRotation >= 0.1f) _errors.Add("右手与 wpn 相对位姿超过 1mm / 0.1°。");
                if (_leftPosition >= 0.01f || _leftRotation >= 1f) _errors.Add("左手相对枪的漂移超过 1cm / 1°。");
                if (_modelPosition >= 0.001f || _modelRotation >= 0.1f) _errors.Add("真实装备根相对右手超过 1mm / 0.1°。");
                if (_bodyPosition >= 0.001f || _bodyRotation >= 0.1f) _errors.Add("真实枪身骨骼相对右手超过 1mm / 0.1°。");
            }
            else if (_sawFire && Matches(state, _actorHoldHash) && state.normalizedTime >= 1f)
            {
                _holdReturns++;
                _tracking = false;
            }
        }

        public JObject Report() => new()
        {
            ["running"] = _attached,
            ["netId"] = _netId,
            ["role"] = _brain != null ? _brain.Role.ToString() : null,
            ["gunShotCount"] = _shots,
            ["forwardedShotCount"] = _forwardedShots,
            ["callbackCountsMatch"] = _shots == _forwardedShots,
            ["sampleCount"] = _samples,
            ["trackingShot"] = _tracking,
            ["holdReturnCount"] = _holdReturns,
            ["switchCount"] = _switches,
            ["switchCancellationCount"] = _switchCancellations,
            ["actionCancellationCount"] = _actionCancellations,
            ["destroyedOldModelCount"] = _destroyedOldModels,
            ["oldModelCleanupPending"] = _retiredModels.Count,
            ["oldGunDetached"] = _gun == null,
            ["modelBodyPath"] = _modelBodyPath,
            ["minimumShotNormalizedTime"] = float.IsPositiveInfinity(_minimumShotTime) ? JValue.CreateNull() : new JValue(_minimumShotTime),
            ["maximumShotNormalizedTime"] = _maximumShotTime,
            ["maximumRightGripPositionError"] = _rightPosition,
            ["maximumRightGripRotationErrorDegrees"] = _rightRotation,
            ["maximumLeftGripPositionError"] = _leftPosition,
            ["maximumLeftGripRotationErrorDegrees"] = _leftRotation,
            ["maximumModelRootPositionError"] = _modelPosition,
            ["maximumModelRootRotationErrorDegrees"] = _modelRotation,
            ["maximumGunBodyPositionError"] = _bodyPosition,
            ["maximumGunBodyRotationErrorDegrees"] = _bodyRotation,
            ["errors"] = new JArray(_errors.OrderBy(error => error)),
            ["withinGripLimits"] = _samples > 0 && _errors.Count == 0
        };

        public void Detach()
        {
            if (_holder != null) _holder.OnWeaponChanged -= OnWeaponChanged;
            if (_forwarder != null) _forwarder.OnShot -= OnForwardedShot;
            DetachGun();
            _attached = false;
        }

        private void DetachGun()
        {
            if (_gun == null) return;
            _gun.OnShot -= OnShot;
            _gun.OnActionCanceled -= OnActionCanceled;
            _gun = null;
        }

        private void OnDestroy() => Detach();

        private static Pose Relative(Transform parent, Transform child)
        {
            Matrix4x4 relative = parent.worldToLocalMatrix * child.localToWorldMatrix;
            return new Pose(relative.GetColumn(3), relative.rotation);
        }

        private void Measure(Pose actual, Pose expected, ref float position, ref float rotation)
        {
            float distance = Vector3.Distance(actual.position, expected.position);
            float angle = Quaternion.Angle(actual.rotation, expected.rotation);
            if (float.IsNaN(distance) || float.IsInfinity(distance) || float.IsNaN(angle) || float.IsInfinity(angle))
            {
                _errors.Add("采样的相对位姿包含非有限数值。");
                return;
            }
            position = Mathf.Max(position, distance);
            rotation = Mathf.Max(rotation, angle);
        }

        private static bool Matches(AnimatorStateInfo state, int hash) => state.fullPathHash == hash || state.shortNameHash == hash;

        private static Transform FindUnique(Transform root, string name)
        {
            Transform[] found = root.GetComponentsInChildren<Transform>(true).Where(value => value.name == name).ToArray();
            Require(found.Length == 1, "无法唯一定位真实骨骼：" + name);
            return found[0];
        }

        private static void Require(bool value, string message)
        {
            if (!value) throw new InvalidOperationException(message);
        }
    }
}
