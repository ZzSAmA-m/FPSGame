using System.Collections.Generic;
using Core.Utility;
using GamePlay.Player.MotionFSM;
using Sirenix.OdinInspector;
using UnityEngine;

namespace GamePlay.Player
{
    /// <summary>订阅动作语义事件，负责视角模型、骨骼父节点和旧调试武器的显示。</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(PlayerMotionController))]
    public sealed class PlayerModelController : MonoBehaviour
    {
        [SerializeField] private Transform TPModelParent;
        [SerializeField] private Transform FPModelParent;
        [SerializeField] private Transform SourceModel;
        [SerializeField, ReadOnly] private EPlayerModelType _e_PlayerModelType = EPlayerModelType.TP;
        [SerializeField] private List<GameObject> FpModels = new();
        [SerializeField] private List<GameObject> TpModels = new();
        [SerializeField] private GameObject _rifleModel;
        [SerializeField] private GameObject _knifeModel;

        public Transform FirstPersonRoot => FPModelParent;
        public Transform ThirdPersonRoot => TPModelParent;
        public GameObject RifleModel => _rifleModel;
        public GameObject KnifeModel => _knifeModel;

        private PlayerMotionStateMachine _motion;

        private void OnEnable()
        {
            _motion = GetComponent<PlayerMotionController>().StateMachine;
            _motion.OnViewChanged += ApplyView;
            _motion.OnHoldPoseChanged += ApplyHoldPose;
            if (_motion.IsStarted)
            {
                ApplyView(_motion.View);
                ApplyHoldPose(_motion.HoldPose);
            }
        }

        private void OnDisable()
        {
            if (_motion == null)
                return;
            _motion.OnViewChanged -= ApplyView;
            _motion.OnHoldPoseChanged -= ApplyHoldPose;
            _motion = null;
        }

        private void ApplyView(EPlayerModelType view)
        {
            bool firstPerson = view == EPlayerModelType.FP;
            SetVisible(TpModels, !firstPerson);
            SetVisible(FpModels, firstPerson);
            _e_PlayerModelType = view;
            Transform parent = firstPerson ? FPModelParent : TPModelParent;
            if (SourceModel == null || parent == null)
                return;
            SourceModel.SetParent(parent, false);
            SourceModel.localPosition = Vector3.zero;
            SourceModel.localRotation = Quaternion.identity;
        }

        private void ApplyHoldPose(PlayerHoldPose pose)
        {
            // 实际装备模型的生命周期归 PlayerHolder 管理，这里只切换旧的调试模型。
            bool showLegacy = !_motion.HasEquippedWeapon;
            if (_rifleModel != null)
                _rifleModel.SetActive(showLegacy && pose == PlayerHoldPose.Rifle);
            if (_knifeModel != null)
                _knifeModel.SetActive(showLegacy && pose == PlayerHoldPose.Knife);
        }

        private static void SetVisible(List<GameObject> models, bool visible)
        {
            foreach (GameObject model in models)
                if (model != null) model.SetActive(visible);
        }
    }
}
