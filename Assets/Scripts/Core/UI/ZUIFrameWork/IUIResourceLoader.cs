using System;
using UnityEngine;

namespace ZUI
{
    public interface IUIResourceLoader
    {
        GameObject GetUIInstance<T>() where T:UIWindow;

    }
}
