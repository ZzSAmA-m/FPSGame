
using UnityEngine;
public class Tester : MonoBehaviour
{

    
    /// <summary>创建枪械并开始准备动作。</summary>
    private void Start()
    {
        GameModule.UI.Show<TestWindow>();
        GameModule.UI.Show<CrossHairUI>();
    }

}