// =====================================================================
// PlayerControl.cs
// 作用：管理玩家的“可控状态”（全局可控与移动可控）。
// 当玩家不可控（controllable == false）时，无法移动或执行任何操作；
// 通过 GrantControl / LoseControl / ToggleControl / CheckIfCanGrantControl
// 等公开方法可以在运行时授予、剥夺、切换或按条件恢复玩家控制权。
// =====================================================================
using UnityEngine;

namespace cowsins
{
    // This class controls the controllable state of the player.
    // If non-controllable ( controllable == false ), the player won´t be able to move or perform any action.
    // GrantControl, LoseControl, ToggleControl and CheckIfCanGrantControl methods can be accessed to handle the player control.
    /// <summary>
    /// 玩家控制状态管理器：挂载在玩家身上，负责维护全局可控与移动可控两个开关，
    /// 并实现 IPlayerControlProvider 接口供其他系统（如 PlayerDependencies）查询与操作。
    /// </summary>
    public class PlayerControl : MonoBehaviour, IPlayerControlProvider
    {
        /// <summary>
        /// 玩家当前是否整体可被控制（可执行任何操作）。
        /// </summary>
        public bool IsControllable => controllable;
        /// <summary>
        /// 玩家当前是否可移动（独立于全局可控的移动开关）。
        /// </summary>
        public bool IsMovementControllable => movementControllable;

        // 全局可控标志：决定玩家能否执行任何操作。
        private bool controllable = true;
        // 移动可控标志：决定玩家能否移动，独立于全局可控标志。
        private bool movementControllable = true;

        // Reference to PlayerStats.cs ( IPlayerStatsProvider is implemented in PlayerStats.cs )
        // 玩家基础属性提供者引用：用于在授予控制权前判断玩家是否已死亡。
        private IPlayerStatsProvider playerStatusProvider; 

        // Awake：在组件初始化时获取玩家基础属性提供者，并默认授予玩家控制权。
        private void Awake()
        {
            playerStatusProvider = GetComponent<IPlayerStatsProvider>();
            GrantControl();
        }

        /***************************************** GLOBAL/CORE CONTROL *************************************************/
        // 以下方法用于全局控制：影响玩家能否执行任何操作。

        // 无条件强制玩家进入可控状态；更推荐使用 CheckIfCanGrantControl() 来按条件授予。
        /// <summary>
        /// Forces the player to be controlled. CheckIfCanGrantControl() method is recommended instead.
        /// </summary>
        public void GrantControl()
        {
            controllable = true;
            GrantMovementControl();
        } 
            

        // 剥夺玩家控制权，将其置为不可控。
        /// <summary>
        /// Disallows the player to be controlled.
        /// </summary>
        public void LoseControl() => controllable = false;

        // 切换玩家的可控状态（可控 ↔ 不可控）。
        /// <summary>
        /// Toggles the controllable state of the player.
        /// </summary>
        public void ToggleControl() => controllable = !controllable;

        // 在授予控制权前先检查前置条件：游戏未暂停且玩家未死亡，否则直接返回。
        /// <summary>
        /// Checks if the game is paused or the player is dead before allowing the player to be controlled.
        /// </summary>
        public void CheckIfCanGrantControl()
        {
            if (PauseMenu.isPaused || playerStatusProvider?.IsDead == true) return;
            GrantControl();
        }


        /***************************************** MOVEMENT CONTROL *************************************************/
        // 以下方法用于移动控制：独立于全局控制，只影响玩家的移动能力。
        // 允许玩家移动；若全局控制被禁用，此设置不会生效。
        /// <summary>
        /// Allows movement. If Global Control is disabled, this will have no effect.
        /// </summary>
        public void GrantMovementControl() => movementControllable = true;

        // 禁止玩家移动；该开关独立于全局控制系统。
        /// <summary>
        /// Disallows the player movement. This is independent to the Global Control System
        /// </summary>
        public void LoseMovementControl() => movementControllable = false;

        // 切换玩家的移动可控状态（可移动 ↔ 不可移动）。
        /// <summary>
        /// Toggles the movement controllable state of the player.
        /// </summary>
        public void ToggleMovementControl() => movementControllable = !movementControllable;
    }
}