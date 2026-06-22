/**
 * ToolbarButton.cs - KSP1 发射倒计时工具栏按钮管理
 *
 * 用途：管理 KSP ApplicationLauncher 工具栏上的模组按钮。
 * 负责按钮的注册、图标加载、点击响应和清理。
 *
 * 功能说明：
 *   - 在飞行场景的工具栏上添加模组按钮
 *   - 从 GameData 目录加载 38x38 像素的 PNG 图标
 *   - 点击按钮时切换倒计时菜单的显示/隐藏
 *   - 场景切换或模组卸载时自动清理按钮
 *   - Ctrl+L 快捷键兜底切换菜单
 *
 * 实现方式（参考 ShipEngineOptimization 示例）：
 *   直接调用 KSP.UI.Screens 命名空间下的真实 ApplicationLauncher API，
 *   不再使用本地 stub 类型，也不使用反射。
 *   注册逻辑保留每帧 Update 轮询，确保 ApplicationLauncher 就绪后能够立即注册按钮。
 *
 * 关键 API（KSP 1.12，命名空间 KSP.UI.Screens）：
 *   ApplicationLauncher.Instance          -> 单例实例
 *   ApplicationLauncher.AppScenes.FLIGHT  -> 飞行场景枚举值（实际为 2）
 *   ApplicationLauncher.AddModApplication -> 注册模组按钮
 *   ApplicationLauncher.RemoveModApplication -> 移除模组按钮
 *
 * 图标说明：
 *   图标文件为 GameData/KSPLaunchCountdown/Textures/icon.png（38x38 像素）
 *   通过 GameDatabase.Instance.GetTexture 加载，路径格式为 "KSPLaunchCountdown/Textures/icon"
 *   （不含扩展名和 GameData 前缀）
 *
 * 依赖：
 *   - Assembly-CSharp.dll (KSP 核心，提供 KSP.UI.Screens.ApplicationLauncher)
 *   - UnityEngine.CoreModule.dll (Unity 核心)
 *   - UnityEngine.AnimationModule.dll (ApplicationLauncherButton 的间接依赖)
 *   - KSPApiHelper.cs (UI 控制、分级等辅助方法)
 */

using System;
using KSP.UI.Screens;
using UnityEngine;

namespace KSPLaunchCountdown
{
    /// <summary>
    /// 工具栏按钮管理器
    /// 直接调用 KSP.UI.Screens.ApplicationLauncher API（不再通过 stub 或反射）
    /// </summary>
    public class ToolbarButton : MonoBehaviour
    {
        /// <summary>日志标签</summary>
        private const string LOG_TAG = "[KSPLaunchCountdown]";

        /// <summary>
        /// 图标在 GameDatabase 中的路径（不含扩展名和 GameData 前缀）
        /// 对应文件：GameData/KSPLaunchCountdown/Textures/icon.png
        /// </summary>
        private const string ICON_DB_PATH = "KSPLaunchCountdown/Textures/icon";

        /// <summary>ApplicationLauncher 按钮实例</summary>
        private ApplicationLauncherButton launcherButton;

        /// <summary>倒计时菜单引用</summary>
        private CountdownMenu countdownMenu;

        /// <summary>加载的图标纹理</summary>
        private Texture2D iconTexture;

        /// <summary>
        /// 初始化工具栏按钮
        /// </summary>
        public void Initialize(CountdownMenu menu)
        {
            countdownMenu = menu;
            Debug.Log($"{LOG_TAG} 工具栏按钮初始化完成");
        }

        /// <summary>
        /// Unity 每帧更新
        /// 参考示例模式：每帧检查 ApplicationLauncher.Instance 是否已创建，
        /// 就绪后立即注册按钮。这种方式不依赖 GameEvents 事件时序，更可靠。
        /// </summary>
        void Update()
        {
            // 仅在飞行场景且按钮未注册时尝试注册
            if (launcherButton == null && HighLogic.LoadedSceneIsFlight)
            {
                SetupAppLauncher();
            }

            // Ctrl+L 快捷键兜底：切换菜单显示/隐藏
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
        }

        /// <summary>
        /// 设置 ApplicationLauncher 按钮
        /// 参考示例代码：先检查 ApplicationLauncher.Instance != null，
        /// 再调用 AddModApplication 注册按钮。
        /// </summary>
        private void SetupAppLauncher()
        {
            // 检查 ApplicationLauncher 单例是否已就绪
            if (ApplicationLauncher.Instance == null)
            {
                return;
            }

            // 加载图标纹理
            if (iconTexture == null)
            {
                iconTexture = LoadIconTexture();
                if (iconTexture == null) return;
            }

            // 直接调用 ApplicationLauncher.Instance.AddModApplication 注册按钮
            // 参数说明（与示例一致）：
            //   onTrue:       按钮激活回调
            //   onFalse:      按钮取消激活回调
            //   onHover:      鼠标悬停回调（不需要，传 null）
            //   onHoverOut:   鼠标移出回调（不需要，传 null）
            //   onEnable:     按钮启用回调（不需要，传 null）
            //   onDisable:    按钮禁用回调（不需要，传 null）
            //   visibleInScenes: 仅在飞行场景显示
            //   texture:      38x38 纹理图标
            launcherButton = ApplicationLauncher.Instance.AddModApplication(
                OnButtonToggle,       // 按钮激活回调：显示菜单
                OnButtonUntoggle,     // 按钮取消激活回调：隐藏菜单
                null,                 // 鼠标悬停回调
                null,                 // 鼠标移出回调
                null,                 // 按钮启用回调
                null,                 // 按钮禁用回调
                ApplicationLauncher.AppScenes.FLIGHT,  // 仅在飞行场景显示
                iconTexture           // 图标纹理
            );

            if (launcherButton != null)
            {
                Debug.Log($"{LOG_TAG} 工具栏按钮已注册");
            }
        }

        /// <summary>
        /// 从 GameDatabase 加载图标纹理
        /// 路径格式：模组名/目录/文件名（不含扩展名和 GameData 前缀）
        /// </summary>
        private Texture2D LoadIconTexture()
        {
            Texture2D tex = GameDatabase.Instance.GetTexture(ICON_DB_PATH, false);
            if (tex != null)
            {
                Debug.Log($"{LOG_TAG} 图标加载成功: {ICON_DB_PATH}");
            }
            else
            {
                Debug.LogError($"{LOG_TAG} 图标加载失败: {ICON_DB_PATH}。请确认 icon.png 已放入 GameData/KSPLaunchCountdown/Textures/ 目录");
            }
            return tex;
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
        /// 清理方法，由入口类在 OnDestroy 中调用
        /// 参考示例：检查按钮实例不为空后，调用 RemoveModApplication 移除按钮
        /// </summary>
        public void Cleanup()
        {
            if (launcherButton != null)
            {
                if (ApplicationLauncher.Instance != null)
                {
                    ApplicationLauncher.Instance.RemoveModApplication(launcherButton);
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
        /// Unity 销毁时自动清理
        /// </summary>
        void OnDestroy()
        {
            Cleanup();
        }
    }
}
