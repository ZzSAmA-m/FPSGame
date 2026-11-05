using UnityEngine;
namespace GamePlay.HoldSystem
{
    public abstract class HoldableSO : ScriptableObject,IHoldable
    {
        public abstract IHoldable Item{get;} 
    }

}
