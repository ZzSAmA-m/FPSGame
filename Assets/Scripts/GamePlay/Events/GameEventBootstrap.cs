using Core.Event;
using UnityEngine;

namespace GamePlay.Events
{
    /// <summary>在游戏启动时清空上一次运行的事件和监听。</summary>
    public static class GameEventBootstrap
    {
        /// <summary>在场景对象启用前初始化事件中心。</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Initialize()
        {
            EventCenter.Reset();
        }
    }
}
