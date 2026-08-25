// =====================================================================
// PlayerDebugger.cs
// 作用：在屏幕上叠加一个运行期调试面板（OnGUI），用于实时查看玩家的
// 移动状态、武器状态、生命/护盾、交互进度以及输入值等关键信息，
// 方便在编辑器或开发版构建中排查问题。
// 注意：面板代码被 #if UNITY_EDITOR || DEVELOPMENT_BUILD 包裹，仅在这些环境下编译。
// =====================================================================
using UnityEngine;

namespace cowsins
{
    /// <summary>
    /// 玩家调试面板：挂载在玩家身上，通过 OnGUI 在屏幕上绘制多个信息分组，
    /// 实时展示玩家移动、武器、属性、交互与输入等运行期状态。
    /// </summary>
    public class PlayerDebugger : MonoBehaviour
    {
        // 调试面板顶部留白（像素），避免遮挡屏幕顶部信息。
        private const float TopMargin = 130f;
        // 每个信息框的宽度（像素）。
        private const float BoxWidth = 320f;
        // 每行标签的高度（像素）。
        private const float LabelHeight = 20f;
        // 标签之间、以及标签与框边缘之间的垂直间距（像素）。
        private const float LabelPadding = 5f;

        // Player Components
        // 玩家依赖聚合对象：集中提供各类玩家子系统的引用。
        private PlayerDependencies playerDependencies;
        // 玩家移动组件引用（用于读取跳跃等移动上下文）。
        private PlayerMovement playerMovement;
        // 玩家基础属性提供者（生命值/护盾等）。
        private IPlayerStatsProvider playerStatsProvider; // IPlayerStatsProvider is implemented in PlayerStats.cs
        // 玩家控制状态提供者（是否可控）。
        private IPlayerControlProvider playerControlProvider; // IPlayerControlProvider is implemented in PlayerControl.cs
        // 玩家移动状态提供者（速度、是否落地、是否蹲伏等）。
        private IPlayerMovementStateProvider playerMovementProvider; // IPlayerMovementStateProvider is implemented in PlayerMovement.cs
        // 武器引用提供者（当前武器对象与武器数据）。
        private IWeaponReferenceProvider weaponController; // IWeaponReferenceProvider is implemented in WeaponController.cs
        // 武器行为提供者（是否换弹、是否瞄准等）。
        private IWeaponBehaviourProvider weaponBehaviour; // IWeaponBehaviourProvider is implemented in WeaponController.cs
        // 交互管理器提供者（高亮可交互物与交互进度）。
        private IInteractManagerProvider interactManager; // IInteractManagerProvider is implemented in InteractManager.cs
        // 玩家状态机引用（用于显示当前玩家状态）。
        private PlayerStates playerStates;
        // 武器状态机引用（用于显示当前武器状态）。
        private WeaponStates weaponStates;
        // 刚体引用（用于显示玩家移动速度/速度向量）。
        private Rigidbody rb;
        // 输入管理器引用（用于显示移动、视角、射击等输入值）。
        private InputManager inputManager;

        // Get references
        // Start：在初始化阶段获取玩家各子系统引用——先直接 GetComponent 获取
        // 依赖容器、状态机与刚体，再通过 PlayerDependencies 间接获取其余子系统，供 OnGUI 面板读取。
        private void Start()
        {
            playerDependencies = GetComponent<PlayerDependencies>();
            playerStates = GetComponent<PlayerStates>();
            weaponStates = GetComponent<WeaponStates>();
            rb = GetComponent<Rigidbody>();

            playerMovement = GetComponent<PlayerMovement>();
            playerStatsProvider = playerDependencies.PlayerStats;
            playerControlProvider = playerDependencies.PlayerControl;
            playerMovementProvider = playerDependencies.PlayerMovementState;
            weaponController = playerDependencies.WeaponReference;
            weaponBehaviour = playerDependencies.WeaponBehaviour;
            interactManager = playerDependencies.InteractManager;
            inputManager = playerDependencies.InputManager;
        }

        // BeginBox：标记一个信息框的起始位置——先以“零高度”绘制框以记录顶部 Y，
        // 再将 currentY 下移一行，并返回框的起始 Y，供 EndBox 之后计算实际高度并重绘。
        private float BeginBox(string title, ref float currentY)
        {
            float boxStartY = currentY;
            currentY += LabelHeight + LabelPadding;
            GUI.Box(new Rect(10, boxStartY, BoxWidth, 0), title);
            return boxStartY;
        }

        // DrawLabel：在指定纵向位置绘制一行文本标签，并将 currentY 下移一行，
        // 使后续标签依次向下排列。
        private void DrawLabel(ref float currentY, string label)
        {
            GUI.Label(new Rect(20, currentY, BoxWidth - 20, LabelHeight), label);
            currentY += LabelHeight + LabelPadding;
        }

