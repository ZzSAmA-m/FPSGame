// 文件：SoundManager.cs
// 作用：音效管理器：提供全局播放音效的能力，支持 2D（非定位）与 3D（空间定位）两种播放方式，
//       并支持延迟播放与随机音调变化。3D 音效复用 PoolManager 的对象池来管理 AudioSource。
using UnityEngine;
using System.Collections;
namespace cowsins
{
    /// <summary>
    /// 音效管理器：负责播放 2D/3D 音效，并利用对象池管理 3D 音源的创建与回收。
    /// </summary>
    public class SoundManager : MonoBehaviour
    {
        // 单例实例。
        public static SoundManager Instance;

        // 用于 3D 空间音效的 AudioSource 模板（会被注册进对象池）。
        [SerializeField] private AudioSource source3D;

        // 用于 2D 非定位音效的本地 AudioSource。
        private AudioSource src;
        // 初始化单例，并配置 2D 音源与 3D 音源对象池。
        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                transform.SetParent(null);
            }
            else Destroy(this.gameObject);

            // 取本地 AudioSource 并设为 2D（spatialBlend=0 表示无空间衰减）。
            src = GetComponent<AudioSource>();
            src.spatialBlend = 0f;

            // 将 3D 音源模板注册进对象池（容量 5），供 3D 音效复用。
            if (source3D != null && PoolManager.Instance != null) PoolManager.Instance.RegisterPool(source3D.gameObject, 5);
        }

        /// <summary>
        /// 播放一个 2D（非定位）音效。
        /// </summary>
        /// <param name="clip">要播放的音频片段。</param>
        /// <param name="delay">延迟播放时间（秒）。</param>
        /// <param name="pitchAdded">音调增量（randomPitch 为 true 时作为随机范围）。</param>
        /// <param name="randomPitch">是否在 [-pitchAdded, pitchAdded] 范围内随机音调。</param>
        public void PlaySound(AudioClip clip, float delay, float pitchAdded, bool randomPitch)
        {
            StartCoroutine(Play(clip, delay, pitchAdded, randomPitch));
        }

        /// <summary>
        /// 在指定位置播放一个 3D（空间定位）音效。
        /// </summary>
        /// <param name="clip">要播放的音频片段。</param>
        /// <param name="position">播放位置。</param>
        /// <param name="delay">延迟播放时间（秒）。</param>
        /// <param name="pitchAdded">音调增量（randomPitch 为 true 时作为随机范围）。</param>
        /// <param name="randomPitch">是否随机音调。</param>
        public void PlaySoundAtPosition(AudioClip clip, Vector3 position, float delay, float pitchAdded, bool randomPitch)
        {
            StartCoroutine(PlayAtPosition(clip, position, delay, pitchAdded, randomPitch));
        }

        // 协程：延迟后以指定/随机音调播放 2D 音效（PlayOneShot）。
        private IEnumerator Play(AudioClip clip, float delay, float pitch, bool randomPitch)
        {
            if (clip == null) yield break;
            yield return new WaitForSeconds(delay);
            float pitchAdded = randomPitch ? Random.Range(-pitch, pitch) : pitch;
            src.pitch = 1 + pitchAdded;
            src.PlayOneShot(clip);
            yield return null;
        }

        // 协程：延迟后从对象池取一个 3D 音源，在指定位置播放音效，播完后归还到池。
        private IEnumerator PlayAtPosition(AudioClip clip, Vector3 position, float delay, float pitch, bool randomPitch)
        {
            if (clip == null) yield break;
            yield return new WaitForSeconds(delay);

            AudioSource newSource = PoolManager.Instance.GetFromPool(source3D.gameObject, position, Quaternion.identity).GetComponent<AudioSource>();
            newSource.spatialBlend = 1f;
            newSource.volume = 1f;

            float pitchAdded = randomPitch ? Random.Range(-pitch, pitch) : pitch;
            newSource.pitch = 1 + pitchAdded;

            newSource.clip = clip;
            newSource.Play();

            yield return new WaitForSeconds(clip.length / newSource.pitch);
            PoolManager.Instance.ReturnToPool(newSource.gameObject, source3D.gameObject);
        }
    }
}

