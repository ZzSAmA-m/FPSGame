using System.Collections.Generic;
using Core.Utility;
using GamePlay.HoldSystem;
using GamePlay.Player.Combat;
using GamePlay.Player.MotionFSM;
using GamePlay.Player.Simulation;
using UnityEngine;

namespace GamePlay.Player.Presentation
{
    /// <summary>只在 TP 为动画姿态叠加俯仰；不读取或修改 FP 挂点、相机或动画层。</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(PlayerMotionController), typeof(PlayerSightController))]
    [DefaultExecutionOrder(20000)]
    public sealed class PlayerAimPoseController : MonoBehaviour
    {
        [Header("TP 骨架绑定")]
        [SerializeField] private Transform _thirdPersonRoot;
        [SerializeField] private Animator _animator;
        [SerializeField] private Transform[] _spines = new Transform[4];
        [SerializeField] private Transform _neck;
        [SerializeField] private Transform _head;
        [SerializeField] private Transform _leftUpperArm;
        [SerializeField] private Transform _leftForearm;
        [SerializeField] private Transform _leftHand;
        [SerializeField] private Transform _rightUpperArm;
        [SerializeField] private Transform _rightForearm;
        [SerializeField] private Transform _rightHand;
        [SerializeField] private Transform _weaponMount;

        [Header("躯干与视线角度分配")]
        [SerializeField] private Vector4 _spineWeights = new Vector4(0.1f, 0.2f, 0.3f, 0.4f);
        [SerializeField, Range(0f, 1f)] private float _bodyPitchRatio = 0.65f;
        [SerializeField, Range(0f, 89f)] private float _bodyUpLimit = 35f;
        [SerializeField, Range(0f, 89f)] private float _bodyDownLimit = 50f;
        [SerializeField, Range(0f, 1f)] private float _neckResidualRatio = 0.6f;
        [SerializeField, Range(0f, 89f)] private float _neckLimit = 35f;
        [SerializeField, Range(0f, 89f)] private float _headLimit = 25f;
        [SerializeField, Min(0f)] private float _actionBlendSeconds = 0.1f;

        private readonly HashSet<string> _diagnostics = new();
        private PlayerMotionController _motionController;
        private PlayerMotionStateMachine _motion;
        private PlayerSightController _sight;
        private PlayerHolder _holder;
        private PlayerBrain _brain;
        private PlayerWeaponController _weapon;
        private WeaponEntry _entry;
        private LocalPose[] _animatedPose;
        private bool _subscribed;
        private bool _configurationValidated;
        private bool _hasAppliedPose;
        private Vector3 _lastLeftPoleLocal;
        private Vector3 _lastRightPoleLocal;
        private bool _serverDriven;
        private PlayerCombatPoseController _combatPose;

        public int AppliedFrameCount { get; private set; }
        public int IKFrameCount { get; private set; }
        public int FallbackCount { get; private set; }
        public float CurrentAimWeight { get; private set; }
        public float LastBodyPitch { get; private set; }
        public float LastAppliedPitch { get; private set; }
        public bool HasAppliedPose => _hasAppliedPose;

        private bool IsThirdPerson => _motion != null && _motion.IsStarted && _motion.View == EPlayerModelType.TP;

        private void OnEnable() => ResolveDependencies();

        private void OnDisable()
        {
            _combatPose?.RestoreRemotePose();
            RestoreAnimatedPose();
            Unsubscribe();
            ResetTransition();
        }

        private void Update()
        {
            if (_serverDriven) return;
            _combatPose?.RestoreRemotePose();
            // FP 状态在模式门禁后立即返回，不采样或恢复共享骨骼。
            if (_motion == null || _motion.View != EPlayerModelType.TP) return;
            RestoreAnimatedPose();
        }

        private void LateUpdate()
        {
            if (!_serverDriven) ApplyCurrentPose(Time.unscaledDeltaTime);
        }

        public void SetServerDriven(bool serverDriven) => _serverDriven = serverDriven;

