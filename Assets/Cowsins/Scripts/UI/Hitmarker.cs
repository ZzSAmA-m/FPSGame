// ============================================================================
// Hitmarker.cs —— 命中标记（Hitmarker）UI 组件。
// 当玩家命中敌人时调用 Play 播放命中提示：播放命中音效、触发命中动画，
// 并根据是否为爆头将标记颜色设为红色（爆头）或白色（普通命中）。
// ============================================================================
using UnityEngine;
using UnityEngine.UI;

namespace cowsins
{
    /// <summary>
    /// 命中标记：播放命中音效与动画，并按是否爆头显示不同颜色。
    /// </summary>
    public class Hitmarker : MonoBehaviour
    {

        [SerializeField] private AudioClip hitmarkerSoundEffect; // 命中时播放的音效

        [SerializeField] private Animator animator; // 用于播放命中动画的 Animator

        [SerializeField] private Image image; // 命中标记的 Image，用于改变颜色

        // 播放命中标记：播放音效与命中动画，并根据是否爆头设置颜色。
        public void Play(bool headshot)
        {
            SoundManager.Instance.PlaySound(hitmarkerSoundEffect, .08f, .15f, true); // 播放命中音效
            animator.SetTrigger("hit"); // 触发命中动画

            image.color = headshot ? new Color(1, 0, 0) : new Color(1, 1, 1); // 爆头为红色，普通命中为白色
        }
    }
}