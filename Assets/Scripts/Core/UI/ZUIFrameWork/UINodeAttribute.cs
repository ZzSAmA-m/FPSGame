using System;
using UnityEngine;

namespace ZUI
{
    public class UINodeAttribute:Attribute
    {
        public UIType Type{get;}
        public string Path{get;}
        public UINodeAttribute(UIType type,string path)
        {
            Type=type;
            Path=path;
        }
    }
}
