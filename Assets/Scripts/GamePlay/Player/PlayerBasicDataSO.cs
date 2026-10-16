using System;
using Core.Utility;
using UnityEngine;
namespace GamePlay.Player
{
    [CreateAssetMenu(fileName = "PlayerBasicDataSO", menuName = "Scriptable Objects/PlayerBasicDataSO")]
    public class PlayerBasicDataSO : ScriptableObject
    {
        [SerializeField]private EPlayerHandState _ePlayerHandState;          //玩家手持物状态
        [SerializeField]private int _playerHealth;                           //玩家血量


        public EPlayerHandState playerHandState =>_ePlayerHandState;
        public int playerHealth=>_playerHealth;
    }

}
