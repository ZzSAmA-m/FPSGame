// ===========================================================================================
// AttachmentType.cs —— 配件类型枚举
// 职责：定义武器可装备的所有配件类型，供配件系统、检视 UI 与编辑器使用，
//       用于区分枪管、瞄准镜、枪托、握把、弹匣、手电筒与激光器等配件类别。
// ===========================================================================================
namespace cowsins
{
    // 配件类型枚举：列出武器上可装备的所有配件种类，供配件状态管理与兼容性查询使用。
    /// <summary>
    /// Defines the different types of attachments that can be equipped on weapons.
    /// </summary>
    public enum AttachmentType
    {
        // 枪管（如消音枪管）
        Barrel,
        // 瞄准镜
        Scope,
        // 枪托
        Stock,
        // 握把
        Grip,
        // 弹匣
        Magazine,
        // 手电筒
        Flashlight,
        // 激光瞄准器
        Laser
    }
}
