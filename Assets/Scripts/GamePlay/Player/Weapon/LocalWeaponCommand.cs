namespace GamePlay.Player
{
    /// <summary>从本拍输入提取武器指令，供实时武器步进使用。</summary>
    public readonly struct LocalWeaponCommand
    {
        public readonly uint Tick;
        public readonly bool FireRequested;
        public readonly bool ReloadRequested;
        public readonly bool LightAttackPressed;
        public readonly bool HeavyAttackPressed;

        public LocalWeaponCommand(uint tick, bool fireRequested, bool reloadRequested = false,
            bool lightAttackPressed = false, bool heavyAttackPressed = false)
        {
            Tick = tick;
            FireRequested = fireRequested;
            ReloadRequested = reloadRequested;
            LightAttackPressed = lightAttackPressed;
            HeavyAttackPressed = heavyAttackPressed;
        }
    }
}
