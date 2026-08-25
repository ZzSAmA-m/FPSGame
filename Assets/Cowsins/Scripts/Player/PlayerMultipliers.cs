// ============================================================================
// PlayerMultipliers.cs —— 玩家属性乘数系统
// 作用：管理玩家的伤害、治疗、体重等可修改属性的乘数（ModifiableStat），
//       支持临时修改器的添加、刷新与到期自动移除，并实现 IPlayerMultipliers 接口。
// ============================================================================
using System.Collections.Generic;
using UnityEngine;

namespace cowsins
{
    /// <summary>玩家乘数系统：维护并管理各属性乘数以及带持续时间的临时修改器。</summary>
    public class PlayerMultipliers : MonoBehaviour, IPlayerMultipliers
    {
        /// <summary>临时修改器数据：记录被修改的属性、修改器本身以及过期时间。</summary>
        public class TemporaryModifierData
        {
            // 被修改的属性对象。
            public ModifiableStat Stat;
            // 作用于该属性的修改器。
            public StatModifier Modifier;
            // 该修改器的过期时刻（基于 Time.time）。
            public float ExpirationTime;
        }
        
        // 当前生效中的临时修改器列表。
        private List<TemporaryModifierData> activeTemporaryModifiers = new List<TemporaryModifierData>();
        // 对外只读暴露当前生效的临时修改器列表。
        public IReadOnlyList<TemporaryModifierData> ActiveTemporaryModifiers => activeTemporaryModifiers;
        
        // 伤害乘数（默认值为 1，表示不改变原始伤害）。
        private ModifiableStat damageMultiplier = new ModifiableStat(1);
        // 治疗乘数（默认值为 1，表示不改变原始治疗量）。
        private ModifiableStat healMultiplier = new ModifiableStat(1);
        // 玩家体重乘数（默认值为 1，表示不改变原始体重）。
        private ModifiableStat playerWeightMultiplier = new ModifiableStat(1);
        
        // 对外暴露伤害乘数。
        public ModifiableStat DamageMultiplier => damageMultiplier;
        // 对外暴露治疗乘数。
        public ModifiableStat HealMultiplier => healMultiplier;
        // 对外暴露体重乘数。
        public ModifiableStat WeightMultiplier => playerWeightMultiplier;
        
        // 在脚本唤醒时初始化三个乘数属性为默认值 1。
        private void Awake()
        {
            damageMultiplier = new ModifiableStat(1);
            healMultiplier = new ModifiableStat(1);
            playerWeightMultiplier = new ModifiableStat(1);
        }

        /// <summary>
        /// 添加一个带持续时间的临时修改器：若相同来源的修改器已存在则刷新其持续时间，否则新增。
        /// </summary>
        /// <param name="stat">要修改的属性对象。</param>
        /// <param name="modifier">施加的修改器。</param>
        /// <param name="duration">持续时间（秒）。</param>
        public void AddTemporaryModifier(ModifiableStat stat, StatModifier modifier, float duration)
        {
            float expirationTime = Time.time + duration;
            
            // Check if this specific modifier (by Source) is already active
            foreach (TemporaryModifierData modifierData in activeTemporaryModifiers)
            {
                if (modifierData.Modifier.Source != modifier.Source) 
                    continue;
                // Refresh the duration
                modifierData.ExpirationTime = expirationTime;
                return;
            }

            // Not found, add as a new temporary modifier
            stat.AddModifier(modifier);
            activeTemporaryModifiers.Add(new TemporaryModifierData 
            { 
                Stat = stat, 
                Modifier = modifier, 
                ExpirationTime = expirationTime 
            });
        }

        /// <summary>按来源移除所有匹配的临时修改器，并倒序遍历以安全删除。</summary>
        /// <param name="source">修改器来源对象。</param>
        public void RemoveModifierFromSource(object source)
        {
            // Iterate backwards to safely remove while iterating
            for (int i = activeTemporaryModifiers.Count - 1; i >= 0; i--)
            {
                if (activeTemporaryModifiers[i].Modifier.Source != source) 
                    continue;
                activeTemporaryModifiers[i].Stat.RemoveModifierFromSource(source);
                activeTemporaryModifiers.RemoveAt(i);
            }
        }
        
        // 每帧检查临时修改器是否到期，到期则移除对应修改器并清理列表。
        private void Update()
        {
            if (activeTemporaryModifiers.Count == 0) return;

            // Iterate backwards to check expirations
            for (int i = activeTemporaryModifiers.Count - 1; i >= 0; i--)
            {
                if (Time.time < activeTemporaryModifiers[i].ExpirationTime) 
                    continue;
                activeTemporaryModifiers[i].Stat.RemoveModifierFromSource(activeTemporaryModifiers[i].Modifier.Source);
                activeTemporaryModifiers.RemoveAt(i);
            }
        }
    }
}
