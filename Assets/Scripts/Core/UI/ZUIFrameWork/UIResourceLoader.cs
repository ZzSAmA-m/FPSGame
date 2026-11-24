using System.Reflection;
using UnityEngine;

namespace ZUI
{
    public class UIResourceLoader : IUIResourceLoader
    {
        public GameObject GetUIInstance<T>() where T : UINode
        {
            var attribute = typeof(T).GetCustomAttribute<UINodeAttribute>();
            if (attribute == null)
                return null;

            return Resources.Load<GameObject>($"UI/Prefabs/{attribute.Path}");
        }
    }
}
