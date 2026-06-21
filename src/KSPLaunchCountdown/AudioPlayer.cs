/**
 * AudioPlayer.cs - KSP1 发射倒计时音频播放器
 *
 * 用途：负责加载和播放倒计时语音音频文件。
 * 使用KSP的GameDatabase加载.ogg格式音频，AudioSource组件播放。
 * 支持播放状态检测和播放结束回调，供倒计时控制器使用。
 *
 * 音频加载方式：
 *   使用KSP内置的GameDatabase.GetAudioClip()方法加载音频文件。
 *   该方法通过KSP的资源数据库加载音频，音频文件需放在GameData目录下，
 *   路径格式为 "模组名/目录/文件名"（不含扩展名）。
 *   例如：KSPLaunchCountdown/Lauch Voice/DFH-1/DFH-1
 *
 * 依赖：
 *   - Assembly-CSharp.dll (KSP核心，提供GameDatabase、KSPUtil等)
 *   - UnityEngine.CoreModule.dll (Unity核心，提供MonoBehaviour、AudioSource、协程等)
 *   - UnityEngine.AudioModule.dll (Unity音频模块，提供AudioClip等)
 */

using System;
using System.Collections;
using UnityEngine;

namespace KSPLaunchCountdown
{
    /// <summary>
    /// 音频播放器
    /// 挂载到GameObject上，提供音频加载、播放、停止和状态查询功能。
    /// 使用KSP的GameDatabase加载音频，通过协程等待播放完成。
    /// </summary>
    public class AudioPlayer : MonoBehaviour
    {
        /// <summary>日志标签</summary>
        private const string LOG_TAG = "[KSPLaunchCountdown]";

        /// <summary>AudioSource组件，用于播放音频</summary>
        private AudioSource audioSource;

        /// <summary>当前是否正在加载音频（预留状态标志，可用于UI显示加载状态）</summary>
#pragma warning disable CS0414
        private bool isLoading = false;
#pragma warning restore CS0414

        /// <summary>
        /// 音频播放结束回调
        /// 当倒计时音频播放完毕时触发，通知倒计时控制器执行下一步操作
        /// </summary>
        public event Action OnAudioFinished;

        /// <summary>
        /// 获取当前是否正在播放音频
        /// </summary>
        public bool IsPlaying
        {
            get
            {
                return audioSource != null && audioSource.isPlaying;
            }
        }

        /// <summary>
        /// Unity生命周期方法，在对象创建时初始化AudioSource组件
        /// </summary>
        void Awake()
        {
            // 创建AudioSource组件并配置为2D音频（无距离衰减）
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.spatialBlend = 0f;       // 0=2D音频，1=3D音频；倒计时语音应为2D
            audioSource.loop = false;             // 不循环播放
            audioSource.playOnAwake = false;      // 不自动播放
            audioSource.volume = 1.0f;            // 音量（0.0~1.0），可调整倒计时语音音量
        }

        /// <summary>
        /// 加载并播放指定的音频文件
        /// 使用KSP的GameDatabase加载音频，通过协程等待播放完成
        /// </summary>
        /// <param name="relativePath">
        /// 音频文件相对于GameData目录的路径（不含扩展名），
        /// 如 "KSPLaunchCountdown/Lauch Voice/DFH-1/DFH-1"
        /// </param>
        public void LoadAndPlay(string relativePath)
        {
            // 如果正在播放，先停止
            Stop();

            // 移除文件扩展名（GameDatabase使用无扩展名的路径）
            string dbPath = relativePath;
            if (dbPath.EndsWith(".ogg", StringComparison.OrdinalIgnoreCase))
            {
                dbPath = dbPath.Substring(0, dbPath.Length - 4);
            }

            Debug.Log($"{LOG_TAG} 开始加载音频: {dbPath}");

            // 启动协程加载和播放
            StartCoroutine(LoadAndPlayCoroutine(dbPath));
        }

        /// <summary>
        /// 异步加载和播放音频的协程
        /// 1. 使用GameDatabase加载AudioClip
        /// 2. 播放音频
        /// 3. 等待播放完成
        /// 4. 触发播放结束回调
        /// </summary>
        /// <param name="dbPath">GameDatabase音频资源路径（不含扩展名）</param>
        private IEnumerator LoadAndPlayCoroutine(string dbPath)
        {
            isLoading = true;

            // 使用KSP的GameDatabase加载音频
            // GameDatabase在游戏启动时已扫描GameData目录下的所有资源文件
            // 路径格式：模组名/目录/文件名（不含扩展名和GameData前缀）
            AudioClip clip = GameDatabase.Instance.GetAudioClip(dbPath);

            isLoading = false;

            if (clip == null)
            {
                Debug.LogError($"{LOG_TAG} 音频加载失败，路径: {dbPath}。" +
                    "请确认音频文件已放入GameData目录且路径正确");
                yield break;
            }

            Debug.Log($"{LOG_TAG} 音频加载成功: {clip.name}, 时长: {clip.length:F1}秒");

            // 设置AudioSource并播放
            audioSource.clip = clip;
            audioSource.Play();

            // 等待音频播放完毕
            // 每帧检查播放状态，直到播放结束或被停止
            while (audioSource.isPlaying)
            {
                yield return null;
            }

            // 音频播放结束，触发回调
            Debug.Log($"{LOG_TAG} 音频播放结束");
            OnAudioFinished?.Invoke();
        }

        /// <summary>
        /// 停止当前音频播放
        /// </summary>
        public void Stop()
        {
            // 停止AudioSource播放
            if (audioSource != null && audioSource.isPlaying)
            {
                audioSource.Stop();
                Debug.Log($"{LOG_TAG} 音频播放已停止");
            }

            isLoading = false;

            // 停止所有相关的协程
            StopAllCoroutines();
        }

        /// <summary>
        /// Unity生命周期方法，在对象销毁时清理资源
        /// </summary>
        void OnDestroy()
        {
            Stop();

            if (audioSource != null)
            {
                Destroy(audioSource);
                audioSource = null;
            }

            OnAudioFinished = null;
        }
    }
}
