using System;
using UnityEngine;

public static class Serializer
{
    public static string PositionToString(uint id,Transform transform)
    {
        var pos=transform.position;
        PlayerPos pp=new(id,transform.position);
        var str=JsonUtility.ToJson(pp);
        return str;
    }

    public static PlayerPos StringToPosition(string str)
    {
        return JsonUtility.FromJson<PlayerPos>(str);        
    } 
}

[Serializable]
public struct PlayerPos
{
    public uint id;
    public Vector3 pos;
    public PlayerPos(uint id, Vector3 pos)
    {
        this.id=id;
        this.pos=pos;
    }
}