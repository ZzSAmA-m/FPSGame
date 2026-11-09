using Core.Event;
using UnityEngine;

namespace GamePlay.Events
{
    /// <summary>在游戏启动时重置并注册玩法事件。</summary>
    public static class GameEventBootstrap
    {
        /// <summary>在场景对象启用前初始化事件中心。</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Initialize()
        {
            if (EventCenter.Reset())
                RegisterAll();
        }

        /// <summary>注册全部玩法事件并返回是否成功。</summary>
        public static bool RegisterAll()
        {
            bool attachRegistered = EventCenter.Register<Transform>(
                CameraEventIds.Attach,
                typeof(CameraEventIds).FullName + "." + nameof(CameraEventIds.Attach));
            bool detachRegistered = EventCenter.Register(
                CameraEventIds.Detach,
                typeof(CameraEventIds).FullName + "." + nameof(CameraEventIds.Detach));
            return attachRegistered && detachRegistered;
        }
    }
}
