using GamePlay.HoldSystem;
using UnityEngine;

namespace GamePlay.Player
{
    internal sealed class PlayerLoadoutDefaultsSO : ScriptableObject
    {
        [SerializeField] private KnifeSO _defaultKnife;

        public KnifeSO DefaultKnife => _defaultKnife;
    }
}
