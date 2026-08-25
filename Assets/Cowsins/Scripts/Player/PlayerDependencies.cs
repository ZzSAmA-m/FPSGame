// ============================================================================
// PlayerDependencies.cs —— 玩家依赖组件集中管理器
// 作用：在 Awake 中通过 GetComponent 收集并缓存玩家运行所需的全部接口与组件引用，
//       获取完成后统一初始化输入、摄像机 FOV、摄像机特效、武器特效、UI 等子系统。
// 说明：本类不可被移除，且所有相关接口都必须由同一 GameObject 上的脚本实现，
//       否则会打印 "Missing required component" 错误。
// ============================================================================
using UnityEngine;

namespace cowsins
{
    /// <summary>玩家依赖集中管理器：负责收集并初始化玩家所需的全部依赖组件与接口。</summary>
    /// <summary>
    /// Stores references to all required dependencies for the Player to function properly.
    /// This class must not be removed and all the interfaces must be implemented.
    /// </summary>
    public class PlayerDependencies : MonoBehaviour
    {
        // 输入管理器引用：负责采集玩家的键盘/鼠标等输入。
        [SerializeField] private InputManager inputManager;
        // 摄像机 FOV 管理器引用：负责视野（Field of View）的动态缩放。
        [SerializeField] private CameraFOVManager cameraFOVManager;
        // 摄像机特效组件引用：负责镜头晃动、后坐力等视觉表现。
        [SerializeField] private CameraEffects cameraEffects;
        // 武器特效组件引用：负责武器开火、换弹等表现。
        [SerializeField] private WeaponEffects weaponEffects;
        // UI 控制器引用：负责 HUD 等界面逻辑。
        [SerializeField] private UIController uiController;
        // 准星组件引用：控制准星显示与状态。
        [SerializeField] private Crosshair crosshair;
        // UI 特效组件引用：负责准星扩散、命中提示等 UI 特效。
        [SerializeField] private UIEffects uIEffects;

        // 对外暴露输入管理器（只读）。
        public InputManager InputManager => inputManager;
        // 对外暴露摄像机 FOV 管理器（只读）。
        public CameraFOVManager CameraFOVManager => cameraFOVManager;
        // 对外暴露准星组件（只读）。
        public Crosshair Crosshair => crosshair;

        // PlayerMovement
        // 玩家移动事件提供者：向外抛出移动相关事件。
        public IPlayerMovementEventsProvider PlayerMovementEvents { get; private set; }
        // 玩家移动状态提供者：提供移动状态与朝向等信息。
        public IPlayerMovementStateProvider PlayerMovementState { get; private set; }

        // WeaponController
        // 武器引用提供者：提供当前武器的引用。
        public IWeaponReferenceProvider WeaponReference { get; private set; }
        // 武器行为提供者：提供武器的行为接口。
        public IWeaponBehaviourProvider WeaponBehaviour { get; private set; }
        // 武器后坐力提供者：提供后坐力数据。
        public IWeaponRecoilProvider WeaponRecoil { get; private set; }
        // 武器事件提供者：抛出武器相关事件。
        public IWeaponEventsProvider WeaponEvents { get; private set; }

        // PlayerStats
        // 可受伤接口：供外部对玩家造成伤害。
        public IDamageable Damageable { get; private set; }
        // 玩家属性提供者：提供生命值等属性。
        public IPlayerStatsProvider PlayerStats { get; private set; }
        // 玩家属性事件提供者：抛出属性变化事件。
        public IPlayerStatsEventsProvider PlayerStatsEvents { get; private set; }
        // 坠落高度提供者：提供坠落伤害计算所需的高度。
        public IFallHeightProvider FallHeight { get; private set; }

        // InteractManager
        // 交互管理提供者：提供交互相关的管理接口。
        public IInteractManagerProvider InteractManager { get; private set; }
        // 交互事件提供者：抛出交互相关事件。
        public IInteractEventsProvider InteractEvents { get; private set; }

        // PlayerControl
        // 玩家控制提供者：提供玩家控制相关接口。
        public IPlayerControlProvider PlayerControl { get; private set; }

        // PlayerMultipliers
        // 玩家乘数提供者：提供各类属性乘数。
        public IPlayerMultipliers PlayerMultipliers { get; private set; }


        // 在脚本唤醒时执行：收集所有必需组件并完成各子系统初始化。
        // 使用 GetRequiredComponent 获取接口实现，若缺失则打印错误日志。
        private void Awake()
        {
            PlayerMovementEvents = GetRequiredComponent<IPlayerMovementEventsProvider>();
            PlayerMovementState = GetRequiredComponent<IPlayerMovementStateProvider>();

            WeaponReference = GetRequiredComponent<IWeaponReferenceProvider>();
            WeaponBehaviour = GetRequiredComponent<IWeaponBehaviourProvider>();
            WeaponRecoil = GetRequiredComponent<IWeaponRecoilProvider>();
            WeaponEvents = GetRequiredComponent<IWeaponEventsProvider>();

            Damageable = GetRequiredComponent<IDamageable>();
            PlayerStats = GetRequiredComponent<IPlayerStatsProvider>();
            PlayerStatsEvents = GetRequiredComponent<IPlayerStatsEventsProvider>();
            FallHeight = GetRequiredComponent<IFallHeightProvider>();

            InteractManager = GetRequiredComponent<IInteractManagerProvider>();
            InteractEvents = GetRequiredComponent<IInteractEventsProvider>();

            PlayerControl = GetRequiredComponent<IPlayerControlProvider>();

            PlayerMultipliers = GetRequiredComponent<IPlayerMultipliers>();

            // 初始化各子系统：以空值安全的方式设置玩家引用并执行各自的初始化逻辑。
            inputManager?.SetPlayer(this);
            cameraFOVManager?.Initialize(this);
            cameraEffects?.Initialize(this);
            weaponEffects?.Initialize(this);
            uiController?.Initialize(this);
            uIEffects?.Initialize(this);
        }
        // 从当前 GameObject 上获取指定类型的必需组件；若缺失则打印错误日志并返回 null。
        private T GetRequiredComponent<T>() where T : class
        {
            var component = GetComponent<T>();
            if (component == null) CowsinsUtilities.LogError("Missing required component of type {typeof(T).Name} on {gameObject.name}");
            return component;
        }
    }
}