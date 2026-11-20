using UnityEngine;
using System.Collections.Generic;
namespace ZUI
{
    public abstract class UIBase:MonoBehaviour
    {
        private readonly List<UIWidget> _children = new();
        protected virtual void OnCreate() { }
        protected virtual void OnRefresh() { }
        protected virtual void OnDestroy() { }
        internal virtual void RefreshView() => OnRefresh();
    }
}
