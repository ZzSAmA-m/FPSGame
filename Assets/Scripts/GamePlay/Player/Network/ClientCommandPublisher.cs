using Core.Input;
using Core.ZLog;
using Mirror;
using Network.Common;
using Network.Common.Messages;
using UnityEngine;
namespace GamePlay.Player.Network
{
    public class ClientCommandPublisher : NetworkBehaviour
    {
        
        /// <summary>
        /// 每次采样间隔时间
        /// </summary>
        [SerializeField,Sirenix.OdinInspector.ReadOnly]private float _sampleDeltaTime=1f/60f;

        private PlayerNetworkInputCollector _collector;
        private float _publishTimer;
        private bool _publishing;
        private uint _sequence;
        void Awake()
        {
            _collector=GetComponent<PlayerNetworkInputCollector>();
        }
        void Update()
        {
            if(!isLocalPlayer)return;
            TickPublish();
        }
        /// <summary>
        /// 按采样间隔取出输入并交给 Publish。帧率不足时在本帧补发，避免长期低于 60 次/秒。
        /// </summary>
        void TickPublish()
        {
            if(!_publishing || _sampleDeltaTime<=0f)
                return;

            _publishTimer+=Time.unscaledDeltaTime;
            int catchUpCount=0;
            while(_publishTimer>=_sampleDeltaTime && catchUpCount<4)
            {
                _publishTimer-=_sampleDeltaTime;
                PlayerInput input=_collector.GetSample();
                Publish(ref input);
                catchUpCount++;
            }
        }
        public void StartPublish()
        {
            _publishing=true;
            _publishTimer=0f;
        }
        public void StopPublish()
        {
            _publishing=false;
            _publishTimer=0f;
        }
        void Publish(ref PlayerInput input)
        {
            if (NetworkManager.singleton is not FPSNetworkManager manager || manager.Bridge == null)
            {
                return;
            }
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
            manager.Bridge.SendPlayerCommand(command);
            ZLog.Log<ClientCommandPublisher>("Send Command: "+command);
        }
    }

}
