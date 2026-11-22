using Core;

namespace GamePlay.Events
{
    /// <summary>通过类名和事件名生成相机事件编号。</summary>
    public static class CameraEvent
    {
        /// <summary>将场景相机挂到指定挂点。</summary>
        public static readonly int Attach = RuntimeID.ToRuntimeID("CameraEvent.Attach");

        /// <summary>将场景相机移回原父节点。</summary>
        public static readonly int Detach = RuntimeID.ToRuntimeID("CameraEvent.Detach");
    }
}
