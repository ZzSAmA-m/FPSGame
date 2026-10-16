using UnityEngine;
//重力类
namespace GamePlay.Physics
{
    public static class GForce
    {
        static GForce()
        {
            globalGForce=-9.8f;
        }
        public static float globalGForce{get;private set;}

    }
}