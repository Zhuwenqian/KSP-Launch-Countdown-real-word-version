/**
 * CountdownController.cs - KSP1 发射倒计时控制器
 *
 * 用途：倒计时的核心控制逻辑，协调音频播放和发射序列的执行。
 * 支持单段和多段音频模式、"先启动发动机再分离"功能，
 * 以及根据发射前安全检查结果的"先点火后放行"策略。
 *
 * 发射流程（单段音频模式）：
 *   1. 隐藏游戏UI
 *   2. 开启SAS
 *   3. 设置满油门
 *   4. 播放倒计时音频
 *   5. 等待音频播放结束
 *   6. 执行分级（分离+启动发动机）
 *   7. [若启用先启动发动机] 等待延迟后第二次分级
 *   8. 等待3秒
 *   9. 恢复游戏UI
 *
 * 发射流程（多段音频模式 p1/p2）：
 *   1. 隐藏游戏UI
 *   2. 开启SAS
 *   3. 设置满油门
 *   4. 播放p1音频（倒计时部分）
 *   5. 等待p1播放结束
 *   6. 执行分级（分离+启动发动机）
 *   7. 播放p2音频（点火后部分）
 *   8. [若启用先启动发动机] p2开始后等待延迟执行第二次分级
 *   9. 等待p2播放结束
 *   10. 等待3秒
 *   11. 恢复游戏UI
 *
 * 依赖：
 *   - Assembly-CSharp.dll (KSP核心)
 *   - UnityEngine.CoreModule.dll (Unity核心)
 *   - KSPApiHelper.cs (KSP API反射辅助类)
 *   - Localization.cs (多语言支持)
 *   - LaunchSafetyChecker.cs (发射前安全检查)
 */

using System.Collections;
using UnityEngine;

namespace KSPLaunchCountdown
{
    /// <summary>
    /// 倒计时控制器
    /// 协调AudioPlayer和LaunchSequence，按序列执行倒计时发射流程
    /// 支持单段/多段音频和"先启动发动机再分离"功能
    /// </summary>
    public class CountdownController : MonoBehaviour
    {
        /// <summary>日志标签</summary>
        private const string LOG_TAG = "[KSPLaunchCountdown]";

        /// <summary>
        /// 分级后等待恢复UI的延迟时间（秒）
        /// 可调整参数：增大此值让玩家有更多时间观察发射效果
        /// </summary>
        private const float UI_RESTORE_DELAY = 3.0f;

        /// <summary>音频播放器引用</summary>
        private AudioPlayer audioPlayer;

        /// <summary>发射序列执行器引用</summary>
        private LaunchSequence launchSequence;

        /// <summary>本地化系统引用</summary>
        private Localization localization;

        /// <summary>当前是否正在执行倒计时序列</summary>
        private bool isCountingDown = false;

        /// <summary>当前是否被取消</summary>
        private bool isCancelled = false;

        /// <summary>UI在倒计时开始前是否已经被隐藏</summary>
        private bool uiWasHiddenBefore = false;

        /// <summary>
        /// 最近一次安全检查的结果
        /// 用于在倒计时期间判断是否需要"先点火后放行"策略
        /// </summary>
        private SafetyCheckResult lastSafetyCheckResult;

        /// <summary>
        /// 倒计时状态变化回调
        /// 参数：true=倒计时开始，false=倒计时结束或取消
        /// </summary>
        public event System.Action<bool> OnCountdownStateChanged;

        /// <summary>获取当前是否正在执行倒计时</summary>
        public bool IsCountingDown => isCountingDown;

        /// <summary>
        /// 初始化控制器，注入依赖的音频播放器、发射序列执行器和本地化系统
        /// </summary>
        public void Initialize(AudioPlayer player, LaunchSequence sequence, Localization loc)
        {
            audioPlayer = player;
            launchSequence = sequence;
            localization = loc;
            audioPlayer.OnAudioFinished += OnAudioPlaybackFinished;
        }

