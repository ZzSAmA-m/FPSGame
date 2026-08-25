// MainMenuManager（主菜单管理器）：
// 管理主菜单的多个分区（Sections）的显示切换与淡入过渡，
// 并提供切换场景 / 异步加载场景的接口。作为单例供菜单按钮调用。
using UnityEngine;
using UnityEngine.SceneManagement;

namespace cowsins
{
    /// <summary>
    /// 主菜单管理器：控制主菜单各分区的显示、淡入过渡以及场景加载。
    /// </summary>
    public class MainMenuManager : MonoBehaviour
    {
        [System.Serializable]
        // 主菜单分区：把分区名称与对应的 CanvasGroup 绑定在一起
        public class MainMenuSection
        {
            // 分区名称（用于在 Inspector 中标识）
            public string sectionName;
            // 分区对应的画布组，用于控制透明度和激活状态
            public CanvasGroup section;
        }
        // 全局单例实例
        public static MainMenuManager Instance { get; private set; }

        // 主菜单所有分区数组
        [SerializeField, Header("Sections")] private MainMenuSection[] mainMenuSections;

        // 当前正在进行淡入过渡的目标 CanvasGroup
        private CanvasGroup objectToLerp;

        // 菜单音频源
        private AudioSource audioSource;

        // 单例初始化：若已存在实例则销毁重复对象
        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else if (Instance != this)
            {
                Destroy(gameObject);
            }
        }

        // 初始显示第一个分区，隐藏其余分区
        private void Start()
        {
            mainMenuSections[0].section.gameObject.SetActive(true);
            mainMenuSections[0].section.alpha = 1;

            // We want to skip the first item
            for (int i = 1; i < mainMenuSections.Length; i++)
            {
                mainMenuSections[i].section.gameObject.SetActive(false);
                mainMenuSections[i].section.alpha = 0;
            }

            audioSource = GetComponent<AudioSource>();
        }

        // 每帧执行：对目标分区进行透明度淡入，直到完全不透明
        private void Update()
        {
            if (!objectToLerp || objectToLerp?.alpha >= 1) return;
            objectToLerp.gameObject.SetActive(true);
            objectToLerp.alpha += Time.deltaTime * 3;
        }


        // 设置需要淡入显示的目标分区
        public void SetObjectToLerp(CanvasGroup To) => objectToLerp = To;

        // 按场景索引同步加载场景
        public void ChangeScene(int scene) => SceneManager.LoadScene(scene);

        // 按场景索引异步加载场景（避免加载时卡顿）
        public void LoadScene(int sceneIndex)
        {
            SceneManager.LoadSceneAsync(sceneIndex);
        }
    }
}
