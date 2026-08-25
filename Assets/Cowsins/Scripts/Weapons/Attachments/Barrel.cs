using UnityEngine;
// ===========================================================================================
// Barrel.cs —— 枪管配件脚本
// 职责：定义武器“枪管”配件（如消音枪管），继承自 Attachment 基类。装备时可替换武器的
//       开火音效与枪口焰特效，卸载时恢复武器原本的音效与特效，随后交由基类处理通用流程。
// ===========================================================================================
namespace cowsins
{
    /// <summary>
    /// 枪管配件：继承 Attachment 基类，装备时切换武器的开火音效与枪口焰，卸载时还原。
    /// </summary>
    public class Barrel : Attachment
    {
        // supressedFireSFX：装备后替换武器默认开火音效的消音音效（不可留空）
        // uniqueMuzzleFlashVFX：装备后替换武器默认枪口焰特效（留空则沿用武器上的默认特效）
        [Title("Barrel")]
        [Tooltip("SFX played instead of the original. You cannot leave this unassigned.")] public AudioClip[] supressedFireSFX;
        [Tooltip("You can leave this unassigned to use the default muzzle flash VFX on your weapon scriptable object")] public GameObject uniqueMuzzleFlashVFX;

        /// <summary>
        /// 装备枪管：若配置了消音音效则替换武器开火音效，若配置了专属枪口焰则替换枪口焰，
        /// 最后调用基类 Attach 完成通用装备流程。
        /// </summary>
        public override void Attach(WeaponIdentification Id)
        {
            if (supressedFireSFX.Length > 0) Id.fireSFXs = supressedFireSFX;
            if (uniqueMuzzleFlashVFX != null) Id.muzzleVFX = uniqueMuzzleFlashVFX;
            base.Attach(Id);
        }

        /// <summary>
        /// 卸载枪管：把武器的开火音效与枪口焰恢复为武器脚本资产上的默认值，
        /// 最后调用基类 Dettach 完成通用卸载流程。
        /// </summary>
        public override void Dettach(WeaponIdentification Id)
        {
            Weapon_SO weapon = Id.weapon;
            Id.fireSFXs = weapon.audioSFX.shooting;
            Id.muzzleVFX = weapon.muzzleVFX;
            base.Dettach(Id);
        }
    }
}