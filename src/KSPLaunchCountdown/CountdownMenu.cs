/**
 * CountdownMenu.cs - KSP1 发射倒计时菜单UI
 *
 * 用途：提供倒计时控制的图形界面，包括预设选择和发射按钮。
 * 使用Unity IMGUI系统（OnGUI + GUILayout.Window）绘制菜单窗口。
 *
 * 菜单布局：
 *   ┌─────────────────────────┐
 *   │    发射倒计时控制         │
 *   ├─────────────────────────┤
 *   │ 预设: [DFH-1      ▼]   │
 *   │                         │
 *   │     [  Launch  ]        │
 *   │     [  Cancel  ]        │
 *   └─────────────────────────┘
 *
 * 交互流程：
 *   1. 从下拉列表选择预设语音包
 *   2. 点击Launch按钮开始倒计时
 *   3. 倒计时进行中可点击Cancel取消
 *
 * 兼容性说明：
 *   由于精简版Assembly-CSharp.dll中HighLogic.UISkin返回UISkinDef类型，
 *   本类通过KSPApiHelper.GetUISkin()获取GUISkin，确保编译兼容性。
 *
 * 依赖：
 *   - Assembly-CSharp.dll (KSP核心，提供HighLogic.UISkin等)
 *   - UnityEngine.CoreModule.dll (Unity核心，提供OnGUI、GUILayout等)
 *   - KSPApiHelper.cs (KSP API反射辅助类)
 */

using UnityEngine;

namespace KSPLaunchCountdown
{
    /// <summary>
    /// 倒计时菜单UI
    /// 使用Unity IMGUI绘制控制菜单，提供预设选择和发射控制
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
        private Rect windowRect = new Rect(200f, 200f, 260f, 180f);

        /// <summary>当前选中的预设索引</summary>
        private int selectedPresetIndex = 0;

        /// <summary>预设名称数组，用于下拉列表显示</summary>
        private string[] presetNames = new string[0];

        /// <summary>窗口唯一ID，用于GUILayout.Window</summary>
        private readonly int windowId = "KSPLaunchCountdownMenu".GetHashCode();

        /// <summary>
        /// 获取或设置菜单是否可见
        /// </summary>
        public bool IsVisible
        {
            get => isVisible;
            set => isVisible = value;
        }

        /// <summary>
        /// 初始化菜单，注入预设管理器和倒计时控制器
        /// </summary>
        /// <param name="manager">预设管理器实例</param>
        /// <param name="controller">倒计时控制器实例</param>
        public void Initialize(PresetManager manager, CountdownController controller)
        {
            presetManager = manager;
            countdownController = controller;

            // 加载预设名称列表
            RefreshPresetList();

            // 注册倒计时状态变化事件，倒计时结束时自动关闭菜单
            countdownController.OnCountdownStateChanged += OnCountdownStateChanged;

            Debug.Log($"{LOG_TAG} 菜单初始化完成，共 {presetNames.Length} 个预设");
        }

        /// <summary>
        /// 刷新预设列表
        /// 从预设管理器重新获取预设名称数组
        /// </summary>
        public void RefreshPresetList()
        {
            if (presetManager != null)
            {
                presetNames = presetManager.GetPresetNames().ToArray();
                // 确保选中索引在有效范围内
                if (selectedPresetIndex >= presetNames.Length)
                {
                    selectedPresetIndex = 0;
                }
            }
        }

        /// <summary>
        /// 倒计时状态变化回调
        /// 倒计时开始时关闭菜单，倒计时结束或取消时不做特殊处理
        /// </summary>
        /// <param name="isRunning">true=倒计时开始，false=倒计时结束或取消</param>
        private void OnCountdownStateChanged(bool isRunning)
        {
            if (isRunning)
            {
                // 倒计时开始时关闭菜单
                isVisible = false;
            }
        }

        /// <summary>
        /// Unity IMGUI绘制方法
        /// 每帧调用多次（布局事件+重绘事件），用于绘制菜单窗口
        /// </summary>
        void OnGUI()
        {
            if (!isVisible) return;

            // 使用KSP的UI皮肤保持风格一致（通过反射获取）
            GUISkin uiSkin = KSPApiHelper.GetUISkin();
            if (uiSkin != null)
            {
                GUI.skin = uiSkin;
            }

            // 绘制菜单窗口
            windowRect = GUILayout.Window(windowId, windowRect, DrawWindowContent, "发射倒计时控制");
        }

        /// <summary>
        /// 绘制菜单窗口内容
        /// </summary>
        /// <param name="windowId">窗口ID（由GUILayout.Window传入）</param>
        private void DrawWindowContent(int windowId)
        {
            // 使用垂直布局
            GUILayout.BeginVertical();

            // 预设选择区域
            GUILayout.Label("选择倒计时预设:");
            if (presetNames.Length > 0)
            {
                // 预设下拉选择
                selectedPresetIndex = GUILayout.SelectionGrid(
                    selectedPresetIndex,   // 当前选中索引
                    presetNames,           // 选项数组
                    1                      // 每行显示1个选项（垂直排列）
                );
            }
            else
            {
                // 无预设可用
                GUILayout.Label("未找到预设语音包");
            }

            // 间隔
            GUILayout.Space(10f);

            // Launch按钮
            bool canLaunch = presetNames.Length > 0 && !countdownController.IsCountingDown;
            GUI.enabled = canLaunch;
            if (GUILayout.Button("Launch", GUILayout.Height(30f)))
            {
                OnLaunchClicked();
            }
            GUI.enabled = true;

            // Cancel按钮（仅在倒计时进行中可用）
            GUI.enabled = countdownController.IsCountingDown;
            if (GUILayout.Button("Cancel", GUILayout.Height(30f)))
            {
                OnCancelClicked();
            }
            GUI.enabled = true;

            GUILayout.EndVertical();

            // 使窗口可拖拽（必须在所有控件绘制之后调用）
            GUI.DragWindow();
        }

        /// <summary>
        /// Launch按钮点击处理
        /// 获取选中的预设，调用倒计时控制器开始倒计时
        /// </summary>
        private void OnLaunchClicked()
        {
            if (presetNames.Length == 0 || selectedPresetIndex >= presetNames.Length)
            {
                Debug.LogWarning($"{LOG_TAG} 无法启动倒计时：无可用预设");
                return;
            }

            string selectedPresetName = presetNames[selectedPresetIndex];
            string audioPath = presetManager.GetPresetAudioPath(selectedPresetName);

            if (string.IsNullOrEmpty(audioPath))
            {
                Debug.LogError($"{LOG_TAG} 预设 '{selectedPresetName}' 的音频路径无效");
                return;
            }

            Debug.Log($"{LOG_TAG} 选择预设: {selectedPresetName}, 音频路径: {audioPath}");
            countdownController.StartCountdown(selectedPresetName, audioPath);
        }

        /// <summary>
        /// Cancel按钮点击处理
        /// 取消正在进行的倒计时
        /// </summary>
        private void OnCancelClicked()
        {
            countdownController.CancelCountdown();
        }

        /// <summary>
        /// Unity生命周期方法，在对象销毁时清理
        /// </summary>
        void OnDestroy()
        {
            if (countdownController != null)
            {
                countdownController.OnCountdownStateChanged -= OnCountdownStateChanged;
            }
        }
    }
}