        /// <summary>在 Animator 求值后调用；手动验证也必须遵守相同 TP 门禁。</summary>
        public bool ApplyCurrentPose(float deltaTime)
        {
            if (!isActiveAndEnabled) return false;
            ResolveDependencies();
            if (_motion == null || _motion.View != EPlayerModelType.TP) return false;
            RestoreAnimatedPose();
            if (!_motion.IsStarted) return false;
            if (_sight == null || !ValidateConfiguration()) return false;

            if (!ReferenceEquals(_entry, _holder != null ? _holder.CurrentEntry : null))
            {
                _entry = _holder != null ? _holder.CurrentEntry : null;
                ResetTransition();
            }
            Gun gun = _holder != null ? _holder.CurrentWeapon as Gun : null;
            bool remotePlayer = _brain != null && _brain.Role == PawnRole.Interpolated;
            bool canAim = gun != null && (remotePlayer
                ? _weapon == null || (!_weapon.IsRemotePreparing && !_weapon.IsRemoteReloading)
                : gun.GunState == GunState.Normal || gun.GunState == GunState.Shooting);
            canAim &= _brain == null || _brain.Animator == null || !_brain.Animator.IsReloadAnimationPlaying;
            float targetWeight = canAim ? 1f : 0f;
            float step = _actionBlendSeconds <= 0f ? 1f : Mathf.Max(0f, Finite(deltaTime) ? deltaTime : 0f) / _actionBlendSeconds;
            CurrentAimWeight = Mathf.MoveTowards(CurrentAimWeight, targetWeight, step);

            float pitch = _sight.AppliedPitch;
            if (!Finite(pitch)) return Reject("TP 俯仰不是有限角度，跳过本帧覆盖。");
            LastAppliedPitch = pitch;
            LastBodyPitch = Mathf.Clamp(pitch * _bodyPitchRatio, -_bodyUpLimit, _bodyDownLimit);
            // 原动画在零输入时保持逐值不变，不用 IK 重建原本的肘形。
            if (Mathf.Abs(pitch) < 0.000001f) return false;

            CaptureAnimatedPose();
            ArmFrame leftBefore = CaptureArm(_leftUpperArm, _leftForearm, _leftHand);
            ArmFrame rightBefore = CaptureArm(_rightUpperArm, _rightForearm, _rightHand);
            Vector3 shoulderBefore = (leftBefore.Shoulder + rightBefore.Shoulder) * 0.5f;
            Pose mountBefore = new Pose(_weaponMount.position, _weaponMount.rotation);
            Matrix4x4 rightBeforeMatrix = _rightHand.localToWorldMatrix;
            Matrix4x4 mountBeforeMatrix = _weaponMount.localToWorldMatrix;
            Vector3 rightAxis = transform.right.normalized;

            float sum = _spineWeights.x + _spineWeights.y + _spineWeights.z + _spineWeights.w;
            for (int i = 0; i < 4; i++)
                RotateWorld(_spines[i], LastBodyPitch * _spineWeights[i] / sum, rightAxis);
            float residual = pitch - LastBodyPitch;
            RotateWorld(_neck, Mathf.Clamp(residual * _neckResidualRatio, -_neckLimit, _neckLimit), rightAxis);
            RotateWorld(_head, Mathf.Clamp(residual * (1f - _neckResidualRatio), -_headLimit, _headLimit), rightAxis);

            Matrix4x4 bodyOnlyMount = _rightHand.localToWorldMatrix * rightBeforeMatrix.inverse * mountBeforeMatrix;
            SetWorldPose(_weaponMount, bodyOnlyMount.GetColumn(3), bodyOnlyMount.rotation);
            AppliedFrameCount++;

            if (CurrentAimWeight <= 0.000001f || gun == null) return true;

            ArmFrame leftBody = CaptureArm(_leftUpperArm, _leftForearm, _leftHand);
            ArmFrame rightBody = CaptureArm(_rightUpperArm, _rightForearm, _rightHand);
            Vector3 shoulderAfter = (leftBody.Shoulder + rightBody.Shoulder) * 0.5f;
            float effectivePitch = Mathf.Lerp(LastBodyPitch, pitch, CurrentAimWeight);
            Quaternion targetRotation = Quaternion.AngleAxis(effectivePitch, rightAxis);
            Vector3 leftTarget = shoulderAfter + targetRotation * (leftBefore.Hand - shoulderBefore);
            Vector3 rightTarget = shoulderAfter + targetRotation * (rightBefore.Hand - shoulderBefore);
            Vector3 mountTarget = shoulderAfter + targetRotation * (mountBefore.position - shoulderBefore);

            if (!AimPoseMath.TryFindSharedTranslation(
                    leftBody.Shoulder, leftTarget, Vector3.Distance(leftBody.Shoulder, leftBody.Elbow), Vector3.Distance(leftBody.Elbow, leftBody.Hand),
                    rightBody.Shoulder, rightTarget, Vector3.Distance(rightBody.Shoulder, rightBody.Elbow), Vector3.Distance(rightBody.Elbow, rightBody.Hand),
                    out Vector3 translation))
                return FallBack();

            Vector3 leftPole = Pole(leftBefore, targetRotation, true);
            Vector3 rightPole = Pole(rightBefore, targetRotation, false);
            // 两臂先全部求解；失败时尚未写入任何一侧的 IK，保留完整 body-only 姿态。
            if (!AimPoseMath.TrySolveArm(leftBody.Shoulder, leftBody.Elbow, leftBody.Hand,
                    leftBody.UpperRotation, leftBody.LowerRotation, leftBody.HandRotation,
                    leftTarget + translation, targetRotation * leftBefore.HandRotation, leftPole, out ArmSolution left) ||
                !AimPoseMath.TrySolveArm(rightBody.Shoulder, rightBody.Elbow, rightBody.Hand,
                    rightBody.UpperRotation, rightBody.LowerRotation, rightBody.HandRotation,
                    rightTarget + translation, targetRotation * rightBefore.HandRotation, rightPole, out ArmSolution right))
                return FallBack();

            _leftUpperArm.rotation = left.UpperRotation;
            _leftForearm.rotation = left.LowerRotation;
            _leftHand.rotation = left.HandRotation;
            _rightUpperArm.rotation = right.UpperRotation;
            _rightForearm.rotation = right.LowerRotation;
            _rightHand.rotation = right.HandRotation;
            SetWorldPose(_weaponMount, mountTarget + translation, targetRotation * mountBefore.rotation);
            IKFrameCount++;
            return true;
        }

