using System;
using System.Collections.Generic;
using System.Linq;
using Core.Timer;
using Core.ZLog;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Mirror;
using Network.Common;
using Network.Common.Messages;
using UnityEngine;
using MyTimer=Core.Timer.Timer;
//ServerManager.Publisher,负责发布消息
namespace Network.Server
{
    public sealed partial class ServerManager : MonoBehaviour
    {
        public bool PublisherEnabled{get; private set;}=true;
        public bool IsPublishing{get; private set;}=false;
        public uint Tick{get; private set;}=0;
        public float PublishDurationTime{get;}=1f/30f;
        private MyTimer _timer;
        void Awake()
        {
            _timer=TimerRegistry.Register("ServerManager.Publisher");
            _timer.Time=PublishDurationTime;
            _timer.EventMethod=Publish;
        }
        void Start()
        {
            StartPublish();
        }

        public void InitPublishState()
        {
            Tick=0;
            StopPublish();
        }
        public void StartPublish()
        {
            IsPublishing=true;
            _timer.StartInvoke();
        }
        public void StopPublish()
        {
            IsPublishing=false;
            _timer.StopInvoke();
        }
        /// <summary>
        /// 广播所有玩家位置
        /// </summary>
        void Publish()
        {
            
            PlayerSnapshotMessage snapshot = new()
            {
                tick=++Tick,
                durationTime=PublishDurationTime,
                playerPoses=Sessions.Count==0?null:new PlayerPose[Sessions.Count],
            };
            int posPtr=0;
            foreach(var session in Sessions.Clients)
            {
                 NetworkIdentity identity = session.Value.Conn.identity;
                if (identity == null)
                    continue;
                var trans=identity.transform;
                var pose =new PlayerPose
                {
                    netId=session.Key,
                    position=trans.position,
                    facingDirection=Vector3.zero //之后实现视角同步,先统一为(0,0,0)
                };
                snapshot.playerPoses[posPtr++]=pose;
            }
            _bridge.SendPlayerSnapshot(ref snapshot);
        }

    }
}
