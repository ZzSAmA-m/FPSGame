using UnityEngine;
namespace GamePlay.HoldSystem
{
    /// <summary>共享武器配置与模型预制体，每次装备创建独立的运行时武器。</summary>
    public abstract class WeaponSO : HoldableSO
    {
        [SerializeField, Tooltip("根节点挂有武器控制器的预制体，包含模型及其局部握持偏移。")]
        private WeaponController _controllerPrefab;

        public virtual WeaponController ControllerPrefab => _controllerPrefab;

        /// <summary>可选的表现资源接口；未资源化的刀具等装备仍使用原有配置。</summary>
        public virtual IWeaponPresentationResources PresentationResources => null;

        /// <summary>武器所属的装备栏。</summary>
        public abstract WeaponSlot Slot { get; }

        /// <summary>通用取物入口；调用方须保存返回的实例，不要重复读取来访问当前装备。</summary>
        public override IHoldable Item => CreateWeapon();

        /// <summary>使用当前序列化配置创建独立武器，不在 SO 中共享弹药和状态机。</summary>
        public abstract WeaponBase CreateWeapon();
    }

}
