using System;
using Core.Utility;
using GamePlay.FSM;
using GamePlay.Player.MotionFSM;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.InputSystem;
namespace GamePlay.Player
{
    public class PlayerAnimatorController : MonoBehaviour
    {
        [SerializeField]Animator _mainAnimator;
        public PlayerWeaponController WeaponController{get;private set;}
        public float offset=0.1f;
        public float switchAnimTime=0.2f;

        public GameObject RifleModel;
        public GameObject KnifeModel;

        private PlayerMotionState _playerMotionState;
        private PlayerFPMotionState _playerFPMotionState;

        public IState CurState{get;private set;}

        //TODO: delete this component
        public CharacterController Cc{get;private set;}

        public readonly int DownLayer = 0;
        public readonly int UpLayer   = 1;
        public readonly int FPLayer   = 2;


        void Awake()
        {
            if (_mainAnimator == null)
            {
                _mainAnimator=GetComponentInChildren<Animator>();
            }
            if (_mainAnimator == null)
            {
                Debug.LogError($"[{nameof(PlayerAnimatorController)}] {nameof(_mainAnimator)}为null!")   ;
            }
            Cc=GetComponent<CharacterController>();
            WeaponController=GetComponent<PlayerWeaponController>();
            _playerMotionState=new(this);
            _playerFPMotionState=new(this);
            SwitchState<PlayerFPMotionState>();
        }



        void Update()
        {
            CurState.OnUpdate();
        }



        void FixedUpdate()
        {
            CurState.OnPhysicsUpdate();
        }



        public void PlayAnim(int animName,int layer=0)
        {
            _mainAnimator.Play(animName,layer);
        }


        public void CrossFadeAnim(int animName,float time,int layer)
        {
            _mainAnimator.CrossFade(animName,time,layer);
        }


        public void SetLayerWeight(int layer,int weight)
        {
            _mainAnimator.SetLayerWeight(layer,weight);
        }
        
        public void SwitchState<T>()
        {
            SwitchState(typeof(T));
        }

        
        public void SwitchState(Type T)
        {
            CurState?.OnExit();

            if (T == typeof(PlayerMotionState))  CurState=_playerMotionState;
            else CurState=_playerFPMotionState;
            
            CurState.OnEnter();
        }
        [Button]
        void DebugSwitchState()
        {
            if(CurState==_playerMotionState)SwitchState<PlayerFPMotionState>();
            else SwitchState<PlayerMotionState>();
        }
    }

}
