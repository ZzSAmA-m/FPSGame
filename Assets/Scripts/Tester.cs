
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using UnityEngine;
public class Tester : MonoBehaviour
{

    
    /// <summary>创建枪械并开始准备动作。</summary>
    private void Start()
    {
        TestFunc().Forget();
    }
    async UniTask TestFunc()
    {
        GameModule.UI.Show<TestWindow>();
        GameModule.UI.Show<CrossHairUI>();
    }

}