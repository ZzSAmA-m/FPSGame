using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Core.ZLog;
using GamePlay.Weapon;
using UnityEngine;

namespace GamePlay.Player
{
    /// <summary>
    /// 处理能被拿在手上的物品的类 (枪,刀,道具...)
    /// </summary>
    public class PlayerHolder : MonoBehaviour
    {
        public IReadOnlyList<IHoldable> HoldItems=>_holdItems;
        [SerializeField]private List<IHoldable> _holdItems;
        /// <summary>现在手持的物品</summary>
        private IHoldable _curItem;
        /// <summary>现在手持的物品的序号</summary>
        private IHoldable _curIndex;
        void Awake()
        {
            SwitchItem(0);
        }
        void Start()
        {
            
        }

        void Init()
        {
            SwitchItem(0);
        }
        /// <summary> 切换手持物 </summary>
        void SwitchItem(int index)
        {
            if (!CheckIndex(index))
            {
                ZLog.LogError<PlayerHolder>("切换HoldItem时超出范围");

            }
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        bool CheckIndex(int index)
        {
            return index>=0&&index<_holdItems.Count;
        }
    }

}