        /// <summary>
        /// 开始倒计时序列
        /// 根据预设的音频模式（单段/多段）和配置执行不同的发射流程
        /// </summary>
        /// <param name="preset">选中的预设对象，包含音频路径和配置</param>
        /// <param name="safetyResult">
        /// 可选的安全检查结果。
        /// 传入null时会在内部自动执行安全检查；
        /// 由菜单传入时可直接使用菜单已完成的检查结果。
        /// </param>
        public void StartCountdown(CountdownPreset preset, SafetyCheckResult safetyResult = null)
        {
            if (isCountingDown)
            {
                Debug.LogWarning($"{LOG_TAG} 倒计时已在进行中，忽略重复请求");
                return;
            }

            if (!HighLogic.LoadedSceneIsFlight || FlightGlobals.ActiveVessel == null)
            {
                Debug.LogWarning($"{LOG_TAG} 无法开始倒计时：当前不在飞行场景或无活跃飞船");
                return;
            }

            // 如果没有传入安全检查结果，在内部执行一次安全检查
            lastSafetyCheckResult = safetyResult ?? LaunchSafetyChecker.PerformCheck(
                FlightGlobals.ActiveVessel, isCountingDown, localization);

            isCountingDown = true;
            isCancelled = false;

            Debug.Log($"{LOG_TAG} 开始倒计时序列，预设: {preset.Name}" +
                $" (模式: {(preset.IsMultiSegment ? "多段" : "单段")}" +
                $", 先启动发动机: {preset.StartEngineBeforeSeparation}" +
                $", 发动机已启动: {lastSafetyCheckResult?.EngineAlreadyRunning})");

            OnCountdownStateChanged?.Invoke(true);

            // 根据音频模式启动不同的协程
            if (preset.IsMultiSegment)
            {
                StartCoroutine(MultiSegmentCountdownCoroutine(preset));
            }
            else
            {
                StartCoroutine(SingleSegmentCountdownCoroutine(preset));
            }
        }

        /// <summary>
        /// 取消倒计时
        /// </summary>
        public void CancelCountdown()
        {
            if (!isCountingDown) return;

            Debug.Log($"{LOG_TAG} 取消倒计时");
            isCancelled = true;

            if (audioPlayer != null) audioPlayer.Stop();
            if (launchSequence != null) launchSequence.ReleaseThrottleHold();

            RestoreUI();
            isCountingDown = false;
            OnCountdownStateChanged?.Invoke(false);
        }

        /// <summary>
        /// 单段音频倒计时协程
        /// 流程：隐藏UI → SAS → [发动机已启动则0油门/否则满油门] → 播放音频 →
        ///       [若发动机已启动则音频结束后满油门] → 分级 → [第二次分级] → 延迟 → 恢复UI
        /// </summary>
        private IEnumerator SingleSegmentCountdownCoroutine(CountdownPreset preset)
        {
            // 步骤1-3：隐藏UI、开SAS、设置油门
            yield return StartCoroutine(PreLaunchSequence());
            if (isCancelled) yield break;

            // 步骤4：播放倒计时音频
            audioPlayer.LoadAndPlay(preset.AudioFilePath);
            yield return null;

            // 步骤5：等待音频播放结束
            while (audioPlayer.IsPlaying && !isCancelled)
            {
                yield return null;
            }
            if (isCancelled) yield break;

            // 步骤6：音频播放结束
            // 如果发动机已启动（"先点火后放行"策略），此时加满油门
            if (lastSafetyCheckResult != null && lastSafetyCheckResult.EngineAlreadyRunning)
            {
                Debug.Log($"{LOG_TAG} 倒计时音频播放结束，发动机已点火，现在加满油门");
                launchSequence.SetFullThrottle();
                yield return null;
            }

            // 步骤7：执行第一次分级
            Debug.Log($"{LOG_TAG} 执行分级");
            launchSequence.ActivateNextStage();

            // 步骤7：若启用"先启动发动机再分离"，等待延迟后第二次分级
            if (preset.StartEngineBeforeSeparation)
            {
                Debug.Log($"{LOG_TAG} 等待 {preset.SingleStageDelay} 秒后执行第二次分级");
                float elapsed = 0f;
                while (elapsed < preset.SingleStageDelay && !isCancelled)
                {
                    elapsed += UnityEngine.Time.deltaTime;
                    yield return null;
                }
                if (isCancelled) yield break;

                launchSequence.ActivateNextStage();
                Debug.Log($"{LOG_TAG} 第二次分级完成");
            }

            // 步骤8：等待后恢复UI
            yield return StartCoroutine(WaitAndRestoreUI());
        }

