// =====================================================================
// IPlayerControlProvider.cs
// 作用：定义“玩家控制状态提供者”接口。
// 由 PlayerControl 实现并被 PlayerDependencies 引用，用于统一查询玩家
// 是否处于可被控制/可移动的状态，以及授予、剥夺或按条件恢复玩家控制权。
// =====================================================================
namespace cowsins
{
    // Implemented by PlayerControl and required by PlayerDependencies
    /// <summary>
    /// 玩家控制状态提供者接口：约定实现方负责维护玩家的全局可控与移动可控状态。
    /// </summary>
    public interface IPlayerControlProvider
    {
        /// <summary>
        /// 获取玩家当前是否处于可被控制（可执行任何操作）的状态。
        /// </summary>
        bool IsControllable { get; }
        /// <summary>
        /// 获取玩家当前是否处于可移动的状态。
        /// </summary>
        bool IsMovementControllable { get; }

        /// <summary>
        /// 强制授予玩家控制权（无条件置为可控）。
        /// </summary>
        void GrantControl();
        /// <summary>
        /// 剥夺玩家控制权（置为不可控）。
        /// </summary>
        void LoseControl();
        /// <summary>
        /// 在满足条件（游戏未暂停、玩家未死亡）时授予玩家控制权。
        /// </summary>
        void CheckIfCanGrantControl();
    }
}