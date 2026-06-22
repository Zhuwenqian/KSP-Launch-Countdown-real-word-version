/**
 * CountdownMenu.cs - KSP1 发射倒计时菜单UI
 *
 * 用途：提供倒计时控制的图形界面，包括预设选择、发射按钮和设置选项。
 * 使用Unity IMGUI系统（OnGUI + GUILayout.Window）绘制菜单窗口。
 *
 * 菜单布局：
 *   ┌──────────────────────────────┐
 *   │      发射倒计时控制            │
 *   ├──────────────────────────────┤
 *   │ 预设: [DFH-1           ▼]   │
 *   │ ☑ 先启动发动机再分离          │
 *   │ 音量: [──────●────] 50%      │
 *   │                              │
 *   │ ⚠ 发射前检查未通过            │
 *   │   • 不在发射台               │
 *   │   ☑ 强制发射                 │
 *   │                              │
 *   │      [  发射  ]              │
 *   │      [  取消  ]              │
 *   └──────────────────────────────┘
 *
 * 交互流程：
 *   1. 从下拉列表选择预设语音包
 *   2. 勾选/取消"先启动发动机再分离"
 *   3. 拖动音量滑块调节倒计时语音音量（0%~100%）
 *   4. 点击Launch按钮，系统执行发射前安全检查（包含电量、发射台、倒计时冲突、发动机状态）
 *   5. 如果检查未通过（如不在发射台、电量不足），窗口内会显示黄色警告，勾选"强制发射"后可继续
 *   6. 倒计时进行中可点击Cancel取消
 *   7. 观看自动执行的发射序列！
 *
 * 安全检查UI：
 *   - 主动点击Launch时执行检查并显示结果
 *   - 订阅CountdownController.OnSafetyCheckFailed事件，确保任何路径的检查失败都会显示警告
 *
 * 依赖：
 *   - Assembly-CSharp.dll (KSP核心)
 *   - UnityEngine.CoreModule.dll (Unity核心)
 *   - KSPApiHelper.cs (KSP API反射辅助类)
 *   - Localization.cs (多语言支持)
 *   - SettingsManager.cs (音量设置持久化)
 *   - LaunchSafetyChecker.cs (发射前安全检查)
 */

using UnityEngine;

namespace KSPLaunchCountdown
{
    /// <summary>
    /// 倒计时菜单UI
    /// 使用Unity IMGUI绘制控制菜单，提供预设选择、设置和发射控制
    /// </summary>
    public class CountdownMenu : MonoBehaviour
    {
        /// <summary>日志标签</summary>
        private const string LOG_TAG = "[KSPLaunchCountdown]";

        /// <summary>预设管理器引用</summary>
        private PresetManager presetManager;

        /// <summary>倒计时控制器引用</summary>
        private CountdownController countdownController;

        /// <summary>设置管理器引用</summary>
        private SettingsManager settingsManager;

        /// <summary>音频播放器引用</summary>
        private AudioPlayer audioPlayer;

        /// <summary>本地化系统引用</summary>
        private Localization localization;

        /// <summary>菜单窗口是否显示</summary>
        private bool isVisible = false;

        /// <summary>菜单窗口的位置和大小</summary>
        private Rect windowRect = new Rect(200f, 200f, 300f, 260f);

        /// <summary>当前选中的预设索引</summary>
        private int selectedPresetIndex = 0;

        /// <summary>预设名称数组，用于下拉列表显示</summary>
        private string[] presetNames = new string[0];

        /// <summary>窗口唯一ID</summary>
        private readonly int windowId = "KSPLaunchCountdownMenu".GetHashCode();

        /// <summary>是否启用"先启动发动机再分离"
        /// 切换预设时自动读取该预设的配置值，用户也可手动修改</summary>
        private bool startEngineBeforeSeparation = false;

        /// <summary>
        /// 最近一次安全检查的结果
        /// 用于在菜单中显示警告和决定是否允许发射
        /// </summary>
        private SafetyCheckResult lastSafetyCheckResult;

        /// <summary>
        /// 是否显示安全检查警告
        /// 当上次检查未通过且玩家未勾选强制发射时为true
        /// </summary>
        private bool showSafetyWarning = false;

        /// <summary>
        /// 是否强制发射（跳过安全检查）
        /// 由玩家在菜单中勾选
        /// </summary>
        private bool forceLaunch = false;

        /// <summary>
        /// 获取或设置菜单是否可见
        /// </summary>
        public bool IsVisible
        {
            get => isVisible;
            set => isVisible = value;
        }