        /// <summary>
        /// 多段音频倒计时协程
        /// 流程：隐藏UI → SAS → [发动机已启动则0油门/否则满油门] → 播放p1 →
        ///       [若发动机已启动则p1结束后满油门] → 分级 → 播放p2 → [第二次分级] →
        ///       等待p2结束 → 延迟 → 恢复UI
        /// </summary>
        private IEnumerator MultiSegmentCountdownCoroutine(CountdownPreset preset)
        {
            // 步骤1-3：隐藏UI、开SAS、设置油门
            yield return StartCoroutine(PreLaunchSequence());
            if (isCancelled) yield break;

            // 步骤4：播放p1音频（倒计时部分）
            Debug.Log($"{LOG_TAG} 播放p1音频: {preset.AudioFilePath}");
            audioPlayer.LoadAndPlay(preset.AudioFilePath);
            yield return null;

            // 步骤5：等待p1播放结束
            while (audioPlayer.IsPlaying && !isCancelled)
            {
                yield return null;
            }
            if (isCancelled) yield break;

            // 步骤6：p1播放结束
            // 如果发动机已启动（"先点火后放行"策略），此时加满油门
            if (lastSafetyCheckResult != null && lastSafetyCheckResult.EngineAlreadyRunning)
            {
                Debug.Log($"{LOG_TAG} p1音频播放结束，发动机已点火，现在加满油门");
                launchSequence.SetFullThrottle();
                yield return null;
            }

            // 步骤7：执行第一次分级
            Debug.Log($"{LOG_TAG} p1音频播放结束，执行分级");
            launchSequence.ActivateNextStage();

            // 步骤8：播放p2音频（点火后部分）
            Debug.Log($"{LOG_TAG} 播放p2音频: {preset.AudioFilePath2}");
            audioPlayer.LoadAndPlay(preset.AudioFilePath2);
            yield return null;

            // 步骤8：若启用"先启动发动机再分离"，p2开始后等待延迟执行第二次分级
            if (preset.StartEngineBeforeSeparation)
            {
                Debug.Log($"{LOG_TAG} p2开始播放，等待 {preset.MultiStageDelay} 秒后执行第二次分级");
                float elapsed = 0f;
                while (elapsed < preset.MultiStageDelay && !isCancelled)
                {
                    elapsed += UnityEngine.Time.deltaTime;
                    yield return null;
                }
                if (isCancelled) yield break;

                launchSequence.ActivateNextStage();
                Debug.Log($"{LOG_TAG} 第二次分级完成");
            }

            // 步骤9：等待p2播放结束
            while (audioPlayer.IsPlaying && !isCancelled)
            {
                yield return null;
            }
            if (isCancelled) yield break;

            // 步骤10-11：等待后恢复UI
            yield return StartCoroutine(WaitAndRestoreUI());
        }

        /// <summary>
        /// 发射前准备序列协程
        /// 隐藏UI → 开SAS → 设置油门
        /// 单段和多段模式共用
        ///
        /// 油门设置策略：
        ///   - 正常情况下直接设置满油门
        ///   - 若芯一级发动机已启动（"先点火后放行"），倒计时期间保持0油门，
        ///     防止火箭在倒计时语音播放期间提前起飞
        /// </summary>
        private IEnumerator PreLaunchSequence()
        {
            HideUI();
            yield return null;
            if (isCancelled) yield break;

            launchSequence.EnableSAS();
            yield return null;
            if (isCancelled) yield break;

            // 根据安全检查结果决定初始油门
            if (lastSafetyCheckResult != null && lastSafetyCheckResult.EngineAlreadyRunning)
            {
                Debug.Log($"{LOG_TAG} 发动机已启动，倒计时期间保持0油门");
                launchSequence.SetZeroThrottle();
            }
            else
            {
                launchSequence.SetFullThrottle();
            }
            yield return null;
        }

        /// <summary>
        /// 等待后恢复UI协程
        /// 等待UI_RESTORE_DELAY秒后恢复UI，释放油门保持
        /// </summary>
        private IEnumerator WaitAndRestoreUI()
        {
            float elapsed = 0f;
            while (elapsed < UI_RESTORE_DELAY && !isCancelled)
            {
                elapsed += UnityEngine.Time.deltaTime;
                yield return null;
            }

            RestoreUI();
            launchSequence.ReleaseThrottleHold();

            isCountingDown = false;
            OnCountdownStateChanged?.Invoke(false);
            Debug.Log($"{LOG_TAG} 倒计时序列完成");
        }

        /// <summary>
        /// 音频播放结束回调（备用通知机制）
        /// </summary>
        private void OnAudioPlaybackFinished()
        {
            Debug.Log($"{LOG_TAG} 收到音频播放结束通知");
        }

        /// <summary>隐藏游戏UI</summary>
        private void HideUI()
        {
            uiWasHiddenBefore = KSPApiHelper.IsUIHidden();
            KSPApiHelper.HideUI();
            Debug.Log($"{LOG_TAG} 游戏UI已隐藏");
        }

        /// <summary>恢复游戏UI</summary>
        private void RestoreUI()
        {
            if (!uiWasHiddenBefore)
            {
                KSPApiHelper.ShowUI();
                Debug.Log($"{LOG_TAG} 游戏UI已恢复");
            }
        }

        /// <summary>Unity销毁时清理</summary>
        void OnDestroy()
        {
            if (isCountingDown) CancelCountdown();
            if (audioPlayer != null) audioPlayer.OnAudioFinished -= OnAudioPlaybackFinished;
            StopAllCoroutines();
        }
    }
}