        // EndBox：根据框的起始 Y 与当前 Y 计算内容实际高度，再用正确高度重新绘制信息框边框。
        private void EndBox(float boxStartY, float currentY, string title)
        {
            float height = currentY - boxStartY;
            GUI.Box(new Rect(10, boxStartY, BoxWidth, height), title);
        }

// 仅在编辑器或开发版构建中编译此调试面板：正式发布版不包含，避免额外开销。
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        // OnGUI：使用 Unity 立即模式 GUI 在屏幕上绘制调试面板，
        // 依次以“起始框 → 逐行标签 → 结束框”的方式渲染多个信息分组。
        void OnGUI()
        {
            float currentY = TopMargin;

            // Player Information Box
            // 玩家信息框：显示闲置状态、速度、位置、朝向、状态机、是否落地等。
            float boxStartY = BeginBox("Player Information", ref currentY);
            DrawLabel(ref currentY, $"Player Is Idle: {playerMovementProvider.IsIdle}");
            DrawLabel(ref currentY, $"Player Current Speed: {Mathf.Round(playerMovementProvider.CurrentSpeed)}");
            DrawLabel(ref currentY, $"Player Velocity: {Mathf.Round(rb.linearVelocity.magnitude)}");
            DrawLabel(ref currentY, $"Player Position: {transform.position}");
            DrawLabel(ref currentY, $"Player Orientation: {playerMovementProvider.Orientation.Forward}");
            DrawLabel(ref currentY, $"Player State: {playerStates?.CurrentState}");
            DrawLabel(ref currentY, $"Grounded: {playerMovementProvider.Grounded}");
            if(playerMovement != null)
            {
                DrawLabel(ref currentY, $"HasJumped: {playerMovement.movementContext.HasJumped}");
            }
            DrawLabel(ref currentY, $"Crouching: {playerMovementProvider.IsCrouching}");
            DrawLabel(ref currentY, $"Is Sliding: {playerMovementProvider.IsSliding}");
            if (playerMovement != null)
            {
                DrawLabel(ref currentY, $"Is Player On Slope: {playerMovement.movementContext.IsPlayerOnSlope}");
                DrawLabel(ref currentY, $"Current Jumps: {playerMovement.jumpBehaviour.jumpCount}");
                DrawLabel(ref currentY, $"Wall Running: {playerMovement.IsWallRunning}");
            }
            EndBox(boxStartY, currentY, "Player Information");

            // Weapon Information Box
            // 武器信息框：显示当前武器 ScriptableObject、ID、总弹量、弹匣余弹、换弹/瞄准状态等。
            boxStartY = BeginBox("Weapon Information", ref currentY);
            DrawLabel(ref currentY, $"Weapon_SO: {weaponController.Weapon}");
            DrawLabel(ref currentY, $"Weapon Object: {weaponController.Id}");
            DrawLabel(ref currentY, $"Weapon Total Bullets: {weaponController.Id?.totalBullets}");
            DrawLabel(ref currentY, $"Weapon Current Bullets: {weaponController.Id?.bulletsLeftInMagazine}");
            DrawLabel(ref currentY, $"Reloading: {weaponBehaviour.IsReloading}");
            DrawLabel(ref currentY, $"Weapon State: {weaponStates?.CurrentState}");
            DrawLabel(ref currentY, $"Weapon Aiming: {weaponBehaviour.IsAiming}");
            EndBox(boxStartY, currentY, "Weapon Information");

            // Player Stats Information Box
            // 玩家属性信息框：显示生命值与护盾的当前/最大值，以及是否可控。
            boxStartY = BeginBox("Player Stats Information", ref currentY);
            DrawLabel(ref currentY, $"Health: {Mathf.Round(playerStatsProvider.Health)} / {playerStatsProvider.MaxHealth}");
            DrawLabel(ref currentY, $"Shield: {Mathf.Round(playerStatsProvider.Shield)} / {playerStatsProvider.MaxShield}");
            DrawLabel(ref currentY, $"Controllable: {playerControlProvider.IsControllable}");
            EndBox(boxStartY, currentY, "Player Stats Information");

            // Interact Manager Information Box
            // 交互管理器信息框：显示当前高亮的可交互物名称与交互进度。
            boxStartY = BeginBox("Interact Manager Information", ref currentY);
            DrawLabel(ref currentY, $"Highlighted Interactable: {interactManager.HighlightedInteractable?.name}");
            DrawLabel(ref currentY, $"Interact Progress: {interactManager.ProgressElapsed:F1}");
            EndBox(boxStartY, currentY, "Interact Manager Information");

            // Input Manager Box
            // 输入管理器信息框：显示移动、视角、手柄视角以及射击/换弹/瞄准等输入状态。
            boxStartY = BeginBox("Input Manager", ref currentY);
            DrawLabel(ref currentY, $"Movement: ({inputManager.X:F1},{inputManager.Y:F1})");
            DrawLabel(ref currentY, $"Look: ({inputManager.Mousex:F1},{inputManager.Mousey:F1})");
            DrawLabel(ref currentY, $"Gamepad Look: ({inputManager.Controllerx:F1},{inputManager.Controllery:F1})");
            DrawLabel(ref currentY, $"Shooting: {inputManager.Shooting}");
            DrawLabel(ref currentY, $"Reloading: {inputManager.Reloading}");
            DrawLabel(ref currentY, $"Aiming: {inputManager.Aiming}");
            DrawLabel(ref currentY, $"Sprinting: {inputManager.Sprinting}");
            DrawLabel(ref currentY, $"Crouching: {inputManager.Crouching}");
            DrawLabel(ref currentY, $"Interacting: {inputManager.Interacting}");
            EndBox(boxStartY, currentY, "Input Manager");
        }
#endif
    }
}
