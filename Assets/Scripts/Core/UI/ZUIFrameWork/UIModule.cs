using System;
using System.Collections.Generic;
using Core.ZLog;
using Object = UnityEngine.Object;

namespace ZUI
{
    public sealed partial class UIModule
    {
        private readonly IUIResourceLoader _loader = new UIResourceLoader();
        private readonly Dictionary<Type, UINode> _roots = new();

        public T Show<T>() where T : UINode
        {
            if (TryGet<T>(out var root))
                root.SetVisible(true);
            else
                CreateRoot<T>();

            // 创建或显示回调可能已关闭、重建这个根节点。
            TryGet<T>(out root);
            return root;
        }

        public void Hide<T>() where T : UINode
        {
            if (TryGet<T>(out var root))
                root.SetVisible(false);
        }

        public void Kill<T>() where T : UINode
        {
            if (!TryGet<T>(out var root))
                return;

            _roots.Remove(typeof(T));
            root.SetVisible(false);
            Object.Destroy(root.gameObject);
        }

        public bool TryGet<T>(out T window) where T : UINode
        {
            if (_roots.TryGetValue(typeof(T), out var instance) && instance != null)
            {
                window = (T)instance;
                return true;
            }

            window = null;
            return false;
        }

        public bool IsVisible<T>() where T : UINode
        {
            return TryGet<T>(out var root) && root.VisibleSelf;
        }

        public void Refresh<T>() where T : UINode
        {
            if (TryGet<T>(out var root))
                root.Refresh();
        }

        private void CreateRoot<T>() where T : UINode
        {
            var manager = UIManager.Instance;
            if (manager == null || manager.UIRootNode == null)
            {
                ZLog.LogError<UIModule>("无法创建窗口，请检查场景中的 UIManager 和 UIRoot。");
                return;
            }

            var prefab = _loader.GetUIInstance<T>();
            if (prefab == null || !prefab.TryGetComponent<T>(out var template))
            {
                ZLog.LogError<UIModule>($"找不到 {typeof(T).Name} 的预制体，或预制体根节点缺少窗口组件。");
                return;
            }

            var root = Object.Instantiate(template, manager.UIRootNode);
            _roots[typeof(T)] = root;
            try
            {
                root.SetVisible(true);
                root.InitializeTree();
            }
            catch
            {
                if (_roots.TryGetValue(typeof(T), out var current) && current == root)
                    _roots.Remove(typeof(T));

                root.SetVisible(false);
                Object.Destroy(root.gameObject);
                throw;
            }
        }
    }
}
