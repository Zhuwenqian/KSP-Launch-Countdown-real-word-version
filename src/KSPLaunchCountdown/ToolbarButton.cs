/**
 * ToolbarButton.cs - KSP1 发射倒计时工具栏按钮管理
 *
 * 用途：管理KSP ApplicationLauncher工具栏上的模组按钮。
 * 负责按钮的注册、图标生成、点击响应和清理。
 *
 * 功能说明：
 *   - 在飞行场景的工具栏上添加模组按钮
 *   - 使用代码生成38x38像素的临时图标（橙色圆形+白色"C"字符）
 *   - 点击按钮时切换倒计时菜单的显示/隐藏
 *   - 场景切换或模组卸载时自动清理按钮
 *
 * 实现方式（参考MechJeb）：
 *   不依赖GameEvents.onGUIApplicationLauncherReady事件，
 *   而是在每帧Update中检查ApplicationLauncher.Ready状态，
 *   就绪后立即注册按钮。这种方式更可靠，不会因事件时序问题导致按钮丢失。
 *
 * 兼容性说明：
 *   由于精简版Assembly-CSharp.dll缺少ApplicationLauncherButton等类型定义，
 *   本类通过KSPApiHelper反射辅助类调用KSP API，确保编译兼容性。
 *   运行时KSP游戏自带完整DLL，所有API均可正常工作。
 *
 * 按钮图标说明：
 *   当前使用代码生成的临时图标，后续可替换为正式的纹理图标。
 *   替换方式：将38x38像素的PNG纹理放入GameData/KSPLaunchCountdown/Textures/目录，
 *   然后修改SetupAppLauncher方法中的图标加载代码。
 *
 * 依赖：
 *   - Assembly-CSharp.dll (KSP核心，提供ApplicationLauncher、GameEvents等)
 *   - UnityEngine.CoreModule.dll (Unity核心，提供Texture2D、Color等)
 *   - KSPApiHelper.cs (KSP API反射辅助类)
 */

using System;
using UnityEngine;

namespace KSPLaunchCountdown
{
    /// <summary>
    /// 工具栏按钮管理器
    /// 参考MechJeb的实现模式：在Update中轮询ApplicationLauncher.Ready状态
    /// 通过KSPApiHelper反射调用KSP API，兼容精简版DLL
    /// </summary>
    public class ToolbarButton : MonoBehaviour
    {
        /// <summary>日志标签</summary>
        private const string LOG_TAG = "[KSPLaunchCountdown]";

        /// <summary>按钮图标尺寸（像素），KSP ApplicationLauncher标准尺寸</summary>
        private const int ICON_SIZE = 38;

        /// <summary>
        /// ApplicationLauncher按钮实例
        /// 使用object类型存储，通过KSPApiHelper操作
        /// </summary>
        private object launcherButton;

        /// <summary>倒计时菜单引用，用于切换菜单显示</summary>
        private CountdownMenu countdownMenu;

        /// <summary>代码生成的临时图标纹理</summary>
        private Texture2D iconTexture;

        /// <summary>
        /// 初始化工具栏按钮，注入倒计时菜单引用
        /// </summary>
        /// <param name="menu">倒计时菜单实例</param>
        public void Initialize(CountdownMenu menu)
        {
            countdownMenu = menu;
            Debug.Log($"{LOG_TAG} 工具栏按钮初始化完成");
        }

        /// <summary>
        /// Unity每帧更新
        /// 参考MechJeb模式：每帧检查ApplicationLauncher.Ready状态，
        /// 就绪后注册按钮。不依赖onGUIApplicationLauncherReady事件。
        /// </summary>
        void Update()
        {
            // 仅在飞行场景且按钮未注册时尝试注册
            if (launcherButton == null && HighLogic.LoadedSceneIsFlight)
            {
                SetupAppLauncher();
            }

            // Ctrl+L快捷键兜底：切换菜单显示/隐藏
            // 当工具栏按钮无法显示时，可通过此快捷键操作
            if (HighLogic.LoadedSceneIsFlight
                && (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl))
                && Input.GetKeyDown(KeyCode.L))
            {
                if (countdownMenu != null)
                {
                    countdownMenu.IsVisible = !countdownMenu.IsVisible;
                    Debug.Log($"{LOG_TAG} Ctrl+L 切换菜单: {(countdownMenu.IsVisible ? "显示" : "隐藏")}");
                }
            }

