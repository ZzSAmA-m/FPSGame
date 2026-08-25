// CowsinsUtilities（cowsins 通用工具类）：
// 提供引擎内广泛使用的静态工具方法，涵盖子弹散布方向计算、动画播放、预设保存（编辑器）、
// 字符串判断、父级可伤害组件查找、楼层判定、附件兼容性检查、按图层查询音效/特效、子弹类型匹配，
// 以及统一格式的日志输出等功能。
using System;
using UnityEngine;
using System.Collections.Generic;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Presets;
using UnityEditor.Animations;
#endif
using System.IO;

namespace cowsins
{
    /// <summary>
    /// cowsins 通用工具类：提供散布、动画、预设、判定、查询与日志等静态辅助方法。
    /// </summary>
    public static class CowsinsUtilities
    {
        /// <summary>
        /// Returns a Vector3 that applies spread to the bullets shot
        /// </summary>
        public static Vector3 GetSpreadDirection(float amount, Camera camera)
        {
            float horSpread = UnityEngine.Random.Range(-amount, amount);
            float verSpread = UnityEngine.Random.Range(-amount, amount);
            Vector3 spread = camera.transform.InverseTransformDirection(new Vector3(horSpread, verSpread, 0));
            Vector3 dir = camera.transform.forward + spread;

            return dir;
        }
        // 在指定 Animator 上触发一个动画（通过 SetTrigger）
        public static void PlayAnim(string anim, Animator animator)
        {
            animator.SetTrigger(anim);
        }

        // 强制立即播放指定动画（跳过过渡，直接从第 0 帧播放）
        public static void ForcePlayAnim(string anim, Animator animator)
        {
            animator.Play(anim, 0, 0);
        }
        // 通过把布尔参数设为 true 来启动一个循环动画
        public static void StartAnim(string anim, Animator animated) => animated.SetBool(anim, true);

        // 通过把布尔参数设为 false 来停止一个循环动画
        public static void StopAnim(string anim, Animator animated) => animated.SetBool(anim, false);
#if UNITY_EDITOR
        // 把指定对象保存为预设资产（存放到 Assets/Cowsins/CowsinsPresets/ 目录）
        public static void SavePreset(UnityEngine.Object source, string name)
        {
            if (EmptyString(name))
            {
                Debug.LogError("ERROR: Do not forget to give your preset a name!");
                return;
            }
            Preset preset = new Preset(source);

            string directoryPath = "Assets/" + "Cowsins/" + "CowsinsPresets/";

            if (!Directory.Exists(directoryPath)) Directory.CreateDirectory(directoryPath);

            string fullPath = directoryPath + name + ".preset";
            AssetDatabase.CreateAsset(preset, fullPath);
            Debug.Log($"Preset successfully saved in {fullPath}");
        }
        // 把预设应用到目标对象上
        public static void ApplyPreset(Preset preset, UnityEngine.Object target)
        {
            preset.ApplyTo(target);
        }

        // 判断当前是否使用 Unity 6（按版本号以 "6" 开头判断）
        public static bool IsUsingUnity6()
        {
            string unityVersion = Application.unityVersion;
            return unityVersion.StartsWith("6"); 
        }

#endif
        // 判断字符串是否为空或仅含空格（注意：不判定 null，传入 null 会抛异常）
        public static bool EmptyString(string string_)
        {
            if (string_.Length == 0) return true;
            int i = 0;
            while (i < string_.Length)
            {
                if (string_[i].ToString() == " ") return true;
                i++;
            }
            return false;
        }

        // 从子物体向上遍历父级，找到第一个实现了 IDamageable 接口的组件
        public static IDamageable GatherDamageableParent(Transform child)
        {
            for (Transform parent = child.parent; parent != null; parent = parent.parent)
            {
                if (parent.TryGetComponent(out IDamageable component))
                {
                    return component;
                }
            }
            return null;
        }

        // 依次调用委托的每个订阅者，全部返回 true 才返回 true；委托为空时返回默认值
        public static bool InvokeFunc(Func<bool> del, bool defaultValue = true)
        {
            if (del == null)
                return defaultValue;

            foreach (Func<bool> func in del.GetInvocationList())
            {
                if (!func())
                    return false;
            }
            return true;
        }

        /// <summary>
        /// Determine wether this is determined as floor or not
        /// </summary>
        public static bool IsFloor(Vector3 v, float maxSlopeAngle)
        {
            float currentFloorAngle = Vector3.Angle(Vector3.up, v);
            return currentFloorAngle < maxSlopeAngle;
        }

