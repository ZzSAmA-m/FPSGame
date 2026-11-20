using System;
using System.Collections.Generic;
using UnityEngine;

namespace ZUI
{
    public sealed partial class UIModule
    {
        private IUIResourceLoader _loader=new UIResourceLoader();
        private UIManager Mgr=UIManager.Instance;
        

        public void Show<T>() where T:UIWindow
        {

            var go=_loader.GetUIInstance<T>();
            GameObject.Instantiate(go,Mgr.UIRootNode);
        }
    }
}
