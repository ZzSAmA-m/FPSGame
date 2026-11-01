namespace GamePlay.Player.Simulation.Reconciliation
{
    /// <summary>比较相距不到半个 uint 范围的 Tick，支持序号回绕。</summary>
    public static class TickMath
    {
        public static bool IsNewer(uint value, uint other) => unchecked((int)(value - other)) > 0;

        public static bool IsOlder(uint value, uint other) => unchecked((int)(value - other)) < 0;
    }
}
