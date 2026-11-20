using UnityEngine;

namespace ZUI
{
    public abstract class UIWindow : UIBase
    {
        public virtual Canvas Canvas { get; private set; }
        protected virtual void OnSetVisible(bool visible) { }
    }
}
