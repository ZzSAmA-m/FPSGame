// ============================================================================
// MeleeState.cs —— 近战攻击状态。
// 该状态是武器有限状态机（FSM）中的一个具体状态：玩家执行近战攻击（如挥刀）
// 时进入。进入后触发近战攻击并隐藏武器本体，在攻击动画播放期间持续计时，
// 动画结束并经过设定的延迟后自动结束近战并切回默认（Default）武器状态。
// ============================================================================
using UnityEngine;
using System.Collections; 

namespace cowsins
{
    /// <summary>
    /// 近战攻击状态：负责执行近战攻击、隐藏武器、等待攻击动画播放完毕，
    /// 并自动切换回默认武器状态。
    /// </summary>
    public class MeleeState : WeaponBaseState
    {
        private WeaponController controller; // 武器控制器引用，用于执行近战逻辑与读取武器设置
        private WeaponAnimator weaponAnimator; // 武器动画控制器引用，用于播放收枪/展示武器等动画

        private float timer; // 计时器：累计进入该状态后经过的时间
        private bool isSwitchQueued = false; // 标记是否已排队执行状态切换，防止重复切换

        private Animator meleeObject; // 近战武器对象上的 Animator，用于读取攻击动画状态与时长

        private float animationDuration; // 当前近战攻击动画的总时长

        // 构造函数：从上下文中缓存武器控制器、武器动画控制器，
        // 并获取近战武器对象上的 Animator 组件。
        public MeleeState(WeaponStates currentContext, WeaponStateFactory playerStateFactory)
            : base(currentContext, playerStateFactory)
        {
            controller = _ctx.WeaponController;
            weaponAnimator = _ctx.WeaponAnimator;
            meleeObject = controller.settings.meleeObject.GetComponent<Animator>(); 
        }

        // 进入近战状态：重置计时与切换标记，执行次级近战攻击，
        // 并隐藏武器本体（Id 对应的 GameObject）。
        public sealed override void EnterState()
        {
            timer = 0;
            isSwitchQueued = false;
            controller.quickActionBehaviour.SecondaryMeleeAttack();
            if(controller.Id != null) controller.Id.gameObject.SetActive(false);
        }

        // 每帧更新：退出瞄准状态，并检查是否需要切换回默认状态。
        public sealed override void UpdateState()
        {
            controller.aimBehaviour?.Exit();
            CheckSwitchState();
        }

        // 物理更新：近战状态无需额外物理逻辑，此处留空。
        public sealed override void FixedUpdateState()
        {
        }

        // 退出近战状态：恢复武器本体显示，并播放展示武器（MeleeShowWeapon）动画。
        public sealed override void ExitState()
        {
            if (controller.Id != null) controller.Id.gameObject.SetActive(true);
            _ctx.WeaponAnimator.HolsterMotionObject.Play("MeleeShowWeapon", 0, 0);
        }

        // 检查状态切换：累计时间并读取当前近战动画时长；
        // 当经过时间达到「动画时长 + 近战延迟」后，结束近战并切回默认状态。
        public sealed override void CheckSwitchState()
        {
            timer += Time.deltaTime;
            AnimatorStateInfo stateInfo = meleeObject.GetCurrentAnimatorStateInfo(0); // 读取第 0 层动画状态信息
            animationDuration = stateInfo.length; // 记录近战攻击动画的总时长
 
            // 仅当尚未排队且时间满足条件时才执行一次切换，避免重复触发
            if (!isSwitchQueued && timer >= animationDuration + controller.settings.meleeDelay)
            {
                controller.quickActionBehaviour.FinishMelee(); // 结束近战（收尾清理）

                AnimatorStateInfo holsterStateInfo = weaponAnimator.HolsterMotionObject.GetCurrentAnimatorStateInfo(0); // 读取收枪动画状态信息

                SwitchState(_factory.Default()); // 切回默认武器状态
                isSwitchQueued = true; // 标记已排队，防止后续帧重复切换
            }
        }
    }
}
