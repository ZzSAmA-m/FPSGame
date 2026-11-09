using System;
using System.Collections.Generic;
using UnityEngine;

namespace GamePlay.HoldSystem
{
    /// <summary>一把枪共享的模型、动作名与音频引用；播放进度属于角色或模型控制器。</summary>
    [CreateAssetMenu(fileName = "GunResourcePack", menuName = "Scriptable Objects/Weapon/Gun/Resource Pack")]
    public sealed class GunResourcePackSO : ScriptableObject, IWeaponPresentationResources
    {
        [SerializeField, InspectorName("模型预制体"), Tooltip("根节点挂有通用枪械模型控制器的完整预制体。")]
        private WeaponController _modelPrefab;

        [Header("动画状态名称（支持完整层路径，不是 Clip 文件名）")]
        [SerializeField, InspectorName("第一人称角色")] private GunAnimationNames _firstPersonAnimations = new();
        [SerializeField, InspectorName("第三人称角色")] private GunAnimationNames _thirdPersonAnimations = new();
        [SerializeField, InspectorName("枪械模型")] private GunAnimationNames _modelAnimations = new();

        [Header("音频资源（仅引用，不负责加载或播放）")]
        [SerializeField, InspectorName("准备音效")] private List<AudioClip> _prepareAudio = new();
        [SerializeField, InspectorName("开火音效变体")] private List<AudioClip> _fireAudio = new();
        [SerializeField, InspectorName("换弹音效资源")] private List<AudioClip> _reloadAudio = new();

        private IReadOnlyList<AudioClip> _prepareAudioView;
        private IReadOnlyList<AudioClip> _fireAudioView;
        private IReadOnlyList<AudioClip> _reloadAudioView;

        public WeaponController ModelPrefab => _modelPrefab;

        public bool TryGetAnimation(WeaponPresentationAction action, WeaponAnimationTarget target, out string stateName)
        {
            GunAnimationNames names = target switch
            {
                WeaponAnimationTarget.FirstPerson => _firstPersonAnimations,
                WeaponAnimationTarget.ThirdPerson => _thirdPersonAnimations,
                WeaponAnimationTarget.Model => _modelAnimations,
                _ => null
            };
            stateName = names?.GetStateName(action);
            return !string.IsNullOrWhiteSpace(stateName);
        }

        public IReadOnlyList<AudioClip> GetAudioClips(WeaponPresentationAction action) => action switch
        {
            WeaponPresentationAction.Prepare => _prepareAudioView ??= (_prepareAudio ??= new()).AsReadOnly(),
            WeaponPresentationAction.Fire => _fireAudioView ??= (_fireAudio ??= new()).AsReadOnly(),
            WeaponPresentationAction.Reload => _reloadAudioView ??= (_reloadAudio ??= new()).AsReadOnly(),
            _ => Array.Empty<AudioClip>()
        };

        private void OnValidate()
        {
            _prepareAudioView = null;
            _fireAudioView = null;
            _reloadAudioView = null;
        }
    }

    [Serializable]
    public sealed class GunAnimationNames
    {
        [SerializeField, InspectorName("装备准备")] private string _prepare;
        [SerializeField, InspectorName("持握（末帧姿态）")] private string _hold;
        [SerializeField, InspectorName("开火")] private string _fire;
        [SerializeField, InspectorName("换弹")] private string _reload;

        public string GetStateName(WeaponPresentationAction action) => action switch
        {
            WeaponPresentationAction.Prepare => _prepare,
            WeaponPresentationAction.Hold => _hold,
            WeaponPresentationAction.Fire => _fire,
            WeaponPresentationAction.Reload => _reload,
            _ => null
        };
    }
}
