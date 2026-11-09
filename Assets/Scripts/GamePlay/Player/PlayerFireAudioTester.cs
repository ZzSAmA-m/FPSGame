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
            _source.clip=_fireAudios[GetRand()];
            _source.Play();
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        int GetRand()
        {

            int ret=UnityEngine.Random.Range(0,_fireAudios.Count-1);
            while (_lastRand == ret)
            {
                ret=UnityEngine.Random.Range(0,_fireAudios.Count-1);
            }
            _lastRand=ret;
            return ret;
            
        }
    }

}
