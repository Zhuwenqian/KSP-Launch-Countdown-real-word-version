/**
 * CountdownAPI.cs - KSP1 发射倒计时模组对外接口
 *
 * 用途：为其他 KSP 模组提供稳定、反射友好的公共 API，使其能够：
 *   1. 查询本模组加载的倒计时语音包预设列表
 *   2. 按名称选择当前要使用的预设
 *   3. 启动/取消倒计时
 *   4. 订阅预设切换、倒计时开始/结束等事件
 *
 * 使用方式（其他模组通过反射调用，无需引用本 DLL）：
 *   1. 获取单例实例：
 *      Type apiType = Type.GetType("KSPLaunchCountdown.CountdownAPI, KSPLaunchCountdown");
 *      PropertyInfo instanceProp = apiType.GetProperty("Instance");
 *      object api = instanceProp.GetValue(null);
 *
 *   2. 选择预设：
 *      MethodInfo selectMethod = apiType.GetMethod("SelectPreset");
 *      bool ok = (bool)selectMethod.Invoke(api, new object[] { "Saturn V" });
 *
 *   3. 启动倒计时：
 *      MethodInfo startMethod = apiType.GetMethod("StartCountdown", Type.EmptyTypes);
 *      bool started = (bool)startMethod.Invoke(api, null);
 *
 * 事件通知：
 *   - 本类提供实例事件（直接引用 DLL 时可订阅）：
 *     OnPresetSelected(string presetName)
 *     OnCountdownStarted()
 *     OnCountdownFinished()
 *   - 同时通过 KSP GameEvents 广播（无需引用 DLL）：
 *     GameEvents.FindEvent<EventData<string>>("KSPLaunchCountdownPresetSelected")
 *     GameEvents.FindEvent<EventVoid>("KSPLaunchCountdownStarted")
 *     GameEvents.FindEvent<EventVoid>("KSPLaunchCountdownFinished")
 *
 * 设计说明：
 *   - 采用单例模式，外部模组始终通过 CountdownAPI.Instance 访问
 *   - 所有公共方法都进行参数校验，失败时返回 false 或空列表，避免抛出异常
 *   - StartCountdown 会执行与 UI 相同的安全检查，未通过时返回 false
 *     并触发 OnSafetyCheckFailed 事件通知 UI 显示警告
 *   - 倒计时进行中再次调用 StartCountdown 会被忽略
 *   - 通过 SetCountdownMenu 方法注入菜单引用，实现 API 与 UI 预设选择的同步
 *
 * 可调整参数：
 *   - StartEngineBeforeSeparation：外部模组启动倒计时时是否启用"先启动发动机再分离"，
 *     默认 false，可通过属性读写。
 *
 * 依赖：
 *   - Assembly-CSharp.dll (KSP核心，提供 FlightGlobals、GameEvents 等)
 *   - UnityEngine.CoreModule.dll (Unity核心，提供 MonoBehaviour、Debug 等)
 *   - PresetManager.cs (预设管理)
 *   - CountdownController.cs (倒计时核心控制)
 *   - LaunchSafetyChecker.cs (发射前安全检查)
 *   - Localization.cs (多语言支持)
 */

using System;
using System.Collections.Generic;
using UnityEngine;

namespace KSPLaunchCountdown
{
    /// <summary>
    /// 对外 API 类
    /// 提供其他模组与本模组交互的稳定入口
    /// </summary>
    public class CountdownAPI : MonoBehaviour
    {
        /// <summary>日志标签</summary>
        private const string LOG_TAG = "[KSPLaunchCountdown]";

        /// <summary>
        /// 单例实例
        /// 外部模组通过此属性访问 API
        /// </summary>
        public static CountdownAPI Instance { get; private set; }

        /// <summary>预设管理器引用</summary>
        private PresetManager presetManager;

        /// <summary>倒计时控制器引用</summary>
        private CountdownController countdownController;

        /// <summary>本地化系统引用</summary>
        private Localization localization;

        /// <summary>
        /// 倒计时菜单 UI 引用（可选）
        /// 用于外部模组选择预设时同步更新 UI 显示
        /// </summary>
        private CountdownMenu countdownMenu;

        /// <summary>
        /// 当前选中的预设
        /// 外部模组选择预设或启动倒计时时会更新
        /// </summary>
        public CountdownPreset CurrentPreset { get; private set; }

        /// <summary>
        /// 外部模组启动倒计时时是否启用"先启动发动机再分离"
        /// 可调整参数：设为 true 时，单段/多段模式会按预设延迟执行第二次分级
        /// 仅影响通过 API 启动的倒计时，不影响 UI 上的同名选项
        /// </summary>
        public bool StartEngineBeforeSeparation { get; set; } = false;

        /// <summary>
        /// 预设选择事件
        /// 参数：选中的预设名称
        /// </summary>
        public event Action<string> OnPresetSelected;

