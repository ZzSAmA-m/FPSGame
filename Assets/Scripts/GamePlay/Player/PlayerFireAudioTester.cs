using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using GamePlay.HoldSystem;
using Sirenix.OdinInspector;
using Unity.VisualScripting;
using UnityEngine;
namespace GamePlay.Player
{
    public class PlayerFireAudioTester : MonoBehaviour
    {
        [SerializeField]private List<AudioClip> _fireAudios;
        [SerializeField]private AudioClip _prepareAudio;
        private AudioSource _source;
        private PlayerWeaponController _controller;
        int _lastRand=-1;
        void Awake()
        {
            _controller=GetComponent<PlayerWeaponController>();
            _source=GetComponent<AudioSource>();
        }
        void OnEnable()
        {
            _controller.OnShot+=OnShot;
            _controller.OnPrepare+=OnPrepare;
        }
        void OnDisable()
        {
            _controller.OnShot-=OnShot;
            _controller.OnPrepare-=OnPrepare;
        }

        private void OnPrepare(Gun gun)
        {
            _source.clip=_prepareAudio;
            _source.Play();
        }

        [Button]
        private void OnShot(Gun gun)
        {
            if (_source == null || _fireAudios == null || _fireAudios.Count == 0)
                return;
            _source.clip=_fireAudios[GetRand()];
            _source.Play();
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        int GetRand()
        {
            int audioCount = _fireAudios.Count;
            int index = UnityEngine.Random.Range(0, audioCount);
            if (audioCount > 1 && index == _lastRand)
            {
                int offset = UnityEngine.Random.Range(1, audioCount);
                index = (index + offset) % audioCount;
            }
            _lastRand = index;
            return index;
        }
    }

}
