using System;
using UnityEngine;

// ===========================================================================================
// UIEvents.cs —— UI 事件中心（静态委托）
// 职责：定义一组静态 UI 事件（Action 委托），供游戏内任意位置触发，UI 系统订阅后响应。
//       覆盖配件 UI、金币 / 经验 / 战斗（击杀、命中）等事件。
// ===========================================================================================
namespace cowsins
{
    // UI 事件中心：以静态委托形式暴露 UI 相关事件，供全局任意代码触发与订阅。
    /// <summary>
    /// Handles events for the UI. These can be called anywhere.
    /// </summary>
    public class UIEvents
    {
        // Attachment UI Events
        // 启用配件 UI 时触发，参数为配件 UI 物体
        public static Action<GameObject> onEnableAttachmentUI;
        // 点击配件 UI 元素时触发，参数为配件与是否装备
        public static Action<Attachment, bool> onAttachmentUIElementClicked;
        // 点击配件 UI 元素切换到新配件时触发，参数为新配件
        public static Action<Attachment> onAttachmentUIElementClickedNewAttachment;

        // Coins, Experience & Combat
        // 金币变化时触发，参数为金币数量与是否更新面板
        public static Action<int, bool> onCoinsChange;
        // 获得经验时触发，参数为是否更新面板
        public static Action<bool> onExperienceCollected;
        // 击杀敌人时触发，参数为被击杀者名称
        public static Action<string> onEnemyKilled;
        // 命中敌人时触发，参数为是否爆头、是否显示伤害飘字、命中位置、伤害值
        public static Action<bool, bool, Vector3, float> onEnemyHit;
    }
}
