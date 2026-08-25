// =============================================================================
// WeaponContext.cs
// 武器上下文（WeaponContext）：集中聚合武器系统运行所需的各类引用与共享状态，
// 作为单一“上下文”对象在 WeaponController 与各行为子模块（射击/换弹/瞄准等）之间传递，
// 避免各模块重复查找依赖，并统一保存开火点、音频源、输入等运行时资源与全局开关。
// =============================================================================
using UnityEngine;

namespace cowsins
{
    /// <summary>
    /// 武器上下文：持有武器系统运行所需的全部核心引用与全局状态，
    /// 由 WeaponController 在启动时构建，供各武器行为子模块共享使用。
    /// </summary>
    public class WeaponContext
    {
        // Core References 
        // 武器自身挂载点的 Transform（即 WeaponController 所在物体的 transform）
        public Transform Transform { get; set; }
        // 武器持握点（武器模型的父节点/持握槽位），武器预制体会被实例化到其下
        public Transform WeaponHolder { get; set; }
        // 输入管理器，提供射击、瞄准、换弹等按键输入
        public InputManager InputManager { get; set; }
        // 玩家依赖聚合对象，集中持有玩家各模块（移动/交互/状态等）的引用
        public PlayerDependencies Dependencies { get; set; }
        // 开火点（枪口）Transform 数组，供命中检测、抛射物生成与特效使用
        public Transform[] firePoint { get; set; }
        // 用于运行协程的 MonoBehaviour（通常为 WeaponController 自身）
        public MonoBehaviour CoroutineRunner { get; set; }
        // 音频源，用于播放射击、换弹等音效
        public AudioSource AudioSource { get; set; }

        // 武器控制器设置对象，保存武器系统的可配置项（准星、后坐力、散布等）
        public WeaponControllerSettings Settings { get; set; }

        // Globals
        // 全局标志：当前是否允许射击（由状态机与各系统共同维护）
        public bool CanShoot;
    }
}