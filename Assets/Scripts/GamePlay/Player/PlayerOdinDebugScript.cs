using GamePlay.Player;
using Sirenix.OdinInspector;
using UnityEngine;

public class PlayerOdinDebugScript : MonoBehaviour
{
    PlayerMotionController playerMotionController;
    PlayerSightController playerSightController;
    void Awake()
    {
        playerMotionController=GetComponent<PlayerMotionController>();
        playerSightController=GetComponent<PlayerSightController>();
    }
    

    [Button]
    public void FPModel()=>playerMotionController.SetView(Core.Utility.EPlayerModelType.FP);
    [Button]
    public void TPModel()=>playerMotionController.SetView(Core.Utility.EPlayerModelType.TP);
    [Button]
    public void EnableLook()=>playerSightController.EnableLook();
    [Button]
    public void DisableLook()=>playerSightController.DisableLook();
}
