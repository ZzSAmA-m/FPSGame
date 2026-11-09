using System.Collections.Generic;
using UnityEngine;

namespace GamePlay.HoldSystem
{
    public enum WeaponPresentationAction
    {
        Prepare,
        Hold,
        Fire,
        Reload
    }

    public enum WeaponAnimationTarget
    {
        FirstPerson,
        ThirdPerson,
        Model
    }

    /// <summary>按动作获取武器表现资源；不负责播放，也不保存角色的运行时状态。</summary>
    public interface IWeaponPresentationResources
    {
        WeaponController ModelPrefab { get; }
        bool TryGetAnimation(WeaponPresentationAction action, WeaponAnimationTarget target, out string stateName);
        IReadOnlyList<AudioClip> GetAudioClips(WeaponPresentationAction action);
    }
}
