using System;
using System.Collections.Generic;
using System.Linq;
using Core.ZLog;
using Mirror;
using Network.Common;
using Network.Common.Messages;
using Network.Server.Session;
using UnityEngine;
//ServerManager.Publisher,负责发布消息
namespace Network.Server
{
    public sealed partial class ServerManager : MonoBehaviour
    {
        public bool PublisherEnabled{get; private set;}=true;
        public bool IsPublishing{get; private set;}=false;
        public uint Tick{get; private set;}=0;
        public float PublishDurationTime{get;}=1f/60f;

        void LateUpdate()
        {
            
        }

        public void InitPublishState()
        {
            Tick=0;
            StopPublish();
        }
        public void StartPublish()
        {
            IsPublishing=true;
        }
        public void StopPublish()
        {
            IsPublishing=false;
        }
        /// <summary>
        /// 广播所有玩家位置
        /// </summary>
        void Publish()
        {
            if(!PublisherEnabled)return;
            PlayerSnapshotMessage snapshot = new()
            {
                tick=++Tick,
                durationTime=PublishDurationTime,
                playerPoses=new PlayerPose[ClientCount],
            };
            int posPtr=0;
            foreach(var session in Sessions.Sessions)
            {
                var trans=session.PlayerIdentity.transform;
                var pose =new PlayerPose
                {
                    netId=session.ConnectionId,
                    position=trans.position,
                    facingDirection=Vector3.zero //之后实现视角同步,先统一为(0,0,0)
                };
                snapshot.playerPoses[posPtr++]=pose;
            }

        }

    }
}
