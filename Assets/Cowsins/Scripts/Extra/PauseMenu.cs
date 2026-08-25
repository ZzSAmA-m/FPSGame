// PauseMenu（暂停菜单）：
// 管理游戏暂停/恢复逻辑：切换暂停状态时接管/归还玩家控制权，并通过协程实现菜单淡入淡出，
// 同时支持暂停/恢复事件回调与退出游戏。作为单例，由输入系统（InputManager）触发 TogglePause。
using System;
using UnityEngine;
using System.Collections;
using UnityEngine.UI;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace cowsins
{
    /// <summary>
    /// 暂停菜单：控制游戏暂停/恢复、菜单淡入淡出及玩家控制权切换。
    /// </summary>
    public class PauseMenu : MonoBehaviour
    {
        // 玩家依赖集合，用于获取控制权、状态与输入管理器的引用
        [SerializeField] private PlayerDependencies playerDependencies;
        // 玩家 HUD（暂停时可选隐藏）
        [SerializeField] private GameObject playerUI;
        // 暂停菜单打开时默认选中的 UI 元素（用于手柄/键盘导航）
        [SerializeField] private Selectable firstSelectedItem;
        // 暂停时是否隐藏玩家 HUD
        [SerializeField] private bool disablePlayerUIWhilePaused;
        // 暂停菜单的 CanvasGroup，用于控制淡入淡出
        [SerializeField] private CanvasGroup menu;
        // 淡入淡出速度
        [SerializeField] private float fadeSpeed;
        // 暂停与恢复时触发的事件回调
        [SerializeField] private UnityEvent onPauseEvents, onUnpauseEvents;
        // 当前正在执行的淡入淡出协程
        private Coroutine fadeCoroutine;

        private IPlayerControlProvider playerControlProvider; // IPlayerControlProvider is implemented in PlayerControl.cs
        private IPlayerStatsProvider playerStatsProvider; // IPlayerStatsProvider is implemented in PlayerStats.cs
        private InputManager inputManager;

        // 暂停菜单单例
        public static PauseMenu Instance { get; private set; }

        /// <summary>
        /// Returns the Pause State of the game
        /// </summary>
        public static bool isPaused { get; private set; }

        // 暂停时触发的事件
        public event Action OnPause;
        // 恢复时触发的事件
        public event Action OnUnPause;

        // 初始化引用、单例设置，并隐藏菜单；缺少 EventSystem 时打印错误提示
        private void Start()
        {
            playerControlProvider = playerDependencies.PlayerControl;
            playerStatsProvider = playerDependencies.PlayerStats;
            inputManager = playerDependencies.InputManager;

            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            // Initially, the game is not paused
            isPaused = false;
            menu.gameObject.SetActive(false);
            menu.alpha = 0;

            if (EventSystem.current == null)
                CowsinsUtilities.LogError("No <b><color=cyan>EventSystem</color></b> object found in the scene. " +
                    "Please create a new Empty GameObject and assign the EventSystem component to it to fix this error.");
        }

        // 启用时订阅输入的暂停切换事件
        private void OnEnable()
        {
            if (inputManager == null && playerDependencies != null) inputManager = playerDependencies.InputManager;
            if (inputManager != null)
                inputManager.OnTogglePause += TogglePause;
        }

        // 禁用时取消订阅，避免重复触发或空引用
        private void OnDisable()
        {
            if (inputManager != null)
                inputManager.OnTogglePause -= TogglePause;
        }

        // 暂停协程：按需隐藏 HUD，并逐帧把菜单透明度淡入到 1
        private IEnumerator HandlePause()
        {
            if (disablePlayerUIWhilePaused && !playerStatsProvider.IsDead)
                playerUI.SetActive(false);

            menu.gameObject.SetActive(true);
            while (menu.alpha < 1)
            {
                menu.alpha += Time.deltaTime * fadeSpeed;
                yield return null;
            }
            menu.alpha = 1;
        }

        // 恢复协程：恢复 HUD，并逐帧把菜单透明度淡出到 0 后隐藏
        private IEnumerator HandleUnpause()
        {
            playerUI.SetActive(true);
            while (menu.alpha > 0)
            {
                menu.alpha -= Time.deltaTime * fadeSpeed;
                yield return null;
            }
            menu.alpha = 0;
            menu.gameObject.SetActive(false);
        }

        // 切换暂停状态：暂停时接管控制、淡入菜单并触发事件；否则执行恢复
        public void TogglePause()
        {
            isPaused = !isPaused;

            // 停止上一次未完成的淡入淡出协程
            if (fadeCoroutine != null) StopCoroutine(fadeCoroutine);

            if (isPaused)
            {
                playerControlProvider.LoseControl();
                fadeCoroutine = StartCoroutine(HandlePause());
                OnPause?.Invoke();
                onPauseEvents?.Invoke();
                // 若有手柄连接，则把导航焦点设置到默认选中项
                if(Gamepad.current !=  null)
                    EventSystem.current?.SetSelectedGameObject(firstSelectedItem.gameObject);
            }
            else
                UnPause();
        }

        // 恢复游戏：归还控制权、淡出菜单并触发恢复事件
        public void UnPause()
        {
            isPaused = false;
            playerControlProvider.CheckIfCanGrantControl();
            if (fadeCoroutine != null) StopCoroutine(fadeCoroutine);
            fadeCoroutine = StartCoroutine(HandleUnpause());
            OnUnPause?.Invoke();
            onUnpauseEvents?.Invoke();
        }

        // 退出游戏
        public void QuitGame()
        {
            Application.Quit();
        }
    }
}
