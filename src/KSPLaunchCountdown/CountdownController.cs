/**
 * CountdownController.cs - KSP1 发射倒计时控制器
 *
 * 用途：倒计时的核心控制逻辑，协调音频播放和发射序列的执行。
 * 按照预定的发射流程控制各步骤的执行顺序和时机：
 *   1. 隐藏游戏UI
 *   2. 开启SAS
 *   3. 设置满油门
 *   4. 播放倒计时音频
 *   5. 等待音频播放结束
 *   6. 分离一级并启动发动机
 *   7. 等待3秒
 *   8. 恢复游戏UI
 *
 * 使用Unity协程实现异步序列控制，确保各步骤按顺序执行。
 * 支持取消倒计时操作，取消时立即停止音频并恢复UI。
 *
 * 兼容性说明：
 *   由于精简版Assembly-CSharp.dll缺少UIMasterController等类型定义，
 *   本类通过KSPApiHelper反射辅助类调用KSP API，确保编译兼容性。
 *
 * 依赖：
 *   - Assembly-CSharp.dll (KSP核心，提供UIMasterController、FlightGlobals等)
 *   - UnityEngine.CoreModule.dll (Unity核心，提供MonoBehaviour、协程等)
 *   - KSPApiHelper.cs (KSP API反射辅助类)
 */

using System.Collections;
using UnityEngine;

namespace KSPLaunchCountdown
{
    /// <summary>
    /// 倒计时控制器
    /// 协调AudioPlayer和LaunchSequence，按序列执行倒计时发射流程
    /// </summary>
    public class CountdownController : MonoBehaviour
    {
        /// <summary>日志标签</summary>
        private const string LOG_TAG = "[KSPLaunchCountdown]";

        /// <summary>
        /// 分级后等待恢复UI的延迟时间（秒）
        /// 可调整参数：增大此值让玩家有更多时间观察发射效果，
        /// 减小此值让UI更快恢复便于操作
        /// </summary>
        private const float UI_RESTORE_DELAY = 3.0f;

        /// <summary>音频播放器引用</summary>
        private AudioPlayer audioPlayer;

        /// <summary>发射序列执行器引用</summary>
        private LaunchSequence launchSequence;

        /// <summary>当前是否正在执行倒计时序列</summary>
        private bool isCountingDown = false;

        /// <summary>当前是否被取消</summary>
        private bool isCancelled = false;

        /// <summary>UI在倒计时开始前是否已经被隐藏</summary>
        private bool uiWasHiddenBefore = false;

        /// <summary>
        /// 倒计时状态变化回调
        /// 参数：true=倒计时开始，false=倒计时结束或取消
        /// </summary>
        public event System.Action<bool> OnCountdownStateChanged;

        /// <summary>
        /// 获取当前是否正在执行倒计时
        /// </summary>
        public bool IsCountingDown => isCountingDown;

        /// <summary>
        /// 初始化控制器，注入依赖的音频播放器和发射序列执行器
        /// </summary>
        /// <param name="player">音频播放器实例</param>
        /// <param name="sequence">发射序列执行器实例</param>
        public void Initialize(AudioPlayer player, LaunchSequence sequence)
        {
            audioPlayer = player;
            launchSequence = sequence;

            // 注册音频播放结束事件
            audioPlayer.OnAudioFinished += OnAudioPlaybackFinished;
        }

        /// <summary>
        /// 开始倒计时序列
        /// 按顺序执行：隐藏UI → 开SAS → 满油门 → 播放音频 → 等待结束 → 分级 → 延迟 → 恢复UI
        /// </summary>
        /// <param name="presetName">选择的预设名称，如 "DFH-1"</param>
        /// <param name="audioRelativePath">预设音频文件路径（GameDatabase格式，不含扩展名）</param>
        public void StartCountdown(string presetName, string audioRelativePath)
        {
            if (isCountingDown)
            {
                Debug.LogWarning($"{LOG_TAG} 倒计时已在进行中，忽略重复请求");
                return;
            }

            // 检查是否在飞行场景（HighLogic在精简DLL中存在，可直接引用）
            if (!HighLogic.LoadedSceneIsFlight || FlightGlobals.ActiveVessel == null)
            {
                Debug.LogWarning($"{LOG_TAG} 无法开始倒计时：当前不在飞行场景或无活跃飞船");
                return;
            }

            isCountingDown = true;
            isCancelled = false;

            Debug.Log($"{LOG_TAG} 开始倒计时序列，预设: {presetName}");

            // 通知状态变化
            OnCountdownStateChanged?.Invoke(true);

            // 启动倒计时协程
            StartCoroutine(CountdownCoroutine(presetName, audioRelativePath));
        }

