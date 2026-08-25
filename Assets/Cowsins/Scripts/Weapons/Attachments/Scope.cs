using UnityEngine;
// ===========================================================================================
// Scope.cs —— 瞄准镜配件脚本
// 职责：定义武器“瞄准镜”配件，继承自 Attachment 基类。装备时应用瞄准镜的瞄准位置偏移
//       与瞄准旋转角度，卸载时恢复默认值，随后交由基类处理通用流程。
// ===========================================================================================
namespace cowsins
{
    /// <summary>
    /// 瞄准镜配件：继承 Attachment 基类，通过 aimingOffset / aimingRotation 控制瞄准姿态。
    /// </summary>
    public class Scope : Attachment
    {
        // aimingOffset：装备该瞄准镜后瞄准时武器位置的额外偏移量
        // aimingRotation：装备该瞄准镜后瞄准时武器的旋转角度
        [Title("Scope")]
        [Tooltip("Vector3 added to the position of th weapon when aiming with this scoped equipped.")] public Vector3 aimingOffset;
        [Tooltip("Rotation of the weapon when aiming with this scope equipped.")] public Vector3 aimingRotation;

        /// <summary>
        /// 装备瞄准镜：把武器的瞄准位置偏移与瞄准旋转设置为该瞄准镜配置的值，
        /// 最后调用基类 Attach 完成通用装备流程。
        /// </summary>
        public override void Attach(WeaponIdentification Id)
        {
            Id.aimingOffset = aimingOffset;
            Id.aimingRotation = aimingRotation;
            base.Attach(Id);
        }

        /// <summary>
        /// 卸载瞄准镜：把武器瞄准位置偏移清零，瞄准旋转恢复为武器默认值，
        /// 最后调用基类 Dettach 完成通用卸载流程。
        /// </summary>
        public override void Dettach(WeaponIdentification Id)
        {
            Id.aimingOffset = Vector3.zero;
            Id.aimingRotation = Id.weapon.aimingRotation;
            base.Dettach(Id);
        }
    }
}