using System;
using UnityEngine;

namespace Core.Event
{
    public static class EventCenter{
        private static readonly string _name="EventCenter";             //事件中心名字
        public static bool showDetail{get;set;}                         //触发事件时候是否显示方法数量 
        public static bool showEventInvokeMsg{get;set;}                 //触发事件时候是否显示事件名称

#region GamePlayEvents
        public static class GamePlayEvent
        {
            public static readonly GameEvent PlayerSpawnEvent=new(nameof(PlayerSpawnEvent));
            public static readonly GameEvent<GameObject> LocalPlayerSpawnEvent=new(nameof(LocalPlayerSpawnEvent));
            public static readonly GameEvent<Transform> CameraAttachEvent=new(nameof(CameraAttachEvent));

            /// <summary>
            /// 本地玩家即将销毁时，让场景相机先脱离玩家层级。
            /// 否则 Unity 销毁玩家根节点时会把作为子物体的相机一起销毁。
            /// </summary>
            public static readonly GameEvent CameraDetachEvent=new(nameof(CameraDetachEvent));

            
        }
#endregion

#region InputEvents
        public static class InputEvent
        {
            /// <summary>
            /// 判断是本地玩家后，广播，让InputDriver绑定本地Player来控制它
            /// </summary>
            public static readonly GameEvent<GameObject> BindPlayer2InputDriverEvent=new(nameof(BindPlayer2InputDriverEvent));

            /// <summary>
            /// 本地玩家退出时解除 InputDriver 引用，避免继续驱动已经销毁的组件。
            /// </summary>
            public static readonly GameEvent<GameObject> UnbindPlayerFromInputDriverEvent=
                new(nameof(UnbindPlayerFromInputDriverEvent));
        }

#endregion

#region NetEvents
        public static class NetEvents
        {
            public static readonly GameEvent<GameObject> AddLocalPlayer2ClientManager=new($"Net.{nameof(AddLocalPlayer2ClientManager)}");
        }

#endregion

        public class GameEvent
        {
            public GameEvent(string name)
            {
                _name=name;
                OnEventRaised+=()=>{
                    if (showEventInvokeMsg)
                    {
                        Debug.Log($"[{EventCenter._name}] Event  \"{_name}\" invoked");
                    }
                    if (showDetail)
                    {
                        Debug.Log($"Details:Total methods:{OnEventRaised.GetInvocationList().Length-1}");
                    }
                };
            }
            private string _name;
            public event Action OnEventRaised;
            public void RaiseEvent()
            {
                OnEventRaised.Invoke();
            }
            
        }
        public class GameEvent<T>
        {
            public GameEvent(string name)
            {
                _name=name;
                OnEventRaised+=(a)=>{
                    if (showEventInvokeMsg)
                    {
                        Debug.Log($"[{EventCenter._name}] Event  \"{_name}\" invoked");
                    }
                    if (showDetail)
                    {
                        Debug.Log($"Details:Total methods:{OnEventRaised.GetInvocationList().Length-1}");
                    }
                };
            }
            private string _name;
            public event Action<T> OnEventRaised;
            public void RaiseEvent(T arg0)
            {
                OnEventRaised.Invoke(arg0);
            }
        }
    }
}