        /// <summary>必须在离开 TP、移动共享模型之前恢复；FP 下此方法不读写骨骼。</summary>
        public void RestoreAnimatedPose()
        {
            if (!_hasAppliedPose) return;
            if (_motion == null || _motion.View != EPlayerModelType.TP)
            {
                _hasAppliedPose = false;
                return;
            }
            if (_animatedPose != null)
                for (int i = 0; i < _animatedPose.Length; i++)
                {
                    LocalPose pose = _animatedPose[i];
                    if (pose.Bone == null) continue;
                    pose.Bone.localRotation = pose.Rotation;
                    if (pose.RestorePosition) pose.Bone.localPosition = pose.Position;
                }
            _hasAppliedPose = false;
        }

        private void ResolveDependencies()
        {
            if (_motionController == null) _motionController = GetComponent<PlayerMotionController>();
            if (_sight == null) _sight = GetComponent<PlayerSightController>();
            if (_holder == null) _holder = GetComponent<PlayerHolder>();
            if (_brain == null) _brain = GetComponent<PlayerBrain>();
            if (_weapon == null) _weapon = GetComponent<PlayerWeaponController>();
            if (_combatPose == null) _combatPose = GetComponent<PlayerCombatPoseController>();
            if (_motionController == null || _subscribed) return;
            _motion = _motionController.StateMachine;
            _motion.OnViewChanging += OnViewChanging;
            if (_holder != null) _holder.OnWeaponChanged += OnWeaponChanged;
            _subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!_subscribed) return;
            if (_motion != null) _motion.OnViewChanging -= OnViewChanging;
            if (_holder != null) _holder.OnWeaponChanged -= OnWeaponChanged;
            _subscribed = false;
        }