        /// <summary>
        /// 倒计时开始事件
        /// </summary>
        public event Action OnCountdownStarted;

        /// <summary>
        /// 倒计时结束事件（正常完成或取消都会触发）
        /// </summary>
        public event Action OnCountdownFinished;

        /// <summary>
        /// 安全检查失败事件
        /// 供 UI 显示警告，与 CountdownController.OnSafetyCheckFailed 保持一致
        /// </summary>
        public event Action<SafetyCheckResult> OnSafetyCheckFailed;

        /// <summary>
        /// KSP GameEvents：预设被选择时触发
        /// 事件名：KSPLaunchCountdownPresetSelected
        /// 参数：预设名称
        /// </summary>
        public static readonly EventData<string> GameEventPresetSelected =
            new EventData<string>("KSPLaunchCountdownPresetSelected");

        /// <summary>
        /// KSP GameEvents：倒计时开始时触发
        /// 事件名：KSPLaunchCountdownStarted
        /// </summary>
        public static readonly EventVoid GameEventCountdownStarted =
            new EventVoid("KSPLaunchCountdownStarted");

        /// <summary>
        /// KSP GameEvents：倒计时结束时触发
        /// 事件名：KSPLaunchCountdownFinished
        /// </summary>
        public static readonly EventVoid GameEventCountdownFinished =
            new EventVoid("KSPLaunchCountdownFinished");

        /// <summary>
        /// 初始化 API
        /// </summary>
        /// <param name="manager">预设管理器</param>
        /// <param name="controller">倒计时控制器</param>
        /// <param name="loc">本地化系统</param>
        public void Initialize(PresetManager manager, CountdownController controller, Localization loc)
        {
            presetManager = manager;
            countdownController = controller;
            localization = loc;
            Instance = this;

            if (countdownController != null)
            {
                countdownController.OnCountdownStateChanged += OnCountdownStateChanged;
            }

            // 默认选中第一个预设，方便外部模组直接 StartCountdown()
            SelectFirstPresetAsDefault();

            Debug.Log($"{LOG_TAG} 对外 API 初始化完成");
        }

        /// <summary>
        /// 设置倒计时菜单引用
        /// 由 KSPLaunchCountdownMod 在菜单初始化后调用，实现 API 与 UI 的双向同步
        /// </summary>
        /// <param name="menu">倒计时菜单 UI</param>
        public void SetCountdownMenu(CountdownMenu menu)
        {
            countdownMenu = menu;

            // 如果已经有当前预设，立即同步到菜单
            if (CurrentPreset != null)
            {
                SyncMenuSelection(CurrentPreset.Name);
            }
        }

        /// <summary>
        /// 选择指定名称的预设
        /// 选择成功时会触发 OnPresetSelected 事件和 KSP GameEvent
        /// </summary>
        /// <param name="presetName">预设名称</param>
        /// <returns>是否选择成功</returns>
        public bool SelectPreset(string presetName)
        {
            if (presetManager == null)
            {
                Debug.LogWarning($"{LOG_TAG} API 选择预设失败：PresetManager 未初始化");
                return false;
            }

            CountdownPreset preset = presetManager.GetPreset(presetName);
            if (preset == null)
            {
                Debug.LogWarning($"{LOG_TAG} API 选择预设失败：找不到预设 '{presetName}'");
                return false;
            }

            CurrentPreset = preset;
            Debug.Log($"{LOG_TAG} API 选择预设: {presetName}");

            // 同步更新 UI 选中项
            SyncMenuSelection(presetName);

            // 触发实例事件
            OnPresetSelected?.Invoke(presetName);

            // 触发 KSP GameEvent，方便未引用 DLL 的模组监听
            try
            {
                GameEventPresetSelected.Fire(presetName);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"{LOG_TAG} 触发 GameEventPresetSelected 失败: {ex.Message}");
            }

            return true;
        }

        /// <summary>
        /// 使用当前已选预设启动倒计时
        /// 会执行完整的安全检查
        /// </summary>
        /// <returns>是否成功启动</returns>
        public bool StartCountdown()
        {
            if (CurrentPreset == null)
            {
                Debug.LogWarning($"{LOG_TAG} API 启动倒计时失败：当前没有选中预设");
                return false;
            }

            return StartCountdown(CurrentPreset.Name);
        }

