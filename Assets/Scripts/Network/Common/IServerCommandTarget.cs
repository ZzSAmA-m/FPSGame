using Network.Common.Messages;

namespace Network.Common
{
    /// <summary>
    /// 服务器把一个采样窗口交给玩家对象执行。
    /// 由 GamePlay 实现，这样 Network 不用引用具体的玩家类型。
    /// </summary>
    public interface IServerCommandTarget
    {
        void ApplyServerCommand(ref PlayerCommandMessage command);
    }
}
