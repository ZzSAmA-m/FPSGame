// ============================================================================
// WeaponHiddenState.cs —— 武器隐藏状态。
// 当武器被收起（例如未装备武器或执行某些动作）时进入该状态：进入时播放
// 收枪（hit）动画，将武器隐藏；退出时播放展示武器（MeleeShowWeapon）动画
// 使武器重新出现。该状态本身不主动驱动状态切换。
// ============================================================================
using UnityEngine;

namespace cowsins
{
    /// <summary>
    /// 武器隐藏状态：进入时播放收枪动画隐藏武器，退出时播放展示动画恢复武器。
    /// </summary>
    public class WeaponHiddenState : WeaponBaseState
    {
        private WeaponController controller; // 武器控制器引用
        // 玩家控制接口引用
        private IPlayerControlProvider playerControl; // IPlayerControlProvider is implemented in PlayerControl.cs

        // 构造函数：从上下文中缓存武器控制器与玩家控制引用。
        public WeaponHiddenState(WeaponStates currentContext, WeaponStateFactory playerStateFactory)
            : base(currentContext, playerStateFactory)
        {
            controller = _ctx.WeaponController;
            playerControl = _ctx.PlayerControlProvider;
        }

        // 进入隐藏状态：在收枪动画对象上播放 "hit" 动画以隐藏武器。
        public sealed override void EnterState()
        {
            CowsinsUtilities.PlayAnim("hit", _ctx.WeaponAnimator.HolsterMotionObject);
        }

        // 每帧更新：隐藏状态无需逐帧逻辑，留空。
        public sealed override void UpdateState()
        {
        }

        // 物理更新：隐藏状态无需物理逻辑，留空。
        public sealed override void FixedUpdateState()
        {
        }

        // 退出隐藏状态：播放展示武器动画，使武器重新可见。
        public sealed override void ExitState() 
        {
            _ctx.WeaponAnimator.HolsterMotionObject.Play("MeleeShowWeapon", 0, 0);
        }

        // 检查状态切换：隐藏状态不主动切换状态，留空。
        public sealed override void CheckSwitchState() {}
    }

}