        /// <summary>
        /// 选择指定预设并立即启动倒计时
        /// 会执行完整的安全检查
        /// </summary>
        /// <param name="presetName">预设名称</param>
        /// <returns>是否成功启动</returns>
        public bool StartCountdown(string presetName)
        {
            if (countdownController == null)
            {
                Debug.LogWarning($"{LOG_TAG} API 启动倒计时失败：CountdownController 未初始化");
                return false;
            }

            if (countdownController.IsCountingDown)
            {
                Debug.LogWarning($"{LOG_TAG} API 启动倒计时失败：已有正在进行的倒计时");
                return false;
            }

            if (!HighLogic.LoadedSceneIsFlight || FlightGlobals.ActiveVessel == null)
            {
                Debug.LogWarning($"{LOG_TAG} API 启动倒计时失败：当前不在飞行场景或无活跃飞船");
                return false;
            }

            if (!SelectPreset(presetName))
            {
                return false;
            }

            // 执行安全检查
            SafetyCheckResult safetyResult = LaunchSafetyChecker.PerformCheck(
                FlightGlobals.ActiveVessel, countdownController.IsCountingDown, localization);

            if (!safetyResult.IsSafe)
            {
                Debug.LogWarning($"{LOG_TAG} API 启动倒计时失败：安全检查未通过");
                OnSafetyCheckFailed?.Invoke(safetyResult);
                return false;
            }

            // 应用 API 自己的"先启动发动机再分离"设置
            CurrentPreset.StartEngineBeforeSeparation = StartEngineBeforeSeparation;

            Debug.Log($"{LOG_TAG} API 启动倒计时，预设: {CurrentPreset.Name}, 先启动发动机: {StartEngineBeforeSeparation}");
            countdownController.StartCountdown(CurrentPreset, safetyResult);

            OnCountdownStarted?.Invoke();

            try
            {
                GameEventCountdownStarted.Fire();
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"{LOG_TAG} 触发 GameEventCountdownStarted 失败: {ex.Message}");
            }

            return true;
        }

        /// <summary>
        /// 取消当前正在进行的倒计时
        /// </summary>
        public void CancelCountdown()
        {
            if (countdownController == null) return;
            countdownController.CancelCountdown();
        }

        /// <summary>
        /// 获取所有可用预设的名称列表
        /// </summary>
        /// <returns>预设名称列表</returns>
        public List<string> GetAvailablePresets()
        {
            if (presetManager == null)
            {
                return new List<string>();
            }
            return presetManager.GetPresetNames();
        }

        /// <summary>
        /// 获取当前选中预设的名称
        /// </summary>
        /// <returns>预设名称，未选中时返回 null</returns>
        public string GetCurrentPresetName()
        {
            return CurrentPreset?.Name;
        }

        /// <summary>
        /// 获取当前是否正在倒计时
        /// </summary>
        /// <returns>是否正在倒计时</returns>
        public bool IsCountingDown()
        {
            return countdownController != null && countdownController.IsCountingDown;
        }

        /// <summary>
        /// 由 CountdownMenu 调用，当 UI 切换预设时同步更新 API 的当前预设
        /// 注意：此方法不触发事件，避免与 UI 自身逻辑循环
        /// </summary>
        /// <param name="preset">UI 选中的预设对象</param>
        public void SyncPresetFromMenu(CountdownPreset preset)
        {
            if (preset == null) return;
            CurrentPreset = preset;
            Debug.Log($"{LOG_TAG} API 从 UI 同步预设: {preset.Name}");
        }

        /// <summary>
        /// 倒计时状态变化回调
        /// 在倒计时结束时触发 OnCountdownFinished 和 GameEvent
        /// </summary>
        /// <param name="started">true=开始，false=结束</param>
        private void OnCountdownStateChanged(bool started)
        {
            if (!started)
            {
                Debug.Log($"{LOG_TAG} API 检测到倒计时结束");
                OnCountdownFinished?.Invoke();

                try
                {
                    GameEventCountdownFinished.Fire();
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"{LOG_TAG} 触发 GameEventCountdownFinished 失败: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// 默认选中第一个预设
        /// 在 API 初始化时调用，确保外部模组可以直接 StartCountdown()
        /// </summary>
        private void SelectFirstPresetAsDefault()
        {
            if (presetManager == null || presetManager.PresetCount == 0)
            {
                CurrentPreset = null;
                return;
            }

            CountdownPreset first = presetManager.GetPresetByIndex(0);
            if (first != null)
            {
                CurrentPreset = first;
                Debug.Log($"{LOG_TAG} API 默认选中第一个预设: {first.Name}");
            }
        }

        /// <summary>
        /// 同步更新 UI 菜单中的选中项
        /// </summary>
        /// <param name="presetName">要选中的预设名称</param>
        private void SyncMenuSelection(string presetName)
        {
            if (countdownMenu == null) return;
            countdownMenu.SelectPresetByName(presetName);
        }

        /// <summary>
        /// Unity 销毁时清理
        /// </summary>
        void OnDestroy()
        {
            if (countdownController != null)
            {
                countdownController.OnCountdownStateChanged -= OnCountdownStateChanged;
            }

            if (Instance == this)
            {
                Instance = null;
            }
        }
    }
}
