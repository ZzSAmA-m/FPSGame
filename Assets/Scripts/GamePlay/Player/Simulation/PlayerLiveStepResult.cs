using UnityEngine;

namespace GamePlay.Player.Simulation
{
    /// <summary>玩家实时输入门面完成一拍后的事实；不参与运动回滚或包含网络元数据。</summary>
    public readonly struct PlayerLiveStepResult
    {
        public PlayerSimState State { get; }
        public bool Fired { get; }
        /// <summary>仅在 Fired 为 true 时有效。</summary>
        public Vector3 FireOrigin { get; }
        /// <summary>仅在 Fired 为 true 时有效。</summary>
        public Vector3 FireDirection { get; }

        public PlayerLiveStepResult(in PlayerSimState state, bool fired, Vector3 fireOrigin, Vector3 fireDirection)
        {
            State = state;
            Fired = fired;
            FireOrigin = fireOrigin;
            FireDirection = fireDirection;
        }
    }
}
