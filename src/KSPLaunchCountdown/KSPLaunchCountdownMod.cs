/**
 * KSPLaunchCountdownMod.cs - KSP1 发射倒计时模组入口类
 *
 * 用途：KSP1发射倒计时模组的主入口点，负责在飞行场景中初始化和协调所有子模块。
 * 该类作为模组的中央控制器，管理工具栏按钮、倒计时菜单、音频播放、
 * 预设管理和发射序列等模块的生命周期。
 *
 * 功能流程：
 *   1. 在飞行场景加载时启动（KSPAddon.Startup.Flight）
 *   2. 初始化所有子模块：SettingsManager、Localization、PresetManager、
 *      AudioPlayer、LaunchSequence、CountdownController、CountdownMenu、ToolbarButton
 *   3. 在场景切换或模组卸载时清理所有资源
 *
 * 依赖：
 *   - Assembly-CSharp.dll (KSP核心，提供KSPAddon、MonoBehaviour、GameEvents等)
 *   - UnityEngine.CoreModule.dll (Unity核心，提供MonoBehaviour基类)
 *   - SettingsManager.cs (设置管理)
 *   - Localization.cs (多语言支持)
 *
 * KSP加载机制：
 *   KSP通过 [KSPAddon] 特性自动发现和加载模组类，
 *   无需手动注册，只需标注特性并继承 MonoBehaviour 即可。
 *   本模组在飞行场景(Flight)加载时启动，每次进入飞行场景都创建新实例。
 */

using UnityEngine;

namespace KSPLaunchCountdown
{
    /// <summary>
    /// 模组主入口类
    /// KSPAddon特性参数说明：
    ///   - KSPAddon.Startup.Flight: 模组加载时机，在飞行场景加载时启动
    ///     可选值：MainMenu, SpaceCentre, VAB, SPH, TrackingStation, EditorAny, Any等
    ///   - false: 是否只实例化一次（false表示每次场景加载都创建新实例）
    /// </summary>
    [KSPAddon(KSPAddon.Startup.Flight, false)]
    public class KSPLaunchCountdownMod : MonoBehaviour
    {
        /// <summary>预设语音包管理器，负责扫描和加载倒计时语音包</summary>
        private PresetManager presetManager;

        /// <summary>本地化系统，负责多语言文本</summary>
        private Localization localization;

        /// <summary>音频播放器，负责加载和播放倒计时语音</summary>
        private AudioPlayer audioPlayer;

        /// <summary>发射序列执行器，负责SAS、油门、分级等操作</summary>
        private LaunchSequence launchSequence;

        /// <summary>倒计时控制器，协调音频播放和发射序列的执行</summary>
        private CountdownController countdownController;

        /// <summary>倒计时菜单UI，提供Preset选择和Launch按钮</summary>
        private CountdownMenu countdownMenu;

        /// <summary>工具栏按钮，在KSP ApplicationLauncher上显示模组按钮</summary>
        private ToolbarButton toolbarButton;

        /// <summary>设置管理器，负责全局设置（音量等）的加载和保存</summary>
        private SettingsManager settingsManager;

        /// <summary>
        /// Unity生命周期方法，在对象首次创建时调用（仅一次）
        /// 用于初始化模组的所有子模块
        /// </summary>
        void Awake()
        {
            Debug.Log("[KSPLaunchCountdown] 模组已加载 - 发射倒计时准备就绪");
        }

        /// <summary>
        /// Unity生命周期方法，在对象启用时调用
        /// 用于初始化所有子模块并建立模块间的依赖关系
        /// </summary>
        void Start()
        {
            Debug.Log("[KSPLaunchCountdown] 模组已启动 - 初始化子模块");

            // 初始化设置管理器（纯逻辑类，从存档配置加载设置）
            settingsManager = new SettingsManager();
            settingsManager.Load();

            // 初始化本地化系统（纯逻辑类，加载语言文件）
            localization = new Localization();
            localization.Initialize();

            // 初始化预设管理器（纯逻辑类，不需要挂载到GameObject）
            presetManager = new PresetManager();
            presetManager.LoadPresets();

            // 初始化音频播放器（需要AudioSource组件，挂载到同一GameObject）
            audioPlayer = gameObject.AddComponent<AudioPlayer>();
            // 应用已保存的音量设置
            audioPlayer.Volume = settingsManager.CountdownVolume;

            // 初始化发射序列执行器（需要访问FlightGlobals，挂载到同一GameObject）
            launchSequence = gameObject.AddComponent<LaunchSequence>();

            // 初始化倒计时控制器（协调音频和发射序列，挂载到同一GameObject）
            countdownController = gameObject.AddComponent<CountdownController>();
            countdownController.Initialize(audioPlayer, launchSequence, localization);

            // 初始化倒计时菜单UI（需要OnGUI，挂载到同一GameObject）
            countdownMenu = gameObject.AddComponent<CountdownMenu>();
            countdownMenu.Initialize(presetManager, countdownController, settingsManager, audioPlayer, localization);

            // 初始化工具栏按钮（需要ApplicationLauncher，挂载到同一GameObject）
            toolbarButton = gameObject.AddComponent<ToolbarButton>();
            toolbarButton.Initialize(countdownMenu);
        }

        /// <summary>
        /// Unity生命周期方法，每帧调用
        /// 当前无帧级更新需求，所有异步逻辑通过协程实现
        /// </summary>
        void Update()
        {
            // 帧级更新逻辑由各子模块自行处理
        }

        /// <summary>
        /// Unity生命周期方法，在对象销毁时调用
        /// 用于清理所有子模块资源、注销事件、移除工具栏按钮等
        /// </summary>
        void OnDestroy()
        {
            Debug.Log("[KSPLaunchCountdown] 模组已卸载 - 清理资源");

            // 工具栏按钮需要手动清理（移除ApplicationLauncher按钮和事件）
            if (toolbarButton != null)
            {
                toolbarButton.Cleanup();
            }

            // 其他子模块随GameObject销毁自动清理，
            // 但如果有注册GameEvents等，需要在各自的OnDestroy中处理
        }
    }
}