        /// <summary>
        /// 初始化菜单
        /// </summary>
        /// <param name="manager">预设管理器</param>
        /// <param name="controller">倒计时控制器</param>
        /// <param name="settings">设置管理器</param>
        /// <param name="player">音频播放器</param>
        /// <param name="loc">本地化系统</param>
        public void Initialize(PresetManager manager, CountdownController controller, SettingsManager settings, AudioPlayer player, Localization loc)
        {
            presetManager = manager;
            countdownController = controller;
            settingsManager = settings;
            audioPlayer = player;
            localization = loc;

            RefreshPresetList();

            // startEngineBeforeSeparation 默认为false，由用户在UI上勾选
            // 不从预设配置读取，因为此选项取决于当前火箭的分级模式

            countdownController.OnCountdownStateChanged += OnCountdownStateChanged;
            countdownController.OnSafetyCheckFailed += OnSafetyCheckFailed;

            Debug.Log($"{LOG_TAG} 菜单初始化完成，共 {presetNames.Length} 个预设");
        }

        /// <summary>刷新预设列表</summary>
        public void RefreshPresetList()
        {
            if (presetManager != null)
            {
                presetNames = presetManager.GetPresetNames().ToArray();
                if (selectedPresetIndex >= presetNames.Length)
                {
                    selectedPresetIndex = 0;
                }
            }
        }

        /// <summary>
        /// 倒计时状态变化回调
        /// 倒计时结束时隐藏菜单或重置UI状态
        /// </summary>
        /// <param name="started">true=倒计时开始，false=倒计时结束</param>
        private void OnCountdownStateChanged(bool started)
        {
            if (started)
            {
                isVisible = false;
            }
            else
            {
                // 倒计时结束（正常完成或取消）时，重置强制发射状态
                forceLaunch = false;
                showSafetyWarning = false;
            }
        }

        /// <summary>
        /// 安全检查失败回调
        /// 当CountdownController内部安全检查未通过时调用
        /// 在菜单中显示警告并允许玩家选择强制发射
        /// </summary>
        /// <param name="result">安全检查结果</param>
        private void OnSafetyCheckFailed(SafetyCheckResult result)
        {
            lastSafetyCheckResult = result;
            showSafetyWarning = result != null && !result.IsSafe;
            forceLaunch = false;

            Debug.LogWarning($"{LOG_TAG} 菜单收到安全检查失败通知，显示警告");
        }

        /// <summary>Unity IMGUI绘制</summary>
        void OnGUI()
        {
            if (!isVisible) return;

            GUISkin uiSkin = KSPApiHelper.GetUISkin();
            if (uiSkin != null)
            {
                GUI.skin = uiSkin;
            }

            windowRect = GUILayout.Window(
                windowId,
                windowRect,
                DrawWindowContent,
                localization.GetString(Localization.Keys.WindowTitle));
        }

        /// <summary>绘制菜单窗口内容</summary>
        private void DrawWindowContent(int windowId)
        {
            GUILayout.BeginVertical();

            // 预设选择区域
            GUILayout.Label(localization.GetString(Localization.Keys.SelectPreset));
            if (presetNames.Length > 0)
            {
                int newIndex = GUILayout.SelectionGrid(
                    selectedPresetIndex,
                    presetNames,
                    1
                );

                // 预设切换时只更新索引，startEngineBeforeSeparation由用户手动控制
                if (newIndex != selectedPresetIndex)
                {
                    selectedPresetIndex = newIndex;
                }
            }
            else
            {
                GUILayout.Label(localization.GetString(Localization.Keys.NoPresetsFound));
            }

            GUILayout.Space(5f);

            // "先启动发动机再分离"复选框
            // 启用后分级操作会执行两次：第一次启动发动机，第二次分离
            startEngineBeforeSeparation = GUILayout.Toggle(
                startEngineBeforeSeparation,
                localization.GetString(Localization.Keys.StartEngineBeforeSeparation)
            );

            GUILayout.Space(5f);

            // 音量控制滑块
            // 音量范围 0%~100%，对应 AudioPlayer.Volume 的 0.0~1.0
            DrawVolumeSlider();

            GUILayout.Space(5f);

            // 安全检查警告区域
            // 当上次安全检查未通过时显示失败原因和强制发射选项
            DrawSafetyWarning();

            GUILayout.Space(10f);

            // Launch按钮
            // 只有在以下情况可用：
            // 1. 有可用预设
            // 2. 没有正在进行的倒计时
            // 3. 安全检查通过，或玩家勾选了强制发射
            bool safetyOk = lastSafetyCheckResult == null || lastSafetyCheckResult.IsSafe || forceLaunch;
            bool canLaunch = presetNames.Length > 0 && !countdownController.IsCountingDown && safetyOk;
            GUI.enabled = canLaunch;
            if (GUILayout.Button(localization.GetString(Localization.Keys.LaunchButton), GUILayout.Height(30f)))
            {
                OnLaunchClicked();
            }
            GUI.enabled = true;

            // Cancel按钮
            GUI.enabled = countdownController.IsCountingDown;
            if (GUILayout.Button(localization.GetString(Localization.Keys.CancelButton), GUILayout.Height(30f)))
            {
                OnCancelClicked();
            }
            GUI.enabled = true;

            GUILayout.EndVertical();

            GUI.DragWindow();
        }

