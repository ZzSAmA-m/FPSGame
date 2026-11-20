using UnityEngine;

namespace ZUI
{
    public sealed class UIManager:MonoBehaviour
    {
        private static UIManager _instance;
        public static UIManager Instance
        {
            get
            {
                if (_instance == null)
                    _instance = FindAnyObjectByType<UIManager>();

                return _instance;
            }
        }

        public Transform UIRootNode{get;private set;}
        void Awake()
        {
            UIRootNode=GameObject.Find("UIRoot").transform;
        }
    }
}
