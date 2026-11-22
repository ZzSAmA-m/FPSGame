using System.Reflection;
using UnityEngine;

namespace ZUI
{
    public class UIResourceLoader : IUIResourceLoader
    {
        public GameObject GetUIInstance<T>() where T : UIWindow
        {
            var attribute = typeof(T).GetCustomAttribute<UIWindowAttribute>();
            return Resources.Load<GameObject>($"UI/Prefabs/{attribute.Path}");
        }
    }
}