        /// <summary>
        /// Checks if the attachment is compatible with the current unholstered weapon
        /// </summary>
        /// <param name="weapon">Weapon to check compatibility</param>
        /// <returns></returns>
        public static (bool found, Attachment attachment, int index) CompatibleAttachment(WeaponIdentification weaponIdentification, AttachmentIdentifier_SO identifier)
        {
            if (weaponIdentification == null) return (false, null, -1);

            Weapon_SO weapon = weaponIdentification.weapon;

            if (weapon?.weaponObject == null || identifier == null)
                return (false, null, -1);

            var compatible = weaponIdentification.compatibleAttachments;

            foreach (AttachmentType type in System.Enum.GetValues(typeof(AttachmentType)))
            {
                IReadOnlyList<Attachment> attachments = compatible.GetCompatible(type);

                for (int i = 0; i < attachments.Count; i++)
                {
                    if (attachments[i]?.attachmentIdentifier == identifier)
                        return (true, attachments[i], i);
                }
            }

            return (false, null, -1);
        }


#if UNITY_EDITOR
        // 在 AnimatorController 中查找指定状态对应的 AnimationClip 并返回其长度（编辑器专用）
        public static (bool, float) CheckClipAvailability(Animator animator, string stateName)
        {
            AnimatorController controller = animator.runtimeAnimatorController as AnimatorController;
            if (controller == null) return (false, 0f);

            foreach (var layer in controller.layers)
            {
                foreach (var childState in layer.stateMachine.states)
                {
                    if (childState.state.name == stateName)
                    {
                        AnimationClip clip = childState.state.motion as AnimationClip;
                        if (clip != null)
                        {
                            return (true, clip.length);
                        }
                        return (false, 0f);
                    }
                }
            }

            return (false, 0f);
        }
#endif
        // 按图层索引查找对应的脚步声数组；先匹配动态表项，未命中则返回默认脚步声
        public static AudioClip[] GetSoundsForLayer(this PlayerMovementSettings.FootStepsSounds footsteps, int layer)
        {
            // Check dynamic entries first
            foreach (var entry in footsteps.surfaceSounds)
            {
                if (entry.cachedLayerIndex == -1)
                    entry.cachedLayerIndex = LayerMask.NameToLayer(entry.layerName);

                if (entry.cachedLayerIndex == layer && entry.sounds.Length > 0)
                    return entry.sounds;
            }

            // Fallback to default
            return footsteps.defaultStep;
        }

        // 按图层索引查找对应的命中特效；未命中则返回默认特效
        public static GameObject GetImpactForLayer(this WeaponControllerSettings.ImpactEffects impactEffects, int layer)
        {
            foreach (var entry in impactEffects.impacts)
            {
                if (entry.cachedLayerIndex == -1)
                    entry.cachedLayerIndex = LayerMask.NameToLayer(entry.layerName);

                if (entry.cachedLayerIndex == layer && entry.impact != null)
                    return entry.impact;
            }

            return impactEffects.defaultImpact;
        }
        // 按图层索引查找对应的弹孔预制体；未命中则返回默认弹孔
        public static GameObject GetBulletHoleForLayer(this BulletHoleImpact bulletHoles, int layer)
        {
            foreach (var entry in bulletHoles.bulletHoleImpact)
            {
                entry.cachedLayerIndex = LayerMask.NameToLayer(entry.layerName);

                if (entry.cachedLayerIndex == layer && entry.bulletHoleImpact != null)
                    return entry.bulletHoleImpact;
            }

            return bulletHoles.defaultImpact;
        }

        // 判断两种子弹类型是否匹配（任一方为“通用”类型时按规则判定）
        public static bool MatchesBulletType(BulletTypeIdentifier_SO a, BulletTypeIdentifier_SO b)
        {
            bool IsUniversal(BulletTypeIdentifier_SO id) =>
                id == null || id.bulletType == BulletTypeIdentifier_SO.BulletType.Universal;

            // If both are universal -> valid.
            if (IsUniversal(a) && IsUniversal(b))
                return true;

            // If only one is universal -> Not valid.
            if (IsUniversal(a) || IsUniversal(b))
                return false;

            // Both are non-universal -> They must match exactly.
            return a == b;
        }

        // 日志统一前缀（红色 COWSINS 标签）
        private const string logPrefix = "<color=red>[COWSINS]</color>";
        // 根据日志类型调用相应的 Debug 方法，并统一加上前缀与格式化
        private static void Log(LogType type, string message, UnityEngine.Object context = null)
        {
            switch (type)
            {
                case LogType.Warning:
                    if (context == null) Debug.LogWarning(Format(message));
                    else Debug.LogWarning(Format(message), context);
                    break;

                case LogType.Error:
                    if (context == null) Debug.LogError(Format(message));
                    else Debug.LogError(Format(message), context);
                    break;

                case LogType.Exception:
                    if (context == null) Debug.LogError(Format(message));
                    else Debug.LogError(Format(message), context);
                    break;
            }
        }
        // 给消息加上统一前缀
        private static string Format(string message) => $"{logPrefix} {message}";

        // 输出带前缀的警告日志
        public static void LogWarning(string message, UnityEngine.Object context = null)
            => Log(LogType.Warning, message, context);

        // 输出带前缀的错误日志
        public static void LogError(string message, UnityEngine.Object context = null)
            => Log(LogType.Error, message, context);

        // 输出带前缀与格式化参数的错误日志
        public static void LogErrorFormat(string message, params object[] args)
            => Debug.LogErrorFormat(Format(message), args);
    }
}