        /// <summary>
        /// 绘制音量控制滑块
        /// 实时更新AudioPlayer音量和SettingsManager持久化设置
        /// </summary>
        private void DrawVolumeSlider()
        {
            if (settingsManager == null) return;

            // 当前音量百分比（0~100）
            int volumePercent = Mathf.RoundToInt(settingsManager.CountdownVolume * 100f);

            GUILayout.BeginHorizontal();
            GUILayout.Label(localization.GetString(Localization.Keys.VolumeLabel, volumePercent), GUILayout.Width(80f));

            // 滑块范围 0.0~1.0
            float newVolume = GUILayout.HorizontalSlider(
                settingsManager.CountdownVolume,
                0.0f,
                1.0f
            );
            GUILayout.EndHorizontal();

            // 只在音量变化时更新（避免每帧重复保存）
            if (Mathf.Abs(newVolume - settingsManager.CountdownVolume) > 0.001f)
            {
                settingsManager.CountdownVolume = newVolume;

                // 同步更新当前AudioPlayer的音量
                // 即使音频正在播放也会立即生效
                if (audioPlayer != null)
                {
                    audioPlayer.Volume = newVolume;
                }
            }
        }

        /// <summary>
        /// 绘制安全检查警告区域
        /// 当安全检查未通过时显示失败原因列表和"强制发射"复选框
        /// </summary>
        private void DrawSafetyWarning()
        {
            if (!showSafetyWarning || lastSafetyCheckResult == null || lastSafetyCheckResult.IsSafe)
            {
                return;
            }

            // 使用醒目的颜色显示警告标题
            GUIStyle warningStyle = new GUIStyle(GUI.skin.label);
            warningStyle.normal.textColor = Color.yellow;
            warningStyle.fontStyle = FontStyle.Bold;
            GUILayout.Label(localization.GetString(Localization.Keys.SafetyCheckFailedTitle), warningStyle);

            // 列出所有失败原因
            foreach (string reason in lastSafetyCheckResult.FailureReasons)
            {
                GUILayout.Label("• " + reason);
            }

            // 强制发射复选框
            forceLaunch = GUILayout.Toggle(
                forceLaunch,
                localization.GetString(Localization.Keys.ForceLaunchButton)
            );
        }

        /// <summary>Launch按钮点击处理</summary>
        private void OnLaunchClicked()
        {
            if (presetNames.Length == 0 || selectedPresetIndex >= presetNames.Length)
            {
                Debug.LogWarning($"{LOG_TAG} 无法启动倒计时：无可用预设");
                return;
            }

            var preset = presetManager.GetPresetByIndex(selectedPresetIndex);
            if (preset == null)
            {
                Debug.LogError($"{LOG_TAG} 无法获取预设对象");
                return;
            }

            // 将用户在菜单中的设置覆盖预设配置
            preset.StartEngineBeforeSeparation = startEngineBeforeSeparation;

            // 执行发射前安全检查
            // 如果玩家勾选了强制发射，跳过安全检查直接启动
            SafetyCheckResult safetyResult = null;
            if (!forceLaunch)
            {
                safetyResult = LaunchSafetyChecker.PerformCheck(
                    FlightGlobals.ActiveVessel, countdownController.IsCountingDown, localization);
            }
            else
            {
                Debug.Log($"{LOG_TAG} 玩家选择强制发射，跳过安全检查");
            }

            lastSafetyCheckResult = safetyResult;
            showSafetyWarning = safetyResult != null && !safetyResult.IsSafe;

            // 如果检查未通过且没有勾选强制发射，不启动倒计时
            if (safetyResult != null && !safetyResult.IsSafe && !forceLaunch)
            {
                Debug.LogWarning($"{LOG_TAG} 发射前安全检查未通过，等待玩家确认");
                return;
            }

            Debug.Log($"{LOG_TAG} 选择预设: {preset.Name}" +
                $" (模式: {(preset.IsMultiSegment ? "多段" : "单段")}" +
                $", 先启动发动机: {startEngineBeforeSeparation})");

            countdownController.StartCountdown(preset, safetyResult);
        }

        /// <summary>Cancel按钮点击处理</summary>
        private void OnCancelClicked()
        {
            countdownController.CancelCountdown();
        }

        /// <summary>Unity销毁时清理</summary>
        void OnDestroy()
        {
            if (countdownController != null)
            {
                countdownController.OnCountdownStateChanged -= OnCountdownStateChanged;
                countdownController.OnSafetyCheckFailed -= OnSafetyCheckFailed;
            }
        }
    }
}
