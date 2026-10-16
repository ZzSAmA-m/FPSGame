using Mirror;
using UnityEngine;
namespace Core.Utility
{
    public class Singleton<T> : MonoBehaviour where T : MonoBehaviour
    {
        private static T _instance;

        public static T Instance
        {
            get
            {
                if (_instance == null)
                    _instance = FindAnyObjectByType<T>();

                return _instance;
            }
        }
    }
    public class NetSingleton<T> : NetworkBehaviour where T : NetworkBehaviour
    {
        private static T _instance;

        public static T Instance
        {
            get
            {
                if (_instance == null)
                    _instance = FindAnyObjectByType<T>();

                return _instance;
            }
        }
    }

}
