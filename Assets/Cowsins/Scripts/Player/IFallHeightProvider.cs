// =====================================================================
// IFallHeightProvider.cs
// 作用：定义“坠落高度提供者”接口。
// 该接口由 PlayerStats 实现，并被 PlayerDependencies 引用，用于解耦
// 对玩家当前坠落高度的读取与写入，供坠落伤害、坠落检测等逻辑统一使用。
// CurrentFallHeight 读取当前记录的坠落高度（可为 null，表示尚未记录）；
// SetFallHeight 用于写入新的坠落高度值。
// =====================================================================
namespace cowsins
{
    // Implemented by PlayerStats and required by PlayerDependencies
    /// <summary>
    /// 坠落高度提供者接口：约定实现方负责保存并提供玩家当前记录的坠落高度。
    /// </summary>
    public interface IFallHeightProvider
    {
        /// <summary>
        /// 获取玩家当前记录的坠落高度；返回类型为可空 float，
        /// 为 null 时表示当前尚未记录任何坠落高度。
        /// </summary>
        float? CurrentFallHeight { get; }
        /// <summary>
        /// 写入/更新玩家当前记录的坠落高度。
        /// </summary>
        /// <param name="newFallHeight">要记录的新坠落高度值。</param>
        void SetFallHeight(float newFallHeight);
    }
}