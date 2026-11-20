using System;
using System.Collections.Generic;
using System.Reflection;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using UnityEditor;
using UnityEngine;

namespace ZUI
{
    public class UIResourceLoader : IUIResourceLoader
    {
        public GameObject GetUIInstance<T>() where T : UIWindow
        {
            var attr=typeof(T).GetCustomAttribute<UIWindowAttribute>();
            return AssetDatabase.LoadAssetAtPath<GameObject>(@$"Assets\UI\Prefabs\{attr.Path}.prefab");
        }
    }
}
