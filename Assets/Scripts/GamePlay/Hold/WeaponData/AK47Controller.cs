namespace GamePlay.HoldSystem
{
    /// <summary>AK47 模型的专用控制器；通过 Gun 访问这把枪的状态和表现事件。</summary>
    public sealed class AK47Controller : WeaponController
    {
        public AK47 Gun => Weapon as AK47;

        public override bool CanBind(WeaponBase weapon) => weapon is AK47;
    }
}
