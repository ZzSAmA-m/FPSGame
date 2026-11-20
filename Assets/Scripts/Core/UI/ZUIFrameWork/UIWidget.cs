using UnityEngine;

namespace ZUI
{
    public abstract class UIWidget:UIBase
    {
        public virtual UIBase Parent { get; private set; }
    }
}
