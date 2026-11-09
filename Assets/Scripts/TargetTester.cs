using Cysharp.Threading.Tasks;
using PrimeTween;
using Sirenix.OdinInspector;
using UnityEngine;

/// <summary>
/// 测试脚本
/// </summary>
public class TargetTester : MonoBehaviour
{
    Material mat;
    void Awake()
    {
        mat=GetComponent<MeshRenderer>().material;
    }


    [Button]
    public void ChangeColor(Color color)
    {
        mat.color=color;
    }
    [Button]
    public void Shinning()
    {
        Tween.MaterialColor(mat,Color.red,0.13f,ease:Ease.OutCubic).OnComplete(()=>Tween.MaterialColor(mat,Color.white,0.13f,ease:Ease.OutCubic));
        
    }
    
}
