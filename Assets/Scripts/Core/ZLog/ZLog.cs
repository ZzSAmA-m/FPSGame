
using UnityEngine;
namespace Core.ZLog
{
    public static class ZLog
    {
        public static bool Enable{get;set;}=true;
        public static void Log(string message)
        {
            if(!Enable)return;
            Debug.Log(message);
        }
        public static void LogError(string message)
        {
            if(!Enable)return;
            Debug.LogError(message);
        }
        public static void LogWarning(string message)
        {
            if(!Enable)return;
            Debug.LogWarning(message);
        }


        public static void Log<T>(string message)
        {
            if(!Enable)return;
            Debug.Log($"[{typeof(T).Name}] {message}");

        }
        public static void LogError<T>(string message)
        {
            if(!Enable)return;
            Debug.LogError($"[{typeof(T).Name}] {message}");
        }
        public static void LogWarning<T>(string message)
        {
            if(!Enable)return;
            Debug.LogWarning($"[{typeof(T).Name}] {message}");
        }
    }
}