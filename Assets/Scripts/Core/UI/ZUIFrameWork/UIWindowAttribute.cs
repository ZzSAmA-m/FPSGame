using System;
using UnityEngine;

namespace ZUI
{
    public class UIWindowAttribute:Attribute
    {
        public UIType Type{get;}
        public string Path{get;}
        public UIWindowAttribute(UIType type,string path)
        {
            Type=type;
            Path=path;
        }
    }
}