            // Ctrl+K快捷键：调试 - 扫描运行时分级相关类型
            // 用于确定KSP运行时中StageManager/Staging的实际类名和方法
            if (HighLogic.LoadedSceneIsFlight
                && (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl))
                && Input.GetKeyDown(KeyCode.K))
            {
                KSPApiHelper.ScanStagingTypes();
            }
        }

        /// <summary>
        /// 设置ApplicationLauncher按钮
        /// 参考MechJeb的SetupAppLauncher方法：
        ///   1. 检查ApplicationLauncher.Ready
        ///   2. 就绪后调用AddModApplication注册按钮
        /// </summary>
        private void SetupAppLauncher()
        {
            // 检查ApplicationLauncher是否就绪（参考MJ: if (!ApplicationLauncher.Ready) return;）
            if (!KSPApiHelper.IsApplicationLauncherReady())
            {
                return;
            }

            // 生成临时图标纹理
            if (iconTexture == null)
            {
                iconTexture = GenerateIconTexture();
            }

            // 添加模组按钮到ApplicationLauncher（通过反射）
            // 参考MJ: ApplicationLauncher.Instance.AddModApplication(
            //   ShowHideMasterWindow, ShowHideMasterWindow, null, null, null, null,
            //   ApplicationLauncher.AppScenes.ALWAYS, mjButtonTexture);
            int flightScenes = KSPApiHelper.GetAppScenesFlight();
            launcherButton = KSPApiHelper.AddModApplication(
                OnButtonToggle,       // 激活回调（按钮被按下）
                OnButtonUntoggle,     // 取消激活回调（按钮被弹起）
                null,                 // 鼠标悬停回调
                null,                 // 鼠标移出回调
                null,                 // 按钮启用回调
                null,                 // 按钮禁用回调
                flightScenes,         // 仅在飞行场景显示
                iconTexture           // 按钮图标
            );

            if (launcherButton != null)
            {
                Debug.Log($"{LOG_TAG} 工具栏按钮已注册");
            }
        }

        /// <summary>
        /// 按钮激活回调（按钮被按下/选中）
        /// 显示倒计时菜单
        /// </summary>
        private void OnButtonToggle()
        {
            if (countdownMenu != null)
            {
                countdownMenu.IsVisible = true;
            }
            Debug.Log($"{LOG_TAG} 工具栏按钮已激活 - 显示菜单");
        }

        /// <summary>
        /// 按钮取消激活回调（按钮被弹起/取消选中）
        /// 隐藏倒计时菜单
        /// </summary>
        private void OnButtonUntoggle()
        {
            if (countdownMenu != null)
            {
                countdownMenu.IsVisible = false;
            }
            Debug.Log($"{LOG_TAG} 工具栏按钮已取消 - 隐藏菜单");
        }

        /// <summary>
        /// 生成临时按钮图标纹理
        /// 创建一个38x38像素的图标：橙色背景+白色"C"字符（代表Countdown）
        /// 后续可替换为正式的纹理图标
        /// </summary>
        /// <returns>生成的Texture2D图标</returns>
        private Texture2D GenerateIconTexture()
        {
            // 创建RGBA格式的纹理
            Texture2D tex = new Texture2D(ICON_SIZE, ICON_SIZE, TextureFormat.RGBA32, false);
            tex.name = "KSPLaunchCountdown_Icon";

            // 填充像素
            Color32[] pixels = new Color32[ICON_SIZE * ICON_SIZE];

            // 颜色定义
            // 橙色背景：代表发射/火焰的颜色
            Color32 bgColor = new Color32(230, 126, 34, 255);
            // 深橙色边框
            Color32 borderColor = new Color32(211, 84, 0, 255);
            // 白色文字
            Color32 textColor = new Color32(255, 255, 255, 255);
            // 透明色
            Color32 transparent = new Color32(0, 0, 0, 0);

            // 绘制圆形图标
            float center = ICON_SIZE / 2f;
            float outerRadius = ICON_SIZE / 2f - 1f;   // 外圆半径（留1像素边距）
            float innerRadius = outerRadius - 2f;        // 内圆半径

            for (int y = 0; y < ICON_SIZE; y++)
            {
                for (int x = 0; x < ICON_SIZE; x++)
                {
                    float dx = x - center + 0.5f;
                    float dy = y - center + 0.5f;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);

                    int index = y * ICON_SIZE + x;

                    if (dist <= innerRadius)
                    {
                        // 内圆区域：橙色背景
                        pixels[index] = bgColor;
                    }
                    else if (dist <= outerRadius)
                    {
                        // 边框区域：深橙色
                        pixels[index] = borderColor;
                    }
                    else
                    {
                        // 外部：透明
                        pixels[index] = transparent;
                    }
                }
            }

            // 在圆形中心绘制"C"字符（简化版，5x7像素）
            // 使用简单的像素矩阵定义"C"的形状
            int[,] cShape = new int[,]
            {
                { 0, 1, 1, 1, 0 },
                { 1, 0, 0, 0, 1 },
                { 1, 0, 0, 0, 0 },
                { 1, 0, 0, 0, 0 },
                { 1, 0, 0, 0, 0 },
                { 1, 0, 0, 0, 1 },
                { 0, 1, 1, 1, 0 }
            };

            // 将"C"字符绘制到图标中心
            int charOffsetX = (ICON_SIZE - 5) / 2;
            int charOffsetY = (ICON_SIZE - 7) / 2;

            for (int cy = 0; cy < 7; cy++)
            {
                for (int cx = 0; cx < 5; cx++)
                {
                    if (cShape[cy, cx] == 1)
                    {
                        int px = charOffsetX + cx;
                        int py = charOffsetY + cy;
                        if (px >= 0 && px < ICON_SIZE && py >= 0 && py < ICON_SIZE)
                        {
                            pixels[py * ICON_SIZE + px] = textColor;
                        }
                    }
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply();

            return tex;
        }

        /// <summary>
        /// 清理方法，由入口类在OnDestroy中调用
        /// 参考MechJeb的ClearButtons方法：
        ///   检查按钮的gameObject是否还存在再移除
        /// </summary>
        public void Cleanup()
        {
            // 参考MJ: if (mjButton != null && mjButton.gameObject != null)
            // 通过反射检查按钮的gameObject是否存在
            if (launcherButton != null)
            {
                bool gameObjectExists = true;
                try
                {
                    var goProp = launcherButton.GetType().GetProperty("gameObject",
                        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                    if (goProp != null)
                    {
                        var go = goProp.GetValue(launcherButton);
                        if (go == null) gameObjectExists = false;
                    }
                }
                catch
                {
                    // 无法检查，假设存在
                }

                if (gameObjectExists)
                {
                    KSPApiHelper.RemoveModApplication(launcherButton);
                }
                launcherButton = null;
            }

            // 销毁图标纹理
            if (iconTexture != null)
            {
                Destroy(iconTexture);
                iconTexture = null;
            }

            Debug.Log($"{LOG_TAG} 工具栏按钮已清理");
        }

        /// <summary>
        /// Unity生命周期方法，在对象销毁时清理
        /// 确保即使Cleanup未被显式调用也能清理资源
        /// </summary>
        void OnDestroy()
        {
            Cleanup();
        }
    }
}
