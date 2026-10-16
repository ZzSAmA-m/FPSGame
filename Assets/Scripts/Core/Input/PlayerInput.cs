using UnityEngine;
namespace Core.Input
{
    public struct PlayerInput
    {

        // 移动输入
        public Vector2 move;

        // 视角输入
        public Vector2 look;

        // 功能键
        public bool space;
        public bool lMouse;
        public bool r;
        //数字键
        public bool key1;
        public bool key2;
        public bool key3;
    }
}