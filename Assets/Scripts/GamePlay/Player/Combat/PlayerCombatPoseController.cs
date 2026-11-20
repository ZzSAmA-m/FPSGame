using System;
using Core.Utility;
using GamePlay.Player.Presentation;
using GamePlay.Player.Simulation;
using GamePlay.Player.Simulation.Reconciliation;
using UnityEngine;

namespace GamePlay.Player.Combat
{
    /// <summary>服务器按拍推进完整动画姿态，远端显示权威命中骨骼的插值结果。</summary>
    [DisallowMultipleComponent, DefaultExecutionOrder(30000)]
    public sealed class PlayerCombatPoseController : MonoBehaviour
    {
        [SerializeField] private Animator _animator;
        [SerializeField] private Transform _pelvis;
        [SerializeField] private Transform _head;
        [SerializeField] private PlayerAimPoseController _aim;

        private PlayerBrain _brain;
        private PlayerMotionController _motion;
        private PlayerAnimatorController _animation;
        private bool _serverDriven;
        private float _animationSpeed;
        private AnimatorCullingMode _cullingMode;
        private PlayerCombatPose _serverPose;
        private PlayerCombatPose _remotePose;
        private bool _remoteApplied;
        private Vector3 _animatedPelvisPosition;
        private Quaternion _animatedPelvisRotation;
        private Vector3 _animatedHeadPosition;
        private Quaternion _animatedHeadRotation;

        public bool IsServerDriven => _serverDriven;
        public Transform HeadBone => _head;
        public Transform PelvisBone => _pelvis;

        public void InitializeServer()
        {
            if (_serverDriven)
                return;
            ResolveDependencies();
            if (_brain == null || _brain.Role != PawnRole.Authoritative || !HasBindings ||
                _animator == null || _animator.updateMode != AnimatorUpdateMode.Normal ||
                _animator.runtimeAnimatorController == null)
                throw new InvalidOperationException("服务器命中姿态需要权威玩家、已绑定骨骼与 Normal Animator。");

            ClearRemotePose();
            _animationSpeed = _animator.speed;
            _cullingMode = _animator.cullingMode;
            _animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            _animator.speed = 0f;
            _serverDriven = true;
            _motion?.SetServerDriven(true);
            _animation?.SetServerDriven(true);
            _aim?.SetServerDriven(true);
            StepServer(0f);
        }

        public void StepServer(float deltaTime)
        {
            if (!_serverDriven)
                throw new InvalidOperationException("服务器命中姿态尚未初始化。");
            if (!PlayerStateValidation.IsFinite(deltaTime) || deltaTime < 0f)
                throw new ArgumentOutOfRangeException(nameof(deltaTime));

            _aim?.RestoreAnimatedPose();
            _motion?.TickServer();
            _animator.speed = _animationSpeed;
            try
            {
                _animator.Update(deltaTime);
                if (_animation != null && _animation.CompleteServerPoseStep())
                    _animator.Update(0f);
            }
            finally { _animator.speed = 0f; }
            _aim?.ApplyCurrentPose(deltaTime);
            _serverPose = ReadPose();
        }

        public PlayerCombatPose CapturePose() => _serverDriven ? _serverPose : ReadPose();

        public void SetRemotePose(in PlayerCombatPose pose)
        {
            ResolveDependencies();
            if (!pose.IsValid || _serverDriven || _brain == null || _brain.Role != PawnRole.Interpolated)
            {
                ClearRemotePose();
                return;
            }
            _remotePose = pose;
        }

        public void ClearRemotePose()
        {
            RestoreRemotePose();
            _remotePose = default;
        }

        /// <summary>在恢复 TP 覆盖与 Animator 求值之前撤销上一帧的权威骨骼覆盖。</summary>
        public void RestoreRemotePose()
        {
            if (!_remoteApplied)
                return;
            if (_pelvis != null)
            {
                _pelvis.localPosition = _animatedPelvisPosition;
                _pelvis.localRotation = _animatedPelvisRotation;
            }
            if (_head != null)
            {
                _head.localPosition = _animatedHeadPosition;
                _head.localRotation = _animatedHeadRotation;
            }
            _remoteApplied = false;
        }

        public void Shutdown()
        {
            ClearRemotePose();
            if (!_serverDriven)
                return;
            _aim?.RestoreAnimatedPose();
            _motion?.SetServerDriven(false);
            _animation?.SetServerDriven(false);
            _aim?.SetServerDriven(false);
            if (_animator != null)
            {
                _animator.speed = _animationSpeed;
                _animator.cullingMode = _cullingMode;
            }
            _serverDriven = false;
            _serverPose = default;
        }

        private void Update()
        {
            if (_aim == null || !_aim.isActiveAndEnabled)
                RestoreRemotePose();
        }

        private void LateUpdate()
        {
            if (!_remotePose.IsValid || _serverDriven || !HasBindings || _brain == null ||
                _brain.Role != PawnRole.Interpolated || _motion == null ||
                _motion.StateMachine.View != EPlayerModelType.TP)
                return;

            _animatedPelvisPosition = _pelvis.localPosition;
            _animatedPelvisRotation = _pelvis.localRotation;
            _animatedHeadPosition = _head.localPosition;
            _animatedHeadRotation = _head.localRotation;
            _remoteApplied = true;
            _pelvis.SetPositionAndRotation(transform.TransformPoint(_remotePose.pelvisPosition),
                transform.rotation * _remotePose.pelvisRotation);
            _head.SetPositionAndRotation(transform.TransformPoint(_remotePose.headPosition),
                transform.rotation * _remotePose.headRotation);
        }

        private void OnDisable() => Shutdown();

        private void ResolveDependencies()
        {
            if (_brain == null) _brain = GetComponent<PlayerBrain>();
            if (_motion == null) _motion = GetComponent<PlayerMotionController>();
            if (_animation == null) _animation = GetComponent<PlayerAnimatorController>();
            if (_aim == null) _aim = GetComponent<PlayerAimPoseController>();
        }

        private bool HasBindings => _pelvis != null && _head != null && _pelvis.IsChildOf(transform) &&
                                    _head.IsChildOf(_pelvis);

        private PlayerCombatPose ReadPose()
        {
            if (!HasBindings)
                return default;
            Quaternion inverse = Quaternion.Inverse(transform.rotation);
            return new PlayerCombatPose
            {
                valid = true,
                pelvisPosition = transform.InverseTransformPoint(_pelvis.position),
                headPosition = transform.InverseTransformPoint(_head.position),
                pelvisRotation = inverse * _pelvis.rotation,
                headRotation = inverse * _head.rotation
            };
        }
    }
}