        /// <summary>
        /// 取消倒计时
        /// 立即停止音频播放，恢复UI，释放油门保持
        /// </summary>
        public void CancelCountdown()
        {
            if (!isCountingDown)
            {
                return;
            }

            Debug.Log($"{LOG_TAG} 取消倒计时");
            isCancelled = true;

            // 停止音频播放
            if (audioPlayer != null)
            {
                audioPlayer.Stop();
            }

            // 释放油门保持
            if (launchSequence != null)
            {
                launchSequence.ReleaseThrottleHold();
            }

            // 恢复UI
            RestoreUI();

            isCountingDown = false;
            OnCountdownStateChanged?.Invoke(false);
        }

        /// <summary>
        /// 倒计时序列协程
        /// 按顺序执行各步骤，使用yield return等待异步操作完成
        /// </summary>
        private IEnumerator CountdownCoroutine(string presetName, string audioRelativePath)
        {
            // 步骤1：隐藏游戏UI
            HideUI();
            yield return null;  // 等待一帧确保UI隐藏生效

            if (isCancelled) yield break;

            // 步骤2：开启SAS
            launchSequence.EnableSAS();
            yield return null;

            if (isCancelled) yield break;

            // 步骤3：设置满油门
            launchSequence.SetFullThrottle();
            yield return null;

            if (isCancelled) yield break;

            // 步骤4：播放倒计时音频
            audioPlayer.LoadAndPlay(audioRelativePath);

            // 步骤5：等待音频播放结束
            // 先等待一帧确保AudioSource开始播放
            yield return null;

            while (audioPlayer.IsPlaying && !isCancelled)
            {
                yield return null;
            }

            if (isCancelled) yield break;

            // 步骤6：音频播放结束，立即分离一级并启动发动机
            Debug.Log($"{LOG_TAG} 倒计时音频播放结束，执行分级");
            launchSequence.ActivateNextStage();

            // 步骤7：等待3秒后恢复UI
            // 可调整参数：UI_RESTORE_DELAY
            float elapsed = 0f;
            while (elapsed < UI_RESTORE_DELAY && !isCancelled)
            {
                elapsed += UnityEngine.Time.deltaTime;
                yield return null;
            }

            // 步骤8：恢复UI
            RestoreUI();

            // 释放油门保持（发射后允许玩家手动控制油门）
            launchSequence.ReleaseThrottleHold();

            isCountingDown = false;
            OnCountdownStateChanged?.Invoke(false);

            Debug.Log($"{LOG_TAG} 倒计时序列完成");
        }

        /// <summary>
        /// 音频播放结束回调
        /// 由AudioPlayer的OnAudioFinished事件触发
        /// 注意：实际等待逻辑在协程中通过轮询IsPlaying实现，
        /// 此回调作为备用通知机制
        /// </summary>
        private void OnAudioPlaybackFinished()
        {
            Debug.Log($"{LOG_TAG} 收到音频播放结束通知");
        }

        /// <summary>
        /// 隐藏游戏UI
        /// 使用KSPApiHelper反射调用UIMasterController.Hide()
        /// </summary>
        private void HideUI()
        {
            // 保存UI之前的隐藏状态，以便恢复时正确处理
            uiWasHiddenBefore = KSPApiHelper.IsUIHidden();
            KSPApiHelper.HideUI();
            Debug.Log($"{LOG_TAG} 游戏UI已隐藏");
        }

        /// <summary>
        /// 恢复游戏UI
        /// 仅在UI不是之前就被隐藏的情况下恢复
        /// </summary>
        private void RestoreUI()
        {
            if (!uiWasHiddenBefore)
            {
                KSPApiHelper.ShowUI();
                Debug.Log($"{LOG_TAG} 游戏UI已恢复");
            }
        }

        /// <summary>
        /// Unity生命周期方法，在对象销毁时清理
        /// </summary>
        void OnDestroy()
        {
            // 取消正在进行的倒计时
            if (isCountingDown)
            {
                CancelCountdown();
            }

            // 注销音频事件
            if (audioPlayer != null)
            {
                audioPlayer.OnAudioFinished -= OnAudioPlaybackFinished;
            }

            // 停止所有协程
            StopAllCoroutines();
        }
    }
}
