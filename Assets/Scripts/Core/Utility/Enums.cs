/// Enum.cs
/// 存放枚举
namespace Core.Utility
{
    public enum EPlayerCommandType
    {
        Move,
        Look,
    
        Jump,
        Sprint,
        Crouch,
    
        Fire,
        Reload,
    
        Interact
    }

    /// <summary>
    /// 玩家模型类型(第一人称/第三人称)
    /// </summary>
    public enum EPlayerModelType
    {
        /// <summary>
        /// null
        /// </summary>
        None,   
        ///<summary>
        /// 第一人称
        /// </summary>                
        FP,       
        
        ///<summary>
        /// 第三人称
        /// </summary>                
        TP
    }

    /// <summary>
    /// 玩家手持状态
    /// </summary>
    public enum EPlayerHandState
    {
        /// <summary>
        /// 刀
        /// </summary>
        Knife,
        /// <summary>
        /// 手枪
        /// </summary>
        Pistol,
        /// <summary>
        /// 步枪
        /// </summary>
        Rifle,
        /// <summary>
        /// 道具1
        /// </summary>
        Prop1,
        /// <summary>
        /// 道具2
        /// <summary>
        Prop2,
        /// <summary>
        /// 道具3
        /// </summary>
        Prop3,
        /// <summary>
        /// C4炸弹
        /// </summary>
        Bomb   
    }

    

    /// <summary>
    /// 当前 Unity 进程在 Mirror 网络中的运行角色。
    /// </summary>
    public enum NetState
    {
        /// <summary>客户端与服务器都未运行。</summary>
        None,

        /// <summary>只运行服务端，通常对应 Dedicated Server。</summary>
        Server,

        /// <summary>只运行客户端。</summary>
        Client,

        /// <summary>同一进程同时运行服务器和本地客户端。</summary>
        Host
    }

    public enum WeaponInputAction
    {
        //数字键1-3
        Key1,
        Key2,
        Key3,
        //end
        /// <summary>
        /// 鼠标左键
        /// </summary>
        MLeft,
        /// <summary>
        /// 鼠标右键
        /// </summary>
        MRight,
        /// <summary>
        /// R键
        /// </summary>
        R,
        /// <summary>
        /// 空格
        /// </summary>
        Space
    }
}