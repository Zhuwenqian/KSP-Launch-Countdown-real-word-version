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
 *   │                              │
 *   │      [  Launch  ]            │
 *   │      [  Cancel  ]            │
 *   └──────────────────────────────┘
 *
 * 交互流程：
 *   1. 从下拉列表选择预设语音包
 *   2. 勾选/取消"先启动发动机再分离"（默认读取预设配置）
 *   3. 点击Launch按钮开始倒计时
 *   4. 倒计时进行中可点击Cancel取消
 *
 * 依赖：
 *   - Assembly-CSharp.dll (KSP核心)
 *   - UnityEngine.CoreModule.dll (Unity核心)
 *   - KSPApiHelper.cs (KSP API反射辅助类)
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

        /// <summary>菜单窗口是否显示</summary>
        private bool isVisible = false;

        /// <summary>菜单窗口的位置和大小</summary>
        private Rect windowRect = new Rect(200f, 200f, 280f, 200f);

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
        public void Initialize(PresetManager manager, CountdownController controller)
        {
            presetManager = manager;
            countdownController = controller;

            RefreshPresetList();

            // startEngineBeforeSeparation 默认为false，由用户在UI上勾选
            // 不从预设配置读取，因为此选项取决于当前火箭的分级模式

            countdownController.OnCountdownStateChanged += OnCountdownStateChanged;

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

        /// <summary>倒计时状态变化回调</summary>
        private void OnCountdownStateChanged(bool isRunning)
        {
            if (isRunning)
            {
                isVisible = false;
            }
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

            windowRect = GUILayout.Window(windowId, windowRect, DrawWindowContent, "发射倒计时控制");
        }

        /// <summary>绘制菜单窗口内容</summary>
        private void DrawWindowContent(int windowId)
        {
            GUILayout.BeginVertical();

            // 预设选择区域
            GUILayout.Label("选择倒计时预设:");
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
                GUILayout.Label("未找到预设语音包");
            }

            GUILayout.Space(5f);

            // "先启动发动机再分离"复选框
            // 启用后分级操作会执行两次：第一次启动发动机，第二次分离
            startEngineBeforeSeparation = GUILayout.Toggle(
                startEngineBeforeSeparation,
                "先启动发动机再分离"
            );

            GUILayout.Space(10f);

            // Launch按钮
            bool canLaunch = presetNames.Length > 0 && !countdownController.IsCountingDown;
            GUI.enabled = canLaunch;
            if (GUILayout.Button("Launch", GUILayout.Height(30f)))
            {
                OnLaunchClicked();
            }
            GUI.enabled = true;

            // Cancel按钮
            GUI.enabled = countdownController.IsCountingDown;
            if (GUILayout.Button("Cancel", GUILayout.Height(30f)))
            {
                OnCancelClicked();
            }
            GUI.enabled = true;

            GUILayout.EndVertical();

            GUI.DragWindow();
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

            Debug.Log($"{LOG_TAG} 选择预设: {preset.Name}" +
                $" (模式: {(preset.IsMultiSegment ? "多段" : "单段")}" +
                $", 先启动发动机: {startEngineBeforeSeparation})");

            countdownController.StartCountdown(preset);
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
            }
        }
    }
}
