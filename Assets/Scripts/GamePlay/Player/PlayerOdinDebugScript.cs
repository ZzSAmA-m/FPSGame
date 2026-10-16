using GamePlay.Player;
using Sirenix.OdinInspector;
using UnityEngine;

public class PlayerOdinDebugScript : MonoBehaviour
{
    PlayerModelController playerModelController;
    PlayerSightController playerSightController;
    void Awake()
    {
        playerModelController=GetComponent<PlayerModelController>();
        playerSightController=GetComponent<PlayerSightController>();
    }
    

    [Button]
    public void FPModel()=>playerModelController.SwitchModel(Core.Utility.EPlayerModelType.FP);
    [Button]
    public void TPModel()=>playerModelController.SwitchModel(Core.Utility.EPlayerModelType.TP);
    [Button]
    public void EnableLook()=>playerSightController.EnableLook();
    [Button]
    public void DisableLook()=>playerSightController.DisableLook();
}
