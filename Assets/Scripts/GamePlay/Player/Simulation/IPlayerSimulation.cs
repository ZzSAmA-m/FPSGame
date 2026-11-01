namespace GamePlay.Player.Simulation
{
    /// <summary>服务器和客户端共用的模拟与状态恢复入口。</summary>
    public interface IPlayerSimulation
    {
        PlayerSimState State { get; }
        PlayerStepResult Step(in PlayerInputFrame input);
        void RestoreState(in PlayerSimState state);
    }
}
