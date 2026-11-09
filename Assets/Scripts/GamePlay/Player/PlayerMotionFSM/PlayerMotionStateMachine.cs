using System;
using Core.Utility;
using GamePlay.FSM;
using UnityEngine;

namespace GamePlay.Player.MotionFSM
{
    /// <summary>独立的角色动作状态机，只接收动作上下文并发布语义事件。</summary>
    public sealed class PlayerMotionStateMachine
    {
        private readonly PlayerMotionState _thirdPersonState;
        private readonly PlayerFPMotionState _firstPersonState;
        private PlayerHoldPose _equipmentPose = PlayerHoldPose.Knife;

        public IState CurState { get; private set; }
        public bool IsStarted { get; private set; }
        public EPlayerModelType View { get; private set; } = EPlayerModelType.TP;
        public EStandRunState Locomotion { get; private set; } = EStandRunState.NONE;
        public PlayerHoldPose HoldPose { get; private set; } = PlayerHoldPose.Knife;
        public bool HasEquippedWeapon { get; private set; }
        public float MovementThreshold { get; }
        internal Vector3 LocalVelocity { get; private set; }
        internal PlayerHoldPose EquipmentPose => HasEquippedWeapon ? _equipmentPose : PlayerHoldPose.Knife;

        public event Action<EPlayerModelType> OnViewChanged;
        public event Action<EStandRunState> OnLocomotionChanged;
        public event Action<PlayerHoldPose> OnHoldPoseChanged;

        internal event Action EquipmentChanged;
        internal event Action<WeaponInputAction> WeaponInput;

        public PlayerMotionStateMachine(float movementThreshold = 0.1f)
        {
            if (movementThreshold < 0f || float.IsNaN(movementThreshold) || float.IsInfinity(movementThreshold))
                throw new ArgumentOutOfRangeException(nameof(movementThreshold));
            MovementThreshold = movementThreshold;
            _thirdPersonState = new PlayerMotionState(this);
            _firstPersonState = new PlayerFPMotionState(this);
        }

        /// <summary>切换动作视角；在停止后调用也会重新启动状态机。</summary>
        public void SetView(EPlayerModelType view)
        {
            if (view != EPlayerModelType.FP && view != EPlayerModelType.TP)
                throw new ArgumentOutOfRangeException(nameof(view));
            if (IsStarted && View == view)
                return;

            CurState?.OnExit();
            View = view;
            IsStarted = true;
            CurState = view == EPlayerModelType.TP ? _thirdPersonState : _firstPersonState;
            OnViewChanged?.Invoke(view);
            CurState.OnEnter();
        }

        /// <summary>保存装备信息；运行时同姿态重新装备也会通知持握状态重新进入。</summary>
        public void SetEquipment(bool hasEquippedWeapon, PlayerHoldPose pose)
        {
            if (pose != PlayerHoldPose.Knife && pose != PlayerHoldPose.Rifle)
                throw new ArgumentOutOfRangeException(nameof(pose));
            HasEquippedWeapon = hasEquippedWeapon;
            _equipmentPose = pose;
            HoldPose = EquipmentPose;
            if (IsStarted)
                EquipmentChanged?.Invoke();
        }

        public void HandleWeaponInput(WeaponInputAction action)
        {
            if (IsStarted)
                WeaponInput?.Invoke(action);
        }

        /// <summary>由外部提供角色本地速度，不读取场景、输入设备或表现组件。</summary>
        public void Tick(Vector3 localVelocity)
        {
            if (!IsStarted)
                return;
            LocalVelocity = localVelocity;
            CurState?.OnUpdate();
        }

        public void PhysicsTick()
        {
            if (IsStarted)
                CurState?.OnPhysicsUpdate();
        }

        /// <summary>退出整个层级并释放内部订阅，保留装备信息供再次启动使用。</summary>
        public void Stop()
        {
            if (!IsStarted)
                return;
            IsStarted = false;
            CurState?.OnExit();
            CurState = null;
            LocalVelocity = Vector3.zero;
        }

        internal void EnterHoldPose(PlayerHoldPose pose)
        {
            HoldPose = pose;
            OnHoldPoseChanged?.Invoke(pose);
        }

        internal void EnterLocomotion(EStandRunState locomotion)
        {
            Locomotion = locomotion;
            OnLocomotionChanged?.Invoke(locomotion);
        }
    }
}