        private void OnViewChanging(EPlayerModelType oldView, EPlayerModelType newView)
        {
            _combatPose?.RestoreRemotePose();
            if (oldView == EPlayerModelType.TP) RestoreAnimatedPose();
            ResetTransition();
        }

        private void OnWeaponChanged()
        {
            if (IsThirdPerson) RestoreAnimatedPose();
            _entry = _holder != null ? _holder.CurrentEntry : null;
            ResetTransition();
        }

        private void ResetTransition()
        {
            CurrentAimWeight = 0f;
            _lastLeftPoleLocal = Vector3.zero;
            _lastRightPoleLocal = Vector3.zero;
        }

        private bool ValidateConfiguration()
        {
            if (_configurationValidated) return true;
            PlayerModelController model = GetComponent<PlayerModelController>();
            if (_thirdPersonRoot == null || !_thirdPersonRoot.IsChildOf(transform) || _animator == null ||
                model == null || _thirdPersonRoot != model.ThirdPersonRoot ||
                !_animator.transform.IsChildOf(_thirdPersonRoot) || _animator.updateMode != AnimatorUpdateMode.Normal)
                return Reject("TP 姿态需要角色自身 TP 根节点内的 Normal Animator。");
            if (_spines == null || _spines.Length != 4)
                return Reject("TP 姿态必须配置四段脊柱。");
            Transform[] bones = { _spines[0], _spines[1], _spines[2], _spines[3], _neck, _head,
                _leftUpperArm, _leftForearm, _leftHand, _rightUpperArm, _rightForearm, _rightHand, _weaponMount };
            var unique = new HashSet<Transform>();
            foreach (Transform bone in bones)
                if (bone == null || !bone.IsChildOf(_animator.transform) || !bone.IsChildOf(_thirdPersonRoot) || !unique.Add(bone))
                    return Reject("TP 骨骼绑定缺失、重复或不属于当前 TP 骨架。");
            if (!_spines[1].IsChildOf(_spines[0]) || !_spines[2].IsChildOf(_spines[1]) || !_spines[3].IsChildOf(_spines[2]) ||
                !_neck.IsChildOf(_spines[3]) || !_head.IsChildOf(_neck) ||
                !_leftUpperArm.IsChildOf(_spines[3]) || !_leftForearm.IsChildOf(_leftUpperArm) || !_leftHand.IsChildOf(_leftForearm) ||
                !_rightUpperArm.IsChildOf(_spines[3]) || !_rightForearm.IsChildOf(_rightUpperArm) || !_rightHand.IsChildOf(_rightForearm) ||
                _rightHand.IsChildOf(_weaponMount) || _leftHand.IsChildOf(_weaponMount))
                return Reject("TP 脊柱、颈头或双臂层级不符合骨骼链，或挂点造成依赖环。");
            if (_holder != null && _holder.WeaponMount != null && _holder.WeaponMount != _weaponMount)
                return Reject("TP 姿态挂点必须与装备栏使用同一 wpn。");
            float sum = _spineWeights.x + _spineWeights.y + _spineWeights.z + _spineWeights.w;
            if (!Finite(sum) || sum <= 0f || _spineWeights.x < 0f || _spineWeights.y < 0f || _spineWeights.z < 0f || _spineWeights.w < 0f)
                return Reject("TP 脊柱权重必须非负且总和大于零。");
            if (!Finite(_bodyPitchRatio) || !Finite(_bodyUpLimit) || !Finite(_bodyDownLimit) || !Finite(_neckResidualRatio) ||
                !Finite(_neckLimit) || !Finite(_headLimit) || !Finite(_actionBlendSeconds))
                return Reject("TP 姿态参数必须为有限数值。");
            _animatedPose = new LocalPose[bones.Length];
            for (int i = 0; i < bones.Length; i++) _animatedPose[i].Bone = bones[i];
            _configurationValidated = true;
            return true;
        }

