using UnityEngine;
// ===========================================================================================
// Flashlight.cs —— 手电筒配件脚本
// 职责：定义武器“手电筒”配件，继承自 Attachment 基类。通过 AttachmentAction 切换手电筒
//       开关并播放开关音效，同时维护 turnedOn 状态供外部查询。
// ===========================================================================================
namespace cowsins
{
    /// <summary>
    /// 手电筒配件：继承 Attachment 基类，管理手电筒光源的开关、状态与音效。
    /// </summary>
    public class Flashlight : Attachment
    {
        // lightSource：手电筒的光源组件（Light）；turnOnSFX / turnOffSFX：开关时播放的音效
        [Title("FLASHLIGHT")]
        [Tooltip("Light for the flashlight"), SerializeField] private Light lightSource;
        [Tooltip("SFX for turning on and off."), SerializeField] private AudioClip turnOnSFX, turnOffSFX;

        // 当前手电筒是否处于开启状态
        private bool turnedOn;

        // 只读属性：返回手电筒当前开关状态
        public bool TurnedOn { get { return turnedOn; } }

        // 初始化：手电筒默认处于关闭状态
        private void Start()
        {
            turnedOn = false;
        }

        /// <summary>
        /// 配件主动动作：取反当前光源状态以切换开关，更新 turnedOn 状态，并播放对应开关音效。
        /// </summary>
        public override void AttachmentAction()
        {
            bool newLightState = !lightSource.gameObject.activeSelf;

            EnableFlashLight(newLightState);
            CheckIfCanTurnOn(newLightState);

            SoundManager.Instance.PlaySound(newLightState ? turnOnSFX : turnOffSFX, 0, 0, true);
        }

        // 根据传入条件更新 turnedOn 状态：cond 为 true 时置为关闭（false），否则置为开启（true）。
        private void CheckIfCanTurnOn(bool cond)
        {
            // Check if we can turn it on
            if (cond)
            {
                // Conditions are met, turn off
                turnedOn = false;
                return;
            }
            // Turn on
            turnedOn = true;
        }

        // 强制设置手电筒开关：cond 为 true 时点亮光源，否则关闭光源并把 turnedOn 置为 false。
        /// <summary>
        /// Forces the flashlight to turn on.
        /// </summary>
        /// <param name="cond"></param>
        private void EnableFlashLight(bool cond)
        {
            // If the condition is met, enable the flashlight light, if not, disable it and turn it off.
            if (cond) lightSource.gameObject.SetActive(true);
            else
            {
                turnedOn = false;
                lightSource.gameObject.SetActive(false);
            }
        }
    }
}