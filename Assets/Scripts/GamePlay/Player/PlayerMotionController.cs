using System;
using Core.Utility;
using GamePlay.HoldSystem;
using GamePlay.Player.MotionFSM;
using GamePlay.Player.Simulation;
using UnityEngine;

namespace GamePlay.Player
{
    /// <summary>将角色速度、装备和输入交给独立动作状态机，不负责动画或模型表现。</summary>
    [DisallowMultipleComponent]
    public sealed class PlayerMotionController : MonoBehaviour
    {
        [SerializeField, Min(0f), Tooltip("动作状态由静止切换为移动的速度阈值。")]
        private float _movementThreshold = 0.1f;

        private PlayerMotionStateMachine _stateMachine;
        private PlayerBrain _brain;
        private PlayerHolder _holder;

        /// <summary>实时运动模拟确认成功起跳；运动回放不会重复通知。</summary>
        public event Action OnJumped;

        // 表现组件可以在自己的 OnEnable 中订阅，不依赖不同组件的 Awake 顺序。
        public PlayerMotionStateMachine StateMachine =>
            _stateMachine ??= new PlayerMotionStateMachine(_movementThreshold);

        private void OnEnable()
        {
            _brain = GetComponent<PlayerBrain>();
            _holder = GetComponent<PlayerHolder>();
            if (_holder != null)
                _holder.OnWeaponChanged += RefreshEquipment;

            RefreshEquipment();
            StateMachine.SetView(StateMachine.View);
        }

        private void OnDisable()
        {
            if (_holder != null)
                _holder.OnWeaponChanged -= RefreshEquipment;
            _stateMachine?.Stop();
        }

        private void Update()
        {
            Vector3 velocity = _brain != null ? _brain.DisplayVelocity : Vector3.zero;
            StateMachine.Tick(transform.InverseTransformDirection(velocity));
        }

        private void FixedUpdate() => StateMachine.PhysicsTick();

        public void SetView(EPlayerModelType view) => StateMachine.SetView(view);

        /// <summary>消费玩家门面提交的实时运动结果，切栏先于这一拍的枪械驱动。</summary>
        public void Present(in PlayerStepResult result)
        {
            if (!isActiveAndEnabled)
                return;

            if (result.key1)
                HandleWeaponInput(WeaponInputAction.Key1);
            if (result.key2)
                HandleWeaponInput(WeaponInputAction.Key2);
            if (result.key3)
                HandleWeaponInput(WeaponInputAction.Key3);
            if (result.jumped)
                OnJumped?.Invoke();
        }

        private void RefreshEquipment()
        {
            WeaponBase weapon = _holder != null ? _holder.CurrentWeapon : null;
            StateMachine.SetEquipment(weapon != null,
                weapon is Gun ? PlayerHoldPose.Rifle : PlayerHoldPose.Knife);
        }

        private void HandleWeaponInput(WeaponInputAction action)
        {
            if (_holder != null)
            {
                switch (action)
                {
                    case WeaponInputAction.Key1: _holder.SelectSlot(WeaponSlot.Rifle); return;
                    case WeaponInputAction.Key2: _holder.SelectSlot(WeaponSlot.Pistol); return;
                    case WeaponInputAction.Key3: _holder.SelectSlot(WeaponSlot.Knife); return;
                }
            }
            StateMachine.HandleWeaponInput(action);
        }

        private void OnValidate()
        {
            if (float.IsNaN(_movementThreshold) || float.IsInfinity(_movementThreshold) || _movementThreshold < 0f)
                _movementThreshold = 0.1f;
        }
    }
}