        private void CaptureAnimatedPose()
        {
            for (int i = 0; i < _animatedPose.Length; i++)
            {
                Transform bone = _animatedPose[i].Bone;
                _animatedPose[i].Rotation = bone.localRotation;
                _animatedPose[i].RestorePosition = bone == _weaponMount;
                if (_animatedPose[i].RestorePosition) _animatedPose[i].Position = bone.localPosition;
            }
            _hasAppliedPose = true;
        }

        private Vector3 Pole(in ArmFrame frame, Quaternion rotation, bool left)
        {
            Vector3 line = frame.Hand - frame.Shoulder;
            Vector3 pole = frame.Elbow - frame.Shoulder;
            if (line.sqrMagnitude > 0.00000001f) pole -= Vector3.Project(pole, line);
            if (pole.sqrMagnitude > 0.00000001f)
            {
                // 缓存动画本身的弯肘方向；本帧俯仰在所有分支的出口统一施加。
                pole.Normalize();
                if (left) _lastLeftPoleLocal = transform.InverseTransformDirection(pole);
                else _lastRightPoleLocal = transform.InverseTransformDirection(pole);
                return (rotation * pole).normalized;
            }
            Vector3 cached = left ? _lastLeftPoleLocal : _lastRightPoleLocal;
            if (cached.sqrMagnitude > 0.00000001f) return (rotation * transform.TransformDirection(cached)).normalized;
            Vector3 fallback = (left ? -transform.right : transform.right) - transform.up * 0.35f - transform.forward * 0.25f;
            return (rotation * fallback).normalized;
        }

        private static ArmFrame CaptureArm(Transform upper, Transform lower, Transform hand) => new ArmFrame
        {
            Shoulder = upper.position, Elbow = lower.position, Hand = hand.position,
            UpperRotation = upper.rotation, LowerRotation = lower.rotation, HandRotation = hand.rotation
        };

        private static void RotateWorld(Transform bone, float degrees, Vector3 axis) =>
            bone.rotation = Quaternion.AngleAxis(degrees, axis) * bone.rotation;

        private static void SetWorldPose(Transform bone, Vector3 position, Quaternion rotation) =>
            bone.SetPositionAndRotation(position, rotation);

        private bool FallBack()
        {
            FallbackCount++;
            Reject("TP 双臂瞄准目标不可达，本帧保留完整动画与身体俯仰，不拉伸骨骼。");
            return true;
        }

        private bool Reject(string message)
        {
            if (_diagnostics.Add(message)) Debug.LogWarning($"[{nameof(PlayerAimPoseController)}] {message}", this);
            return false;
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        private void OnValidate()
        {
            if (Application.isPlaying) RestoreAnimatedPose();
            _configurationValidated = false;
            _bodyPitchRatio = Mathf.Clamp01(Finite(_bodyPitchRatio) ? _bodyPitchRatio : 0.65f);
            _bodyUpLimit = Mathf.Clamp(Finite(_bodyUpLimit) ? _bodyUpLimit : 35f, 0f, 89f);
            _bodyDownLimit = Mathf.Clamp(Finite(_bodyDownLimit) ? _bodyDownLimit : 50f, 0f, 89f);
            _neckResidualRatio = Mathf.Clamp01(Finite(_neckResidualRatio) ? _neckResidualRatio : 0.6f);
            _neckLimit = Mathf.Clamp(Finite(_neckLimit) ? _neckLimit : 35f, 0f, 89f);
            _headLimit = Mathf.Clamp(Finite(_headLimit) ? _headLimit : 25f, 0f, 89f);
            _actionBlendSeconds = Mathf.Max(0f, Finite(_actionBlendSeconds) ? _actionBlendSeconds : 0.1f);
        }

        private struct LocalPose
        {
            public Transform Bone;
            public Quaternion Rotation;
            public Vector3 Position;
            public bool RestorePosition;
        }

        private struct ArmFrame
        {
            public Vector3 Shoulder, Elbow, Hand;
            public Quaternion UpperRotation, LowerRotation, HandRotation;
        }
    }
}
