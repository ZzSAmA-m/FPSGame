using System.Collections.Generic;
using UnityEngine;

namespace ZUI
{
    [DisallowMultipleComponent]
    public abstract class UINode : MonoBehaviour
    {
        private readonly List<UINode> _children = new();
        private bool _initialized;
        private bool _wasVisible;

        public UINode Parent { get; private set; }
        public IReadOnlyList<UINode> Children => _children;
        public bool VisibleSelf => gameObject.activeSelf;
        public bool IsVisibleInHierarchy => isActiveAndEnabled;
        public Canvas Canvas => GetComponentInParent<Canvas>(true);

        protected virtual void OnCreate() { }
        protected virtual void OnSetVisible(bool visible) { }
        protected virtual void OnRefresh() { }

        public void SetVisible(bool visible)
        {
            if (VisibleSelf != visible)
                gameObject.SetActive(visible);
        }

        /// <summary>刷新当前节点及其子树中已启用的节点。</summary>
        public void Refresh()
        {
            if (!_initialized || !gameObject.activeInHierarchy)
                return;

            if (enabled)
                OnRefresh();

            foreach (var child in _children)
                child.Refresh();
        }

        internal void InitializeTree()
        {
            var nodes = GetComponentsInChildren<UINode>(true);
            foreach (var node in nodes)
            {
                if (node == this)
                    continue;

                node.Parent = node.transform.parent.GetComponentInParent<UINode>(true);
                node.Parent._children.Add(node);
            }

            foreach (var node in nodes)
                node.OnCreate();

            // 整棵树创建完成后才派发显隐回调。
            foreach (var node in nodes)
                node._initialized = true;

            foreach (var node in nodes)
                node.NotifyVisibility(node.IsVisibleInHierarchy);
        }

        private void OnEnable() => NotifyVisibility(true);
        private void OnDisable() => NotifyVisibility(false);

        private void NotifyVisibility(bool visible)
        {
            if (!_initialized || _wasVisible == visible)
                return;

            _wasVisible = visible;
            OnSetVisible(visible);
            if (visible && _wasVisible)
                OnRefresh();
        }
    }
}
