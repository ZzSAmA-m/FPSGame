using UnityEngine;
using System.Threading;
using Core.Input;
using Core.Timer;
using Core.ZLog;
using Mirror;
using Network.Client;
using Network.Common;
using Network.Common.Messages;
using GamePlay.Player.Network;
using System;
using Microsoft.CodeAnalysis.CSharp.Syntax;
namespace GamePlay.Player
{
    public sealed class PlayerInputHandler : MonoBehaviour
    {
        /// <summary>
        /// 每次采样间隔时间
        /// </summary>
        [SerializeField,Sirenix.OdinInspector.ReadOnly]private float _sampleDeltaTime=1f/60f;

        private PlayerInputCollector _collector;
        private PlayerInputDriver _driver;
        private float _handleTimer;
        private bool _handling;
        private uint _sequence;
        public bool isLocalPlayer{get;set;}=false;

        public event RefAction<PlayerCommandMessage> CommandProduced;
        void Awake()
        {
            _collector=GetComponent<PlayerInputCollector>();
            _driver=PlayerInputDriver.Instance;
        }
        void Start()
        {
            StartHandleInput();
        }
        void Update()
        {
            
            if(!isLocalPlayer)return;
            
            TickHandleInput();
        }
        /// <summary>
        /// 按采样间隔取出输入并交给 Publish。帧率不足时在本帧补发，避免长期低于 60 次/秒。
        /// </summary>
        void TickHandleInput()
        {
            if(!_handling || _sampleDeltaTime<=0f)
                return;

            _handleTimer+=Time.unscaledDeltaTime;
            int catchUpCount=0;
            while(_handleTimer>=_sampleDeltaTime && catchUpCount<4)
            {
                _handleTimer-=_sampleDeltaTime;
                PlayerInput input=_collector.GetSample();

                HandleCollectedInput(ref input);
                catchUpCount++;
            }
        }
        public void StartHandleInput()
        {
            _handling=true;
            _handleTimer=0f;
        }
        public void StopHandleInput()
        {
            _handling=false;
            _handleTimer=0f;
        }
        void HandleCollectedInput(ref PlayerInput input)
        {
            _driver.DriveInputData(ref input);
            Publish(ref input);
        }
        void Publish(ref PlayerInput input)
        {
            var command=new PlayerCommandMessage
            {
                sequence = ++_sequence,
                move = input.move,
                look = input.look,
                jump = input.space,
                fire = input.lMouse,
                key1 = input.key1,
                key2 = input.key2,
                key3 = input.key3
            };
            CommandProduced?.Invoke(ref command);
        }
    }
}
