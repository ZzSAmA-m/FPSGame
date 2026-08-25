// QuickActionBehaviour.cs：武器快速动作（近战）行为。
// 处理副近战攻击（如刀/剑挥砍）：激活近战物体、延迟触发伤害判定、播放音效与镜头抖动，
// 并在动作结束后延迟重新启用近战能力。
using UnityEngine;
using System.Collections;

namespace cowsins
{
    /// <summary>
    /// 武器快速动作行为：实现近战攻击的触发、球形伤害判定、音效/镜头抖动以及攻击后的冷却恢复。
    /// </summary>
    public class QuickActionBehaviour
    {
        // 武器上下文。
        private WeaponContext context;
        // 输入管理器（本系统内暂未直接使用）。
        private InputManager inputManager;
        // 武器行为提供者，提供近战可用状态等。
        private IWeaponBehaviourProvider weaponBehaviour;
        // 武器引用提供者，提供当前武器与实例标识。
        private IWeaponReferenceProvider weaponReference;
        // 玩家移动状态提供者，用于判断是否在攀爬等。
        private IPlayerMovementStateProvider playerMovement;
        // 玩家控制提供者，用于判断当前是否可操控。
        private IPlayerControlProvider playerControl;
        // 武器事件提供者，用于触发近战/射击抖动等事件。
        private IWeaponEventsProvider weaponEvents;
        // 玩家乘数集合，用于获取伤害乘数。
        private IPlayerMultipliers playerMultipliers;

        // 当前武器数据快捷属性。
        private Weapon_SO weapon => weaponReference.Weapon;
        // 当前武器实例标识快捷属性。
        private WeaponIdentification id => weaponReference.Id;
        // 主相机快捷属性。
        private Camera mainCamera => weaponReference.MainCamera;
        // 武器挂载点快捷属性。
        private Transform weaponHolder => context.WeaponHolder;

        // 武器控制器设置，含近战物体、伤害、冷却等配置。
        private WeaponControllerSettings settings;

        // 近战延迟触发伤害的协程句柄。
        private Coroutine meleeCoroutine;
        // 延迟重新启用近战的协程句柄。
        private Coroutine reEnableMeleeCoroutine;

        // 协程执行载体（MonoBehaviour）。
        private MonoBehaviour CoroutineRunner => context.CoroutineRunner;

        /// <summary>
        /// 构造函数：缓存依赖引用与设置，并将近战能力初始化为可用。
        /// </summary>
        /// <param name="context">武器上下文。</param>
        public QuickActionBehaviour(WeaponContext context)
        {
            this.context = context;
            this.inputManager = context.InputManager;
            this.weaponBehaviour = context.Dependencies.WeaponBehaviour;
            this.weaponReference = context.Dependencies.WeaponReference;
            this.playerMovement = context.Dependencies.PlayerMovementState;
            this.playerControl = context.Dependencies.PlayerControl;
            this.weaponEvents = context.Dependencies.WeaponEvents;
            this.playerMultipliers = context.Dependencies.PlayerMultipliers;

            this.settings = context.Settings;
            weaponBehaviour.IsMeleeAvailable = true;
        }

        /// <summary>
        /// 判断当前是否可执行近战：需玩家可控、设置启用近战、近战可用且不在攀爬状态。
        /// </summary>
        public bool CanExecute()
        {
            return playerControl.IsControllable && settings.canMelee && weaponBehaviour.IsMeleeAvailable && !playerMovement.IsClimbing;
        }

        /// <summary>
        /// 触发副近战攻击：禁用近战、激活近战物体、启动延迟触发协程、触发副攻击事件并播放近战音效。
        /// </summary>
        public void SecondaryMeleeAttack()
        {
            weaponBehaviour.IsMeleeAvailable = false;
            settings.meleeObject.SetActive(true);

            if (meleeCoroutine != null) CoroutineRunner.StopCoroutine(meleeCoroutine);
            meleeCoroutine = CoroutineRunner.StartCoroutine(MeleeRoutine());

            weaponEvents.Events.OnSecondaryAttack?.Invoke(settings.meleeHeadBone ?? null);

            // Play melee audio
            if (settings.meleeAudioClip != null && context.AudioSource != null)
            {
                context.AudioSource.PlayOneShot(settings.meleeAudioClip);
            }
        }

        // 近战协程：等待设置的近战延迟后执行真正的近战伤害判定。
        private IEnumerator MeleeRoutine()
        {
            yield return new WaitForSeconds(settings.meleeDelay);
            Melee();
        }

        // 执行近战：触发用户事件、进行球形伤害判定，并按瞄准/蹲伏倍率施加镜头抖动。
        private void Melee()
        {
            settings.userEvents.OnSecondaryMelee?.Invoke();
            MeleeAttack(settings.meleeRange, settings.meleeAttackDamage);
            weaponEvents.Events.OnShootShake?.Invoke(settings.meleeCamShakeAmount * weaponBehaviour.AimingCamShakeMultiplier * weaponBehaviour.CrouchingCamShakeMultiplier);
        }

        /// <summary>
        /// 结束近战动作：隐藏近战物体，并启动延迟协程在冷却后重新启用近战能力。
        /// </summary>
        public void FinishMelee()
        {
            settings.meleeObject.SetActive(false);

            if (reEnableMeleeCoroutine != null) CoroutineRunner.StopCoroutine(reEnableMeleeCoroutine);
            reEnableMeleeCoroutine = CoroutineRunner.StartCoroutine(ReEnableMeleeRoutine());
        }

        // 重新启用近战协程：等待设定的冷却时间后将近战能力置为可用。
        private IEnumerator ReEnableMeleeRoutine()
        {
            yield return new WaitForSeconds(settings.reEnableMeleeAfterAction);
            weaponBehaviour.IsMeleeAvailable = true;
        }

        // 近战伤害判定：以攻击点为中心的球形检测命中目标并施加伤害（优先暴击/身体标签，其次 IDamageable），随后以射线触发命中特效。
        /// <summary>
        /// Moreover, cowsins� FPS ENGINE also supports melee attacking
        /// Use this for Swords, knives etc
        /// </summary>
        private void MeleeAttack(float attackRange, float damage)
        {
            RaycastHit hit;
            Vector3 basePosition = id != null ? id.transform.position : context.Transform.position;
            Collider[] col = Physics.OverlapSphere(basePosition + mainCamera.transform.parent.forward * attackRange / 2, attackRange, settings.hitLayer);

            float dmg = damage * playerMultipliers.DamageMultiplier.Value;

            foreach (var c in col)
            {
                if (c.CompareTag("Critical") || c.CompareTag("BodyShot"))
                {
                    CowsinsUtilities.GatherDamageableParent(c.transform)?.Damage(dmg, false);
                    break;
                }

                if (c.TryGetComponent<IDamageable>(out var damageable))
                {
                    damageable.Damage(dmg, false);
                    break;
                }
            }

            //VISUALS
            Ray ray = new Ray(mainCamera.transform.position, mainCamera.transform.forward);
            if (Physics.Raycast(ray, out hit, attackRange, settings.hitLayer))
            {
                weaponEvents.Events.OnHit?.Invoke(hit.collider.gameObject.layer, 0f, hit, false);
            }
        }
    }

}
