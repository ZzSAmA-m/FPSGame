using GamePlay.Player;
using UnityEngine;

public class PlayerBrain : MonoBehaviour
{
    [SerializeField]private PlayerBasicDataSO _playerBasicData;
    [Sirenix.OdinInspector.ReadOnly]private PlayerController _playerController;
